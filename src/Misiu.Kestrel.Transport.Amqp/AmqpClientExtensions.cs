using Microsoft.Extensions.DependencyInjection;

namespace Misiu.Kestrel.Transport.Amqp;

/// <summary>
/// Extension methods for configuring AMQP Client
/// </summary>
public static class AmqpClientExtensions
{
    /// <summary>
    /// Adds AMQP Client services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Configuration action for AMQP transport options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAmqpClient(
        this IServiceCollection services,
        Action<AmqpTransportOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient("AmqpClient");
        services.AddHostedService<AmqpClientConsumer>();
        return services;
    }
}
