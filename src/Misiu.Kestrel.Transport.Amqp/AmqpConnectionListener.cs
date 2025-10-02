using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Misiu.Kestrel.Transport.Amqp;

/// <summary>
/// Listens on a RabbitMQ queue; each message becomes a synthetic ConnectionContext
/// whose Transport.Input contains a raw HTTP/1.1 request. Kestrel parses it and writes
/// a raw HTTP response to Application.Output, which we publish back to AMQP.
/// </summary>
public sealed class AmqpConnectionListener : IConnectionListener, IDisposable
{
    private readonly AmqpEndPoint _endpoint;
    private readonly AmqpTransportOptions _opts;
    private readonly ILogger<AmqpConnectionListener> _logger;

    private IConnection? _conn;
    private IModel? _ch;
    private AsyncEventingBasicConsumer? _consumer;
    private string? _consumerTag;

    // Accept queue for Kestrel to pick up ConnectionContext instances
    private readonly Channel<AmqpConnectionContext> _acceptQueue = Channel.CreateUnbounded<AmqpConnectionContext>(
        new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });

    /// <inheritdoc />
    public EndPoint EndPoint => _endpoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="AmqpConnectionListener"/> class
    /// </summary>
    public AmqpConnectionListener(AmqpEndPoint endpoint, AmqpTransportOptions opts, ILogger<AmqpConnectionListener> logger)
    {
        _endpoint = endpoint;
        _opts = opts;
        _logger = logger;
    }

    /// <summary>
    /// Starts the listener
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _opts.HostName,
            Port = _opts.Port,
            VirtualHost = _opts.VirtualHost,
            UserName = _opts.UserName,
            Password = _opts.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };

        _conn = factory.CreateConnection($"AmqpTransport-{_endpoint.Name}");
        _ch = _conn.CreateModel();

        _ch.QueueDeclare(_opts.RequestQueue, durable: _opts.Persistent, exclusive: false, autoDelete: false, arguments: null);
        _ch.QueueDeclare(_opts.ResponseQueue, durable: _opts.Persistent, exclusive: false, autoDelete: false, arguments: null);
        _ch.BasicQos(0, _opts.PrefetchCount, global: false);

        _consumer = new AsyncEventingBasicConsumer(_ch);
        _consumer.Received += OnReceivedAsync;
        
        // Use unique consumer tag to avoid conflicts with previous consumers
        _consumerTag = $"AmqpTransport-{_endpoint.Name}-{Guid.NewGuid()}";
        _ch.BasicConsume(_opts.RequestQueue, autoAck: false, consumer: _consumer, consumerTag: _consumerTag);

        _logger.LogInformation("AMQP listener started on queue '{Queue}'", _opts.RequestQueue);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<ConnectionContext?> AcceptAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("AcceptAsync called, waiting for connection...");
        
        if (await _acceptQueue.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false)
            && _acceptQueue.Reader.TryRead(out var ctx))
        {
            _logger.LogDebug("AcceptAsync returning connection {ConnectionId}", ctx.ConnectionId);
            return ctx;
        }
        
        _logger.LogDebug("AcceptAsync returning null (no more connections)");
        return null;
    }

    /// <inheritdoc />
    public ValueTask UnbindAsync(CancellationToken cancellationToken = default)
    {
        // Complete the accept queue so any waiting AcceptAsync calls will complete
        _acceptQueue.Writer.TryComplete();
        
        // Cancel the consumer first to stop receiving new messages
        try
        {
            if (_consumer != null && _ch != null && _consumerTag != null)
            {
                _consumer.Received -= OnReceivedAsync;
                // Explicitly cancel the consumer before closing the channel
                _ch.BasicCancel(_consumerTag);
            }
        }
        catch
        {
            // Ignore errors
        }
        
        try
        {
            _ch?.Close();
        }
        catch
        {
            // Ignore close errors
        }
        try
        {
            _conn?.Close();
        }
        catch
        {
            // Ignore close errors
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            _ch?.Dispose();
        }
        catch
        {
            // Ignore dispose errors
        }
        try
        {
            _conn?.Dispose();
        }
        catch
        {
            // Ignore dispose errors
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            var correlationId = ea.BasicProperties?.CorrelationId ?? Guid.NewGuid().ToString();
            _logger.LogDebug("OnReceivedAsync: Received AMQP message with correlationId {CorrelationId}", correlationId);

            // Build a raw HTTP/1.1 request bytes for Kestrel HTTP parser
            var (requestBytes, responsePublisher) = BuildRawHttpRequestAndResponder(ea, _opts.ResponseQueue, correlationId);

            // Connection pair: we need to provide a pipe for Kestrel
            // Kestrel reads from Transport.Input and writes to Transport.Output
            var inputPipe = new Pipe();
            var outputPipe = new Pipe();
            
            var transport = new DuplexPipe(inputPipe.Reader, outputPipe.Writer);

            // Feed raw request into inputPipe.Writer
            _ = Task.Run(async () =>
            {
                try
                {
                    await inputPipe.Writer.WriteAsync(requestBytes).ConfigureAwait(false);
                    await inputPipe.Writer.FlushAsync().ConfigureAwait(false);
                    inputPipe.Writer.Complete();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error writing AMQP request bytes to transport");
                    inputPipe.Writer.Complete(ex);
                }
            });

            // ConnectionContext consumed by Kestrel
            var ctx = new AmqpConnectionContext(transport, outputPipe.Reader, _ch!, ea.DeliveryTag, responsePublisher, _logger, correlationId);
            _logger.LogDebug("OnReceivedAsync: Writing connection {ConnectionId} to accept queue", correlationId);
            await _acceptQueue.Writer.WriteAsync(ctx).ConfigureAwait(false);
            _logger.LogDebug("OnReceivedAsync: Connection {ConnectionId} written to accept queue", correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert AMQP message to ConnectionContext");
            try
            {
                _ch!.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch
            {
                // Ignore ack errors
            }
        }
    }

    private (ReadOnlyMemory<byte> requestBytes, Func<ReadOnlyMemory<byte>, Task> publishResponse)
        BuildRawHttpRequestAndResponder(BasicDeliverEventArgs ea, string responseQueue, string correlationId)
    {
        using var doc = JsonDocument.Parse(ea.Body.ToArray());
        var root = doc.RootElement;

        var method = root.GetProperty("method").GetString() ?? "GET";
        var pathAndQuery = root.GetProperty("pathAndQuery").GetString() ?? "/";
        if (!pathAndQuery.StartsWith("/", StringComparison.Ordinal))
        {
            pathAndQuery = "/" + pathAndQuery;
        }

        var sb = new StringBuilder(512);
        sb.Append(method).Append(' ').Append(pathAndQuery).Append(" HTTP/1.1\r\n");

        if (root.TryGetProperty("headers", out var headers) && headers.ValueKind == JsonValueKind.Object)
        {
            foreach (var h in headers.EnumerateObject())
            {
                if (h.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in h.Value.EnumerateArray())
                    {
                        sb.Append(h.Name).Append(": ").Append(v.GetString()).Append("\r\n");
                    }
                }
                else
                {
                    sb.Append(h.Name).Append(": ").Append(h.Value.GetString()).Append("\r\n");
                }
            }
        }

        byte[] body = Array.Empty<byte>();
        if (root.TryGetProperty("bodyBase64", out var b) && b.ValueKind == JsonValueKind.String)
        {
            body = Convert.FromBase64String(b.GetString()!);
            sb.Append("Content-Length: ").Append(body.Length).Append("\r\n");
        }

        if (!sb.ToString().Contains("\r\nHost:", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append("Host: amqp-transport\r\n");
        }
        sb.Append("\r\n");

        var headBytes = Encoding.ASCII.GetBytes(sb.ToString());
        var buf = new byte[headBytes.Length + body.Length];
        Buffer.BlockCopy(headBytes, 0, buf, 0, headBytes.Length);
        if (body.Length > 0)
        {
            Buffer.BlockCopy(body, 0, buf, headBytes.Length, body.Length);
        }

        Task PublishAsync(ReadOnlyMemory<byte> responseRaw)
        {
            if (_ch == null)
            {
                _logger.LogWarning("Channel is null, cannot publish response for {CorrelationId}", correlationId);
                return Task.CompletedTask;
            }
            
            try
            {
                // Parse raw HTTP response to extract status code, headers, and body
                var responseEnvelope = ParseRawHttpResponse(responseRaw);
                responseEnvelope.CorrelationId = Guid.Parse(correlationId);
                
                // Serialize to JSON with camelCase (to match gateway expectations)
                var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(responseEnvelope, jsonOptions);
                
                var props = _ch.CreateBasicProperties();
                props.Persistent = _opts.Persistent;
                props.CorrelationId = correlationId;

                _ch.BasicPublish(
                    exchange: "",
                    routingKey: responseQueue,
                    mandatory: false,
                    basicProperties: props,
                    body: jsonBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing response for {CorrelationId}", correlationId);
            }
            
            return Task.CompletedTask;
        }
        
        HttpResponseEnvelope ParseRawHttpResponse(ReadOnlyMemory<byte> responseRaw)
        {
            var responseStr = Encoding.UTF8.GetString(responseRaw.Span);
            var lines = responseStr.Split(new[] { "\r\n" }, StringSplitOptions.None);
            
            // Parse status line (e.g., "HTTP/1.1 200 OK")
            var statusLine = lines[0];
            var statusParts = statusLine.Split(' ', 3);
            var statusCode = int.Parse(statusParts[1]);
            
            // Parse headers
            var headers = new Dictionary<string, string[]>();
            int bodyStartIndex = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i]))
                {
                    bodyStartIndex = i + 1;
                    break;
                }
                
                var colonIndex = lines[i].IndexOf(':');
                if (colonIndex > 0)
                {
                    var headerName = lines[i].Substring(0, colonIndex).Trim();
                    var headerValue = lines[i].Substring(colonIndex + 1).Trim();
                    
                    if (headers.ContainsKey(headerName))
                    {
                        var existing = headers[headerName];
                        var newArray = new string[existing.Length + 1];
                        existing.CopyTo(newArray, 0);
                        newArray[existing.Length] = headerValue;
                        headers[headerName] = newArray;
                    }
                    else
                    {
                        headers[headerName] = new[] { headerValue };
                    }
                }
            }
            
            // Extract body (everything after the empty line)
            byte[]? body = null;
            if (bodyStartIndex < lines.Length)
            {
                var bodyText = string.Join("\r\n", lines, bodyStartIndex, lines.Length - bodyStartIndex);
                if (!string.IsNullOrEmpty(bodyText))
                {
                    body = Encoding.UTF8.GetBytes(bodyText);
                }
            }
            
            return new HttpResponseEnvelope
            {
                CorrelationId = Guid.Empty, // Will be set by caller
                StatusCode = statusCode,
                Headers = headers,
                Body = body,
                ProcessingMilliseconds = 0 // Not tracked in Transport approach
            };
        }

        return (buf, PublishAsync);
    }
}
