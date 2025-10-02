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
        _ch.BasicConsume(_opts.RequestQueue, autoAck: false, consumer: _consumer);

        _logger.LogInformation("AMQP listener started on queue '{Queue}'", _opts.RequestQueue);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<ConnectionContext?> AcceptAsync(CancellationToken cancellationToken = default)
    {
        if (await _acceptQueue.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false)
            && _acceptQueue.Reader.TryRead(out var ctx))
        {
            return ctx;
        }
        return null;
    }

    /// <inheritdoc />
    public ValueTask UnbindAsync(CancellationToken cancellationToken = default)
    {
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
            await _acceptQueue.Writer.WriteAsync(ctx).ConfigureAwait(false);
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
            var props = _ch.CreateBasicProperties();
            props.Persistent = _opts.Persistent;
            props.CorrelationId = correlationId;

            _ch.BasicPublish(
                exchange: "",
                routingKey: responseQueue,
                mandatory: false,
                basicProperties: props,
                body: responseRaw);
            
            return Task.CompletedTask;
        }

        return (buf, PublishAsync);
    }
}
