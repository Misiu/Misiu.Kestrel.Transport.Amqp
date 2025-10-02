using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Misiu.Kestrel.Transport.Amqp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Misiu.Kestrel.Transport.Amqp.IntegrationTests;

/// <summary>
/// Integration tests specifically for the delayed result endpoint (/amqp/result/{correlationId})
/// These tests verify that the endpoint returns proper HTTP responses with various content types
/// </summary>
[Collection("RabbitMQ")]
[Trait("Category", "Integration")]
[Trait("RequiresDocker", "true")]
public class DelayedResultEndpointTests : IAsyncLifetime
{
    private readonly RabbitMqFixture _rabbitMq;
    private WebApplication? _gatewayServer;
    private WebApplication? _localApi;
    private IHost? _clientHost;
    private HttpClient? _httpClient;
    private string _gatewayBaseUrl = string.Empty;
    private string _localApiBaseUrl = string.Empty;

    public DelayedResultEndpointTests(RabbitMqFixture rabbitMq)
    {
        _rabbitMq = rabbitMq;
    }

    public async Task InitializeAsync()
    {
        // Purge RabbitMQ queues to ensure clean state
        try
        {
            var factory = new RabbitMQ.Client.ConnectionFactory
            {
                HostName = _rabbitMq.HostName,
                Port = _rabbitMq.Port,
                UserName = _rabbitMq.UserName,
                Password = _rabbitMq.Password
            };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            
            try { channel.QueuePurge("amqp.gateway.requests"); } catch { }
            try { channel.QueuePurge("amqp.gateway.responses"); } catch { }
        }
        catch
        {
            // Ignore errors - queues might not exist yet
        }
        
        // Create local API with additional test endpoints
        _localApi = CreateLocalApiWithExtraEndpoints();
        await _localApi.StartAsync().WaitAsync(TimeSpan.FromSeconds(10));
        
        var addresses = _localApi.Urls.ToList();
        _localApiBaseUrl = addresses.First();

        // Create and start the client host
        _clientHost = TestServerFactory.CreateBackgroundServiceClient(
            _rabbitMq.HostName,
            _rabbitMq.Port,
            _rabbitMq.UserName,
            _rabbitMq.Password,
            _localApiBaseUrl);

        await _clientHost.StartAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(500);

        // Create and start the gateway server with short timeout to trigger 202
        _gatewayServer = TestServerFactory.CreateGatewayServer(
            _rabbitMq.HostName,
            _rabbitMq.Port,
            _rabbitMq.UserName,
            _rabbitMq.Password,
            immediateTimeoutSeconds: 1);

        await _gatewayServer.StartAsync().WaitAsync(TimeSpan.FromSeconds(10));
        
        var gatewayAddresses = _gatewayServer.Urls.ToList();
        _gatewayBaseUrl = gatewayAddresses.First();

        _httpClient = new HttpClient { BaseAddress = new Uri(_gatewayBaseUrl) };
        await Task.Delay(500);
    }

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();
        
        if (_gatewayServer != null)
        {
            await _gatewayServer.StopAsync();
            await _gatewayServer.DisposeAsync();
        }
        
        if (_clientHost != null)
        {
            await _clientHost.StopAsync();
            _clientHost.Dispose();
        }
        
