namespace Misiu.Kestrel.Transport.Amqp;

/// <summary>
/// Options for AMQP transport
/// </summary>
public class AmqpTransportOptions
{
    /// <summary>
    /// Gets or sets the AMQP connection string
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the maximum message size
    /// </summary>
    public int MaxMessageSize { get; set; } = 65536;
}
