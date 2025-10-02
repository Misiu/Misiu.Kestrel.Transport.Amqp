using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Misiu.Kestrel.Transport.Amqp.IntegrationTests.Infrastructure;

/// <summary>
/// Factory for creating test server instances
/// </summary>
public static class TestServerFactory
{
    /// <summary>
    /// Creates a gateway server for testing
    /// </summary>
    public static WebApplication CreateGatewayServer(
        string hostName, 
        int port, 
        string userName, 
        string password,
        int immediateTimeoutSeconds = 3,
        string? pathPrefixToRemove = null)
    {
        var builder = WebApplication.CreateBuilder();
        
        builder.WebHost.UseUrls("http://127.0.0.1:0"); // Random port
        
        builder.Services.AddAmqpGateway(options =>
        {
            options.HostName = hostName;
            options.Port = port;
            options.UserName = userName;
            options.Password = password;
            options.RequestQueue = "amqp.gateway.requests";
            options.ResponseQueue = "amqp.gateway.responses";
            options.ImmediateTimeoutSeconds = immediateTimeoutSeconds;
            options.ResultTtlMinutes = 15;
            options.PathPrefixToRemove = pathPrefixToRemove;
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        var app = builder.Build();

        app.MapAmqpResultEndpoint();
        app.UseAmqpGateway();

        return app;
    }

    /// <summary>
    /// Creates a client app using Transport approach for testing
    /// </summary>
    public static WebApplication CreateTransportClient(
        string hostName,
        int port,
        string userName,
        string password)
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddAmqpTransport(options =>
        {
            options.HostName = hostName;
            options.Port = port;
            options.UserName = userName;
            options.Password = password;
            options.RequestQueue = "amqp.gateway.requests";
            options.ResponseQueue = "amqp.gateway.responses";
        });

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.ListenAmqp("amqp-client");
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        var app = builder.Build();

        // Setup test endpoints
        SetupTestEndpoints(app);

        return app;
    }

    /// <summary>
    /// Creates a local HTTP API for BackgroundService client to forward to
    /// </summary>
    public static WebApplication CreateLocalApi()
    {
        var builder = WebApplication.CreateBuilder();
        
        builder.WebHost.UseUrls("http://127.0.0.1:0"); // Random port
        
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        var app = builder.Build();

        // Setup test endpoints
        SetupTestEndpoints(app);

        return app;
    }

    /// <summary>
    /// Creates a host with BackgroundService client
    /// </summary>
    public static IHost CreateBackgroundServiceClient(
        string hostName,
        int port,
        string userName,
        string password,
        string localApiBaseUrl)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddAmqpClient(options =>
        {
            options.HostName = hostName;
            options.Port = port;
            options.UserName = userName;
            options.Password = password;
            options.RequestQueue = "amqp.gateway.requests";
            options.ResponseQueue = "amqp.gateway.responses";
            options.LocalApiBaseUrl = localApiBaseUrl;
            options.PrefetchCount = 10;
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        return builder.Build();
    }

    private static void SetupTestEndpoints(WebApplication app)
    {
        app.MapGet("/", () => Results.Ok(new { message = "Hello from client!" }));

        app.MapGet("/api/data", () => Results.Ok(new
        {
            message = "Data from API",
            timestamp = DateTimeOffset.UtcNow
        }));

        app.MapPost("/api/echo", async (HttpRequest request) =>
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync();
            return Results.Ok(new
            {
                method = request.Method,
                path = request.Path.ToString(),
                receivedBody = body,
                timestamp = DateTimeOffset.UtcNow
            });
        });

        app.MapGet("/api/slow", async () =>
        {
            await Task.Delay(5000);
            return Results.Ok(new { message = "Slow operation completed", timestamp = DateTimeOffset.UtcNow });
        });

        app.MapGet("/api/medium", async () =>
        {
            await Task.Delay(1000);
            return Results.Ok(new { message = "Medium operation completed", timestamp = DateTimeOffset.UtcNow });
        });

        app.MapGet("/api/error", () =>
        {
            throw new InvalidOperationException("Simulated error");
        });

        app.MapGet("/api/headers", (HttpRequest request) =>
        {
            var headers = request.Headers.ToDictionary(h => h.Key, h => h.Value.ToArray());
            return Results.Ok(new
            {
                message = "Headers test",
                headers = headers,
                customHeader = request.Headers["X-Custom-Header"].FirstOrDefault()
            });
        });
    }
}
