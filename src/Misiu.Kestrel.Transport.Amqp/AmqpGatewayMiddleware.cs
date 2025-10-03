using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Misiu.Kestrel.Transport.Amqp;

/// <summary>
/// Middleware that captures HTTP requests and forwards them via AMQP to a remote client
/// </summary>
public class AmqpGatewayMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AmqpGatewayMiddleware> _logger;
    private readonly AmqpTransportOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<HttpResponseEnvelope>> _pendingRequests;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// Initializes a new instance of the <see cref="AmqpGatewayMiddleware"/> class
    /// </summary>
    public AmqpGatewayMiddleware(
        RequestDelegate next,
        ILogger<AmqpGatewayMiddleware> logger,
        IOptions<AmqpTransportOptions> options,
        IMemoryCache cache)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
        _cache = cache;
        _pendingRequests = new ConcurrentDictionary<Guid, TaskCompletionSource<HttpResponseEnvelope>>();

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

        _connection = factory.CreateConnection("AmqpGateway-Server");
        _channel = _connection.CreateModel();

        // Declare queues
        _channel.QueueDeclare(_options.RequestQueue, durable: _options.Persistent, exclusive: false, autoDelete: false);
        _channel.QueueDeclare(_options.ResponseQueue, durable: _options.Persistent, exclusive: false, autoDelete: false);

        // Start consuming responses
        StartResponseConsumer();
    }

    private void StartResponseConsumer()
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (sender, ea) =>
        {
            try
            {
                var correlationIdStr = ea.BasicProperties?.CorrelationId;
                if (string.IsNullOrWhiteSpace(correlationIdStr) || !Guid.TryParse(correlationIdStr, out var correlationId))
                {
                    _logger.LogWarning("Received response without valid CorrelationId");
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                var envelope = JsonSerializer.Deserialize<HttpResponseEnvelope>(ea.Body.ToArray(), _jsonOptions);
                if (envelope == null)
                {
                    _logger.LogWarning("Failed to deserialize response for {CorrelationId}", correlationId);
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                // Try to complete any pending request
                if (_pendingRequests.TryRemove(correlationId, out var tcs))
                {
                    tcs.TrySetResult(envelope);
                }

                // Store in cache for late retrieval
                var cacheKey = $"amqp:result:{correlationId:N}";
                _cache.Set(cacheKey, envelope, TimeSpan.FromMinutes(_options.ResultTtlMinutes));

                _channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing response");
                _channel.BasicAck(ea.DeliveryTag, false);
            }

            await Task.Yield();
        };

        _channel.BasicQos(0, 50, false);
        _channel.BasicConsume(_options.ResponseQueue, autoAck: false, consumer: consumer);
    }

    /// <summary>
    /// Invokes the middleware
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Guid.NewGuid();
        context.Items["AmqpCorrelationId"] = correlationId;

        // Read request body
        byte[]? body = null;
        if (context.Request.ContentLength.HasValue && context.Request.ContentLength.Value > 0)
        {
            using var ms = new MemoryStream((int)context.Request.ContentLength.Value);
            await context.Request.Body.CopyToAsync(ms);
            body = ms.ToArray();
        }

        // Build request envelope
        var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in context.Request.Headers)
        {
            headers[header.Key] = header.Value.ToArray()!;
        }

        // Transform path before forwarding
        var transformedPath = TransformPath(context.Request.Path + context.Request.QueryString);

        var envelope = new HttpRequestEnvelope
        {
            CorrelationId = correlationId,
            Method = context.Request.Method,
            PathAndQuery = transformedPath,
            Headers = headers,
            Body = body,
            ContentType = context.Request.ContentType,
            GatewayEnqueuedAtUtc = DateTimeOffset.UtcNow
        };

        // Serialize and publish
        var payload = JsonSerializer.SerializeToUtf8Bytes(envelope, _jsonOptions);
        var props = _channel.CreateBasicProperties();
        props.CorrelationId = correlationId.ToString();
        props.Persistent = _options.Persistent;

        // Register pending request
        var tcs = new TaskCompletionSource<HttpResponseEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests.TryAdd(correlationId, tcs);

        try
        {
            _channel.BasicPublish("", _options.RequestQueue, false, props, payload);
            _logger.LogInformation("Published request {CorrelationId} to {Queue}", correlationId, _options.RequestQueue);

            // Wait for response with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.ImmediateTimeoutSeconds));
            var response = await tcs.Task.WaitAsync(cts.Token);

            // Return response
            context.Response.StatusCode = response.StatusCode;
            context.Response.Headers["X-CorrelationId"] = correlationId.ToString();
            context.Response.Headers["X-Processing-Time-Ms"] = response.ProcessingMilliseconds.ToString();

            // Copy headers (excluding hop-by-hop headers which are invalid for HTTP/2 and HTTP/3)
            var hopByHopHeaders = new[] { "Connection", "Keep-Alive", "Transfer-Encoding", "Upgrade", "Proxy-Connection" };
            foreach (var header in response.Headers)
            {
                if (!hopByHopHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))
                {
                    context.Response.Headers[header.Key] = header.Value;
                }
            }

            if (response.Body != null && response.Body.Length > 0)
            {
                // Set Content-Length to avoid chunked transfer encoding
                context.Response.ContentLength = response.Body.Length;
                await context.Response.Body.WriteAsync(response.Body);
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout - return 202 Accepted
            _pendingRequests.TryRemove(correlationId, out _);
            context.Response.StatusCode = 202;
            context.Response.Headers["X-CorrelationId"] = correlationId.ToString();
            context.Response.Headers["Location"] = $"/amqp/result/{correlationId}";

            var acceptedResponse = new
            {
                correlationId = correlationId,
                status = "accepted",
                message = "Request is being processed. Check Location header for result.",
                location = $"/amqp/result/{correlationId}"
            };

            await context.Response.WriteAsJsonAsync(acceptedResponse);
            _logger.LogInformation("Request {CorrelationId} timed out, returning 202", correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request {CorrelationId}", correlationId);
            _pendingRequests.TryRemove(correlationId, out _);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Internal server error");
        }
    }

    private string TransformPath(string pathAndQuery)
    {
        var path = pathAndQuery;

        // Remove prefix if configured
        if (!string.IsNullOrEmpty(_options.PathPrefixToRemove))
        {
            var prefix = _options.PathPrefixToRemove;
            if (!prefix.StartsWith("/"))
            {
                prefix = "/" + prefix;
            }

            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(prefix.Length);
                if (!path.StartsWith("/"))
                {
                    path = "/" + path;
                }
            }
        }

        // Add prefix if configured
        if (!string.IsNullOrEmpty(_options.PathPrefixToAdd))
        {
            var prefix = _options.PathPrefixToAdd;
            if (!prefix.StartsWith("/"))
            {
                prefix = "/" + prefix;
            }

            if (prefix.EndsWith("/"))
            {
                prefix = prefix.TrimEnd('/');
            }

            path = prefix + path;
        }

        return path;
    }
}
