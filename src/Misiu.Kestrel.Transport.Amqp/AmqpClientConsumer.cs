using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Misiu.Kestrel.Transport.Amqp;

/// <summary>
/// Background service that consumes AMQP requests and forwards them to a local HTTP API
/// </summary>
public class AmqpClientConsumer : BackgroundService
{
    private readonly ILogger<AmqpClientConsumer> _logger;
    private readonly AmqpTransportOptions _options;
    private readonly HttpClient _httpClient;
    private IConnection? _connection;
    private IModel? _channel;

    /// <summary>
    /// Initializes a new instance of the <see cref="AmqpClientConsumer"/> class
    /// </summary>
    public AmqpClientConsumer(
        ILogger<AmqpClientConsumer> logger,
        IOptions<AmqpTransportOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _options = options.Value;
        _httpClient = httpClientFactory.CreateClient("AmqpClient");
        
        if (!string.IsNullOrEmpty(_options.LocalApiBaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.LocalApiBaseUrl);
        }
    }

    /// <summary>
    /// Executes the background service
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting AMQP Client Consumer");

        // Initialize RabbitMQ connection
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            VirtualHost = _options.VirtualHost,
            UserName = _options.UserName,
            Password = _options.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };

        _connection = factory.CreateConnection("AmqpClient-Consumer");
        _channel = _connection.CreateModel();

        // Declare queues
        _channel.QueueDeclare(_options.RequestQueue, durable: _options.Persistent, exclusive: false, autoDelete: false);
        _channel.QueueDeclare(_options.ResponseQueue, durable: _options.Persistent, exclusive: false, autoDelete: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (sender, ea) =>
        {
            try
            {
                var correlationIdStr = ea.BasicProperties?.CorrelationId;
                if (string.IsNullOrWhiteSpace(correlationIdStr) || !Guid.TryParse(correlationIdStr, out var correlationId))
                {
                    _logger.LogWarning("Received request without valid CorrelationId");
                    _channel!.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                var request = JsonSerializer.Deserialize<HttpRequestEnvelope>(ea.Body.ToArray());
                if (request == null)
                {
                    _logger.LogWarning("Failed to deserialize request for {CorrelationId}", correlationId);
                    _channel!.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                _logger.LogInformation("Processing request {CorrelationId}: {Method} {Path}", 
                    correlationId, request.Method, request.PathAndQuery);

                var sw = Stopwatch.StartNew();
                var serverStarted = DateTimeOffset.UtcNow;

                try
                {
                    // Execute HTTP request to local API
                    var response = await ExecuteRequestAsync(request, stoppingToken);
                    sw.Stop();

                    // Build response envelope
                    var responseEnvelope = new HttpResponseEnvelope
                    {
                        CorrelationId = correlationId,
                        StatusCode = (int)response.StatusCode,
                        Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
                        Body = await response.Content.ReadAsByteArrayAsync(stoppingToken),
                        ContentType = response.Content.Headers.ContentType?.ToString(),
                        ServerStartedAtUtc = serverStarted,
                        ServerCompletedAtUtc = DateTimeOffset.UtcNow,
                        ProcessingMilliseconds = sw.ElapsedMilliseconds,
                        GatewayEnqueuedAtUtc = request.GatewayEnqueuedAtUtc
                    };

                    // Copy response headers
                    foreach (var header in response.Headers)
                    {
                        responseEnvelope.Headers[header.Key] = header.Value.ToArray();
                    }
                    foreach (var header in response.Content.Headers)
                    {
                        if (!responseEnvelope.Headers.ContainsKey(header.Key))
                        {
                            responseEnvelope.Headers[header.Key] = header.Value.ToArray();
                        }
                    }

                    // Publish response
                    var responsePayload = JsonSerializer.SerializeToUtf8Bytes(responseEnvelope);
                    var props = _channel!.CreateBasicProperties();
                    props.CorrelationId = correlationId.ToString();
                    props.Persistent = _options.Persistent;

                    _channel.BasicPublish("", _options.ResponseQueue, false, props, responsePayload);
                    _logger.LogInformation("Published response for {CorrelationId} with status {StatusCode} ({Duration}ms)",
                        correlationId, response.StatusCode, sw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing request {CorrelationId}", correlationId);
                    
                    // Send error response
                    var errorEnvelope = new HttpResponseEnvelope
                    {
                        CorrelationId = correlationId,
                        StatusCode = 502,
                        Headers = new Dictionary<string, string[]> 
                        { 
                            ["Content-Type"] = new[] { "application/json" } 
                        },
                        Body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = ex.Message })),
                        ContentType = "application/json",
                        ServerStartedAtUtc = serverStarted,
                        ServerCompletedAtUtc = DateTimeOffset.UtcNow,
                        ProcessingMilliseconds = sw.ElapsedMilliseconds,
                        GatewayEnqueuedAtUtc = request.GatewayEnqueuedAtUtc
                    };

                    var errorPayload = JsonSerializer.SerializeToUtf8Bytes(errorEnvelope);
                    var props = _channel!.CreateBasicProperties();
                    props.CorrelationId = correlationId.ToString();
                    props.Persistent = _options.Persistent;

                    _channel.BasicPublish("", _options.ResponseQueue, false, props, errorPayload);
                }

                _channel!.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing message");
                _channel!.BasicAck(ea.DeliveryTag, false);
            }

            await Task.Yield();
        };

        _channel.BasicQos(0, _options.PrefetchCount, false);
        _channel.BasicConsume(_options.RequestQueue, autoAck: false, consumer: consumer);

        _logger.LogInformation("AMQP Client Consumer started, listening on {Queue}", _options.RequestQueue);

        // Keep running until cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task<HttpResponseMessage> ExecuteRequestAsync(HttpRequestEnvelope request, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method), request.PathAndQuery);

        // Copy headers
        foreach (var header in request.Headers)
        {
            // Skip hop-by-hop headers
            if (header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                // May need to add to content headers
            }
        }

        // Add body if present
        if (request.Body != null && request.Body.Length > 0)
        {
            httpRequest.Content = new ByteArrayContent(request.Body);
            if (!string.IsNullOrEmpty(request.ContentType))
            {
                httpRequest.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(request.ContentType);
            }
        }

        return await _httpClient.SendAsync(httpRequest, cancellationToken);
    }

    /// <summary>
    /// Disposes the consumer
    /// </summary>
    public override void Dispose()
    {
        base.Dispose();
        try { _channel?.Close(); } catch { }
        try { _channel?.Dispose(); } catch { }
        try { _connection?.Close(); } catch { }
        try { _connection?.Dispose(); } catch { }
        _httpClient?.Dispose();
    }
}
