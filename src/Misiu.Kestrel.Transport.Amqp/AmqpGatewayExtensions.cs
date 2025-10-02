using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
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
    /// Adds the AMQP Gateway middleware to the application pipeline
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseAmqpGateway(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AmqpGatewayMiddleware>();
    }

    /// <summary>
    /// Maps an endpoint to retrieve results by correlation ID
    /// </summary>
    /// <param name="endpoints">The endpoint route builder</param>
    /// <returns>The endpoint route builder for chaining</returns>
    public static IEndpointRouteBuilder MapAmqpResultEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/amqp/result/{correlationId:guid}", (Guid correlationId, IMemoryCache cache) =>
        {
            var cacheKey = $"amqp:result:{correlationId:N}";
            if (cache.TryGetValue<HttpResponseEnvelope>(cacheKey, out var envelope) && envelope != null)
            {
                return Results.Json(new
                {
                    correlationId = envelope.CorrelationId,
                    statusCode = envelope.StatusCode,
                    processingMs = envelope.ProcessingMilliseconds,
                    serverStartedAt = envelope.ServerStartedAtUtc,
                    serverCompletedAt = envelope.ServerCompletedAtUtc,
                    body = envelope.Body != null ? System.Text.Encoding.UTF8.GetString(envelope.Body) : null,
                    headers = envelope.Headers
                });
            }

            return Results.NotFound(new
            {
                correlationId,
                status = "not_found",
                message = "Result not found. It may still be processing or has expired."
            });
        });

        return endpoints;
    }
}
