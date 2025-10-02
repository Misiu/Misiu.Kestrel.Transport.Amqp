using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Misiu.Kestrel.Transport.Amqp;

/// <summary>
/// Extensions to wire AMQP transport into Kestrel
/// </summary>
public static class KestrelAmqpExtensions
{
    /// <summary>
    /// Registers the AmqpConnectionListenerFactory in DI
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Optional configuration action</param>
    /// <param name="optionsName">The options name</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAmqpTransport(
        this IServiceCollection services,
        Action<AmqpTransportOptions>? configure = null,
        string? optionsName = null)
    {
        var name = optionsName ?? Options.DefaultName;
        if (configure is not null)
        {
            services.AddOptions<AmqpTransportOptions>(name)
                .Configure(configure)
                .ValidateOnStart();
        }
        else
        {
            services.AddOptions<AmqpTransportOptions>(name);
        }

        // Ensure factory is present (coexists with other transports)
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConnectionListenerFactory, AmqpConnectionListenerFactory>());

        return services;
    }

    /// <summary>
    /// Adds an AMQP listener endpoint to Kestrel
    /// </summary>
    /// <param name="kestrel">The Kestrel server options</param>
    /// <param name="name">The endpoint name</param>
    /// <param name="optionsName">The options name</param>
    /// <returns>The Kestrel server options for chaining</returns>
    public static KestrelServerOptions ListenAmqp(
        this KestrelServerOptions kestrel,
        string name,
        string? optionsName = null)
    {
        var opName = optionsName ?? Options.DefaultName;
        kestrel.Listen(new AmqpEndPoint(name, opName));
        return kestrel;
    }

    /// <summary>
    /// Convenience overload: configure options inline and bind in one call
    /// </summary>
    /// <param name="kestrel">The Kestrel server options</param>
    /// <param name="name">The endpoint name</param>
    /// <param name="configure">Configuration action</param>
    /// <returns>The Kestrel server options for chaining</returns>
    public static KestrelServerOptions ListenAmqp(
        this KestrelServerOptions kestrel,
        string name,
        Action<AmqpTransportOptions> configure)
    {
        var optionsName = $"amqp:{name}";
        kestrel.Listen(new AmqpEndPoint(name, optionsName));
        return kestrel;
    }

    /// <summary>
    /// One-stop helper: registers transport in DI with named options and adds the endpoint to Kestrel
    /// </summary>
    /// <param name="builder">The web host builder</param>
    /// <param name="endpointName">The endpoint name</param>
    /// <param name="configure">Configuration action</param>
    /// <returns>The web host builder for chaining</returns>
    public static IWebHostBuilder AddAmqpListener(
        this IWebHostBuilder builder,
        string endpointName,
        Action<AmqpTransportOptions> configure)
    {
        var optsName = $"amqp:{endpointName}";
        builder.ConfigureServices(services => services.AddAmqpTransport(configure, optsName));
        builder.ConfigureKestrel(k => k.ListenAmqp(endpointName, optsName));
        return builder;
    }
}
