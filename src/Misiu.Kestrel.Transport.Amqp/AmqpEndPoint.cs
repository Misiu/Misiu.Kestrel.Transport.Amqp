using System.Net;

namespace Misiu.Kestrel.Transport.Amqp;

/// <summary>
/// A pseudo EndPoint recognized by the AMQP connection listener factory.
/// It lets Kestrel "listen" on RabbitMQ (AMQP) side-by-side with TCP.
/// </summary>
public sealed class AmqpEndPoint : EndPoint
{
    /// <summary>
    /// Gets the endpoint name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the options name
    /// </summary>
    public string? OptionsName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AmqpEndPoint"/> class
    /// </summary>
    /// <param name="name">The endpoint name</param>
    /// <param name="optionsName">The options name</param>
    public AmqpEndPoint(string name, string? optionsName = null)
    {
        Name = name;
        OptionsName = optionsName;
    }

    /// <summary>
    /// Returns a string representation of the endpoint
    /// </summary>
    public override string ToString()
    {
        return $"amqp://{Name}";
    }
}
