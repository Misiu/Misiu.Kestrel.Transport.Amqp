using Microsoft.Extensions.Configuration;
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

    /// <summary>
    /// Adds AMQP Client services to the service collection using configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration section</param>
    /// <param name="sectionName">The configuration section name (default: "AmqpClient")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAmqpClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "AmqpClient")
    {
        services.Configure<AmqpTransportOptions>(configuration.GetSection(sectionName));
        services.AddHttpClient("AmqpClient");
        services.AddHostedService<AmqpClientConsumer>();
        return services;
    }
}
