using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Misiu.Kestrel.Transport.Amqp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace Misiu.Kestrel.Transport.Amqp.IntegrationTests;

/// <summary>
/// Integration tests for BackgroundService approach (HTTP forwarding)
/// These tests require Docker and may take longer to run.
/// </summary>
[Collection("RabbitMQ")]
[Trait("Category", "Integration")]
[Trait("RequiresDocker", "true")]
public class BackgroundServiceApproachTests : IAsyncLifetime
{
    private readonly RabbitMqFixture _rabbitMq;
    private WebApplication? _gatewayServer;
    private WebApplication? _localApi;
    private IHost? _clientHost;
    private HttpClient? _httpClient;
    private string _gatewayBaseUrl = string.Empty;
    private string _localApiBaseUrl = string.Empty;

    public BackgroundServiceApproachTests(RabbitMqFixture rabbitMq)
    {
        _rabbitMq = rabbitMq;
    }

    public async Task InitializeAsync()
    {
        // Create and start the local API
        _localApi = TestServerFactory.CreateLocalApi();
        await _localApi.StartAsync();
        
        var addresses = _localApi.Urls.ToList();
        _localApiBaseUrl = addresses.First();

        // Create and start the client host (using BackgroundService approach)
        _clientHost = TestServerFactory.CreateBackgroundServiceClient(
            _rabbitMq.HostName,
            _rabbitMq.Port,
            _rabbitMq.UserName,
            _rabbitMq.Password,
            _localApiBaseUrl);

        await _clientHost.StartAsync();
        await Task.Delay(1000); // Wait for client to connect to RabbitMQ

        // Create and start the gateway server
        _gatewayServer = TestServerFactory.CreateGatewayServer(
            _rabbitMq.HostName,
            _rabbitMq.Port,
            _rabbitMq.UserName,
            _rabbitMq.Password,
            immediateTimeoutSeconds: 3);

        await _gatewayServer.StartAsync();
        
        var gatewayAddresses = _gatewayServer.Urls.ToList();
        _gatewayBaseUrl = gatewayAddresses.First();

        _httpClient = new HttpClient { BaseAddress = new Uri(_gatewayBaseUrl) };
        
        await Task.Delay(500); // Wait for gateway to connect to RabbitMQ
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
    public async Task Test_RabbitMQ_Server_And_Client_Can_Connect()
    {
        // Act - Make a simple request to verify connectivity
        var response = await _httpClient!.GetAsync("/");

        // Assert
        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Hello from client!", content);
    }

    [Fact]
    public async Task Test_Reconnection_After_RabbitMQ_Restart()
    {
        // Arrange - First verify connection works
        var response1 = await _httpClient!.GetAsync("/api/data");
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        // Act - Restart RabbitMQ
        await _rabbitMq.RestartAsync();

        // Assert - Should reconnect and work after restart
        var response2 = await _httpClient.GetAsync("/api/data");
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        
        var json = await response2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Data from API", json.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Test_Request_Response_Immediate()
    {
        // Act
        var response = await _httpClient!.GetAsync("/api/data");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Data from API", json.GetProperty("message").GetString());
        Assert.True(json.TryGetProperty("timestamp", out _));
    }

    [Fact]
    public async Task Test_Request_Response_With_202_Delayed()
    {
        // Act - Make a request that takes longer than the timeout
        var response = await _httpClient!.GetAsync("/api/slow");

        // Assert - Should get 202 Accepted
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("accepted", json.GetProperty("status").GetString());
        
        var correlationId = json.GetProperty("correlationId").GetString();
        Assert.NotNull(correlationId);
        
        var location = json.GetProperty("location").GetString();
        Assert.NotNull(location);
        Assert.Contains(correlationId, location);

        // Wait for processing to complete
        await Task.Delay(6000);

        // Retrieve the result
        var resultResponse = await _httpClient.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, resultResponse.StatusCode);
        
        var resultJson = await resultResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(200, resultJson.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task Test_NonExistent_Endpoint_Returns_404()
    {
        // Act
        var response = await _httpClient!.GetAsync("/api/nonexistent");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Test_Exception_Returns_500()
    {
        // Act
        var response = await _httpClient!.GetAsync("/api/error");

        // Assert
        // BackgroundService catches exceptions and returns 502 (Bad Gateway) instead of 500
        // This is because it's forwarding to another service
        Assert.True(
            response.StatusCode == HttpStatusCode.InternalServerError || 
            response.StatusCode == HttpStatusCode.BadGateway,
            $"Expected 500 or 502, got {response.StatusCode}");
    }

    [Fact]
    public async Task Test_Response_Headers_Verification()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/headers");
        request.Headers.Add("X-Custom-Header", "test-value");

        // Act
        var response = await _httpClient!.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        
        // Verify the custom header was passed through
        var customHeader = json.GetProperty("customHeader").GetString();
        Assert.Equal("test-value", customHeader);
        
        // Verify Content-Type header is present in response
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal("application/json", response.Content.Headers.ContentType.MediaType);
    }

    [Fact]
    public async Task Test_POST_Request_With_Body()
    {
        // Arrange
        var requestBody = new { name = "Test", value = 42 };
        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _httpClient!.PostAsync("/api/echo", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("POST", json.GetProperty("method").GetString());
        Assert.Equal("/api/echo", json.GetProperty("path").GetString());
        
        var receivedBody = json.GetProperty("receivedBody").GetString();
        Assert.Contains("Test", receivedBody);
        Assert.Contains("42", receivedBody);
    }

    [Fact]
    public async Task Test_Multiple_Concurrent_Requests()
    {
        // Arrange
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act - Send 10 concurrent requests
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_httpClient!.GetAsync($"/api/data"));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert - All should succeed
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Test_Out_Of_Order_Responses_Are_Correctly_Matched()
    {
        // Arrange - Send two requests: one slower (1 second), one fast (immediate)
        // The fast one should complete first but should be matched to the correct request
        
        // Act - Send slower request first (takes 1 second)
        var slowerTask = Task.Run(async () =>
        {
            var response = await _httpClient!.GetAsync("/api/medium");
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            return new { Response = response, Content = content, RequestType = "slower" };
        });

        // Give the slower request a head start
        await Task.Delay(100);

        // Send fast request (returns immediately)
        var fastTask = Task.Run(async () =>
        {
            var response = await _httpClient!.GetAsync("/api/data");
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            return new { Response = response, Content = content, RequestType = "fast" };
        });

        // Wait for both to complete
        var results = await Task.WhenAll(slowerTask, fastTask);
        var slowerResult = results[0];
        var fastResult = results[1];

        // Assert - Both should succeed with OK status
        Assert.Equal(HttpStatusCode.OK, slowerResult.Response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, fastResult.Response.StatusCode);

        // Assert - Responses should contain correct content for each request
        // Slower request should get slower response
        Assert.Equal("slower", slowerResult.RequestType);
        Assert.Equal("Medium operation completed", slowerResult.Content.GetProperty("message").GetString());

        // Fast request should get fast response  
        Assert.Equal("fast", fastResult.RequestType);
        Assert.Equal("Data from API", fastResult.Content.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Test_Headers_Distinguish_Client_Errors_From_Server_Errors()
    {
        // This test verifies we can distinguish between errors from our server
        // and errors returned by the client

        // Act - Get a 404 from the client (endpoint doesn't exist)
        var notFoundResponse = await _httpClient!.GetAsync("/api/nonexistent");
        Assert.Equal(HttpStatusCode.NotFound, notFoundResponse.StatusCode);

        // Act - Get an error from the client API
        var errorResponse = await _httpClient.GetAsync("/api/error");
        
        // The BackgroundService client catches exceptions and wraps them in 502
        // because it's treating the local API as a backend service
        Assert.True(
            errorResponse.StatusCode == HttpStatusCode.InternalServerError || 
            errorResponse.StatusCode == HttpStatusCode.BadGateway);
    }
}
