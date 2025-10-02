using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Misiu.Kestrel.Transport.Amqp;

/// <summary>
/// Extension methods for configuring AMQP Gateway
/// </summary>
public static class AmqpGatewayExtensions
{
    /// <summary>
    /// Adds AMQP Gateway services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Configuration action for AMQP transport options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAmqpGateway(
        this IServiceCollection services,
        Action<AmqpTransportOptions> configure)
    {
        services.Configure(configure);
        services.AddMemoryCache();
        return services;
    }

    /// <summary>
    /// Adds AMQP Gateway services to the service collection using configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration section</param>
    /// <param name="sectionName">The configuration section name (default: "AmqpGateway")</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAmqpGateway(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "AmqpGateway")
    {
        services.Configure<AmqpTransportOptions>(configuration.GetSection(sectionName));
        services.AddMemoryCache();
        return services;
    }

    /// <summary>
    /// Adds the AMQP Gateway middleware to the application pipeline
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseAmqpGateway(this IApplicationBuilder app)
    {
        // Only forward requests that are not targeting the local result endpoint
        app.UseWhen(
            ctx => !ctx.Request.Path.StartsWithSegments("/amqp/result", out _),
            branch => branch.UseMiddleware<AmqpGatewayMiddleware>());

        return app;
    }

    /// <summary>
    /// Maps an endpoint to retrieve results by correlation ID
    /// </summary>
    /// <param name="endpoints">The endpoint route builder</param>
    /// <returns>The endpoint route builder for chaining</returns>
    public static IEndpointRouteBuilder MapAmqpResultEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/amqp/result/{correlationId:guid}", async (Guid correlationId, IMemoryCache cache, HttpContext context) =>
        {
            var cacheKey = $"amqp:result:{correlationId:N}";
            if (cache.TryGetValue<HttpResponseEnvelope>(cacheKey, out var envelope) && envelope != null)
            {
                // Set status code
                context.Response.StatusCode = envelope.StatusCode;
                
                // Add custom headers for processing metadata
                context.Response.Headers["X-Processing-Time-Ms"] = envelope.ProcessingMilliseconds.ToString();
                context.Response.Headers["X-Server-Started-At-Utc"] = envelope.ServerStartedAtUtc.ToString("O");
                context.Response.Headers["X-Server-Completed-At-Utc"] = envelope.ServerCompletedAtUtc.ToString("O");
                
                // Add original headers
                foreach (var header in envelope.Headers)
                {
                    context.Response.Headers[header.Key] = header.Value;
                }
                
                // Write body if present
                if (envelope.Body != null && envelope.Body.Length > 0)
                {
                    await context.Response.Body.WriteAsync(envelope.Body);
                }
            }
            else
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsJsonAsync(new
                {
                    correlationId,
                    status = "not_found",
                    message = "Result not found. It may still be processing or has expired."
                });
            }
        });

        return endpoints;
    }
}
