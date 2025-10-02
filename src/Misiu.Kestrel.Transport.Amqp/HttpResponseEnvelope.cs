namespace Misiu.Kestrel.Transport.Amqp;

/// <summary>
/// Represents an HTTP response envelope for transport over AMQP
/// </summary>
public sealed class HttpResponseEnvelope
{
    /// <summary>
    /// Gets or sets the correlation ID for request tracking
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the HTTP status code
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Gets or sets the HTTP headers
    /// </summary>
    public Dictionary<string, string[]> Headers { get; set; } = new();

    /// <summary>
    /// Gets or sets the response body
    /// </summary>
    public byte[]? Body { get; set; }

    /// <summary>
    /// Gets or sets the content type
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets when the server started processing
    /// </summary>
    public DateTimeOffset ServerStartedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the server completed processing
    /// </summary>
    public DateTimeOffset ServerCompletedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the processing time in milliseconds
    /// </summary>
    public long ProcessingMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets when the gateway enqueued the original request
    /// </summary>
    public DateTimeOffset GatewayEnqueuedAtUtc { get; set; }
}