        if (_localApi != null)
        {
            await _localApi.StopAsync();
            await _localApi.DisposeAsync();
        }
    }

    [Fact]
    public async Task DelayedResult_WithJsonContent_ReturnsProperJsonResponse()
    {
        // Act - Request that takes longer than timeout
        var response = await _httpClient!.GetAsync("/api/json-slow");
        
        // Assert - Should get 202
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var correlationId = json.GetProperty("correlationId").GetString();
        var location = json.GetProperty("location").GetString();
        
        Assert.NotNull(correlationId);
        Assert.NotNull(location);

        // Wait for processing
        await Task.Delay(3000);

        // Retrieve result
        var resultResponse = await _httpClient.GetAsync(location);
        
        // Assert - Proper HTTP response
        Assert.Equal(HttpStatusCode.OK, resultResponse.StatusCode);
        Assert.Equal("application/json", resultResponse.Content.Headers.ContentType?.MediaType);
        
        // Verify custom headers
        Assert.True(resultResponse.Headers.Contains("X-Processing-Time-Ms"));
        Assert.True(resultResponse.Headers.Contains("X-Server-Started-At-Utc"));
        Assert.True(resultResponse.Headers.Contains("X-Server-Completed-At-Utc"));
        
        // Verify actual response body (not wrapped in JSON)
        var body = await resultResponse.Content.ReadAsStringAsync();
        var bodyJson = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal("JSON content", bodyJson.GetProperty("message").GetString());
    }

    [Fact]
    public async Task DelayedResult_WithTextContent_ReturnsProperTextResponse()
    {
        // Act
        var response = await _httpClient!.GetAsync("/api/text-slow");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var location = json.GetProperty("location").GetString();

        // Wait for processing
        await Task.Delay(3000);

        // Retrieve result
        var resultResponse = await _httpClient.GetAsync(location);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, resultResponse.StatusCode);
        Assert.Equal("text/plain", resultResponse.Content.Headers.ContentType?.MediaType);
        
        var body = await resultResponse.Content.ReadAsStringAsync();
        Assert.Equal("Plain text response", body);
    }

    [Fact]
    public async Task DelayedResult_WithBinaryContent_ReturnsProperBinaryResponse()
    {
        // Act
        var response = await _httpClient!.GetAsync("/api/binary-slow");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var location = json.GetProperty("location").GetString();

        // Wait for processing
        await Task.Delay(3000);

        // Retrieve result
        var resultResponse = await _httpClient.GetAsync(location);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, resultResponse.StatusCode);
        Assert.Equal("application/octet-stream", resultResponse.Content.Headers.ContentType?.MediaType);
        
        var body = await resultResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 }, body);
    }

    [Fact]
    public async Task DelayedResult_WithCustomHeaders_ReturnsAllHeaders()
    {
        // Act
        var response = await _httpClient!.GetAsync("/api/headers-slow");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var location = json.GetProperty("location").GetString();

        // Wait for processing
        await Task.Delay(3000);

        // Retrieve result
        var resultResponse = await _httpClient.GetAsync(location);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, resultResponse.StatusCode);
        
        // Verify custom headers from the original response
        Assert.True(resultResponse.Headers.Contains("X-Custom-1"));
        Assert.True(resultResponse.Headers.Contains("X-Custom-2"));
        Assert.Equal("value1", resultResponse.Headers.GetValues("X-Custom-1").First());
        Assert.Equal("value2", resultResponse.Headers.GetValues("X-Custom-2").First());
        
        // Verify processing metadata headers
        Assert.True(resultResponse.Headers.Contains("X-Processing-Time-Ms"));
        Assert.True(resultResponse.Headers.Contains("X-Server-Started-At-Utc"));
        Assert.True(resultResponse.Headers.Contains("X-Server-Completed-At-Utc"));
    }

    [Fact]
    public async Task DelayedResult_With404Status_Returns404()
    {
        // Act
        var response = await _httpClient!.GetAsync("/api/notfound-slow");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var location = json.GetProperty("location").GetString();

        // Wait for processing
        await Task.Delay(3000);

        // Retrieve result
        var resultResponse = await _httpClient.GetAsync(location);
        
        // Assert - Should return 404, not 200
        Assert.Equal(HttpStatusCode.NotFound, resultResponse.StatusCode);
    }

    [Fact]
    public async Task DelayedResult_WithNoContent_Returns204()
    {
        // Act
        var response = await _httpClient!.GetAsync("/api/nocontent-slow");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var location = json.GetProperty("location").GetString();

        // Wait for processing
        await Task.Delay(3000);

        // Retrieve result
        var resultResponse = await _httpClient.GetAsync(location);
        
        // Assert
        Assert.Equal(HttpStatusCode.NoContent, resultResponse.StatusCode);
        
        var body = await resultResponse.Content.ReadAsStringAsync();
        Assert.Empty(body);
    }

    [Fact]
    public async Task DelayedResult_WithNonexistentCorrelationId_Returns404()
    {
        // Act
        var fakeCorrelationId = Guid.NewGuid();
        var resultResponse = await _httpClient!.GetAsync($"/amqp/result/{fakeCorrelationId}");
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, resultResponse.StatusCode);
        
        var body = await resultResponse.Content.ReadAsStringAsync();
        Assert.Contains("not_found", body);
        Assert.Contains(fakeCorrelationId.ToString(), body);
    }

    private WebApplication CreateLocalApiWithExtraEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        var app = builder.Build();

        // JSON endpoint
        app.MapGet("/api/json-slow", async () =>
        {
            await Task.Delay(2000);
            return Results.Ok(new { message = "JSON content", timestamp = DateTimeOffset.UtcNow });
        });

        // Text endpoint
        app.MapGet("/api/text-slow", async () =>
        {
            await Task.Delay(2000);
            return Results.Text("Plain text response", "text/plain");
        });

        // Binary endpoint
        app.MapGet("/api/binary-slow", async () =>
        {
            await Task.Delay(2000);
            var bytes = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
            return Results.Bytes(bytes, "application/octet-stream");
        });

        // Custom headers endpoint
        app.MapGet("/api/headers-slow", async (HttpContext context) =>
        {
            await Task.Delay(2000);
            context.Response.Headers["X-Custom-1"] = "value1";
            context.Response.Headers["X-Custom-2"] = "value2";
            return Results.Ok(new { message = "With headers" });
        });

        // 404 endpoint
        app.MapGet("/api/notfound-slow", async () =>
        {
            await Task.Delay(2000);
            return Results.NotFound(new { error = "Not found" });
        });

        // No content endpoint
        app.MapGet("/api/nocontent-slow", async () =>
        {
            await Task.Delay(2000);
            return Results.NoContent();
        });

        return app;
    }
}
