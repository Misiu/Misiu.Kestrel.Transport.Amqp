namespace Misiu.Kestrel.Transport.Amqp;

/// <summary>
/// Represents an HTTP request envelope for transport over AMQP
/// </summary>
public sealed class HttpRequestEnvelope
{
    /// <summary>
    /// Gets or sets the correlation ID for request tracking
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the HTTP method
    /// </summary>
    public string Method { get; set; } = "GET";

    /// <summary>
    /// Gets or sets the path and query string
    /// </summary>
    public string PathAndQuery { get; set; } = "/";

    /// <summary>
    /// Gets or sets the HTTP headers
    /// </summary>
    public Dictionary<string, string[]> Headers { get; set; } = new();

    /// <summary>
    /// Gets or sets the request body
    /// </summary>
    public byte[]? Body { get; set; }

    /// <summary>
    /// Gets or sets the content type
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets when the gateway enqueued this request
    /// </summary>
    public DateTimeOffset GatewayEnqueuedAtUtc { get; set; }
}
