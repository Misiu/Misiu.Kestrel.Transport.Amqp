namespace Misiu.Kestrel.Transport.Amqp;

/// <summary>
/// Options for AMQP transport
/// </summary>
public class AmqpTransportOptions
{
    /// <summary>
    /// Gets or sets the RabbitMQ hostname
    /// </summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the RabbitMQ port
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Gets or sets the RabbitMQ virtual host
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Gets or sets the RabbitMQ username
    /// </summary>
    public string UserName { get; set; } = "guest";

    /// <summary>
    /// Gets or sets the RabbitMQ password
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Gets or sets the request queue name
    /// </summary>
    public string RequestQueue { get; set; } = "amqp.gateway.requests";

    /// <summary>
    /// Gets or sets the response queue name
    /// </summary>
    public string ResponseQueue { get; set; } = "amqp.gateway.responses";

    /// <summary>
    /// Gets or sets whether messages should be persistent
    /// </summary>
    public bool Persistent { get; set; } = true;

    /// <summary>
    /// Gets or sets the prefetch count for consuming messages
    /// </summary>
    public ushort PrefetchCount { get; set; } = 32;

    /// <summary>
    /// Gets or sets the maximum request body size in bytes
    /// </summary>
    public long MaxRequestBodyBytes { get; set; } = 10_000_000;

    /// <summary>
    /// Gets or sets the immediate response timeout in seconds (for gateway)
    /// </summary>
    public int ImmediateTimeoutSeconds { get; set; } = 3;

    /// <summary>
    /// Gets or sets the result TTL in minutes for late retrieval (for gateway)
    /// </summary>
    public int ResultTtlMinutes { get; set; } = 15;

    /// <summary>
    /// Gets or sets the local API base URL (for client)
    /// </summary>
    public string LocalApiBaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>
    /// Gets or sets the path prefix to remove from incoming requests (for client)
    /// Example: "/proxy" will transform "/proxy/api/data" to "/api/data"
    /// </summary>
    public string? PathPrefixToRemove { get; set; }

    /// <summary>
    /// Gets or sets the path prefix to add to outgoing requests (for client)
    /// Example: "/api/v1" will transform "/data" to "/api/v1/data"
    /// </summary>
    public string? PathPrefixToAdd { get; set; }
}
