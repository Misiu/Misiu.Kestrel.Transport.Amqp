using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Misiu.Kestrel.Transport.Amqp.IntegrationTests.Infrastructure;

namespace Misiu.Kestrel.Transport.Amqp.IntegrationTests;

/// <summary>
/// Integration tests for Transport approach (AMQP as Kestrel transport)
/// These tests require Docker and may take longer to run.
/// Each test creates fresh client/gateway instances for isolation.
/// </summary>
[Collection("RabbitMQ")]
[Trait("Category", "Integration")]
[Trait("RequiresDocker", "true")]
public class TransportApproachTests
{
    private readonly RabbitMqFixture _rabbitMq;

    public TransportApproachTests(RabbitMqFixture rabbitMq)
    {
        _rabbitMq = rabbitMq;
    }

    private async Task<(WebApplication clientApp, WebApplication gatewayServer, HttpClient httpClient)> SetupTestAsync(int timeoutSeconds = 10)
    {
        // Use unique queue names for each test to ensure complete isolation
        var testId = Guid.NewGuid().ToString().Substring(0, 8);
        var requestQueue = $"amqp.gateway.requests.{testId}";
        var responseQueue = $"amqp.gateway.responses.{testId}";

        // Create and start the client app (using Transport approach)  
        var clientApp = TestServerFactory.CreateTransportClient(
            _rabbitMq.HostName,
            _rabbitMq.Port,
            _rabbitMq.UserName,
            _rabbitMq.Password,
            requestQueue,
            responseQueue);

        await clientApp.StartAsync();
        await Task.Delay(1500); // Wait for client to connect to RabbitMQ and start consuming

        // Create and start the gateway server
        var gatewayServer = TestServerFactory.CreateGatewayServer(
            _rabbitMq.HostName,
            _rabbitMq.Port,
            _rabbitMq.UserName,
            _rabbitMq.Password,
            immediateTimeoutSeconds: timeoutSeconds,
            requestQueue: requestQueue,
            responseQueue: responseQueue);

        await gatewayServer.StartAsync();

        // Get the actual URL the gateway is listening on
        var addresses = gatewayServer.Urls.ToList();
        var gatewayBaseUrl = addresses.First();

        var httpClient = new HttpClient { BaseAddress = new Uri(gatewayBaseUrl) };

        await Task.Delay(1000); // Wait for gateway to connect to RabbitMQ

        return (clientApp, gatewayServer, httpClient);
    }

    private async Task TeardownTestAsync(WebApplication? clientApp, WebApplication? gatewayServer, HttpClient? httpClient)
    {
        httpClient?.Dispose();

        // Wait a bit to ensure any in-flight requests are fully processed
        await Task.Delay(500);

        if (gatewayServer != null)
        {
            await gatewayServer.StopAsync();
            await gatewayServer.DisposeAsync();
        }

        if (clientApp != null)
        {
            await clientApp.StopAsync();
            await clientApp.DisposeAsync();
        }

        // Wait to ensure RabbitMQ consumer is fully cancelled and connections are closed
        // Using unique queue names per test, so no need to purge
        await Task.Delay(500);
    }

    [Fact]
    public async Task Test_RabbitMQ_Server_And_Client_Can_Connect()
    {
        // Arrange
        var (clientApp, gatewayServer, httpClient) = await SetupTestAsync();

        try
        {
            // Act - Make a simple request to verify connectivity
            var response = await httpClient.GetAsync("/");

            // Assert
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("Hello from client!", content);
        }
        finally
        {
            await TeardownTestAsync(clientApp, gatewayServer, httpClient);
        }
    }

    [Fact]
    public async Task Test_Reconnection_After_RabbitMQ_Restart()
    {
        // Arrange
        var (clientApp, gatewayServer, httpClient) = await SetupTestAsync();

        try
        {
            // First verify connection works
            var response1 = await httpClient.GetAsync("/api/data");
            Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

            // Act - Restart RabbitMQ
            await _rabbitMq.RestartAsync();

            // Assert - Should reconnect and work after restart
            var response2 = await httpClient.GetAsync("/api/data");
            Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

            var json = await response2.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Data from API", json.GetProperty("message").GetString());
        }
        finally
        {
            await TeardownTestAsync(clientApp, gatewayServer, httpClient);
        }
    }

    [Fact]
    public async Task Test_Request_Response_Immediate()
    {
        // Arrange
        var (clientApp, gatewayServer, httpClient) = await SetupTestAsync();

        try
        {
            // Act
            var response = await httpClient.GetAsync("/api/data");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Data from API", json.GetProperty("message").GetString());
            Assert.True(json.TryGetProperty("timestamp", out _));
        }
        finally
        {
            await TeardownTestAsync(clientApp, gatewayServer, httpClient);
        }
    }

    [Fact]
    public async Task Test_Response_DoesNotContainHopByHopHeaders()
    {
        // Arrange
        var (clientApp, gatewayServer, httpClient) = await SetupTestAsync();

        try
        {
            // Act
            var response = await httpClient.GetAsync("/api/data");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Verify hop-by-hop headers are not present in the response
            Assert.False(response.Headers.Contains("Transfer-Encoding"), "Transfer-Encoding header should be filtered out");
            Assert.False(response.Headers.Contains("Connection"), "Connection header should be filtered out");
            Assert.False(response.Headers.Contains("Keep-Alive"), "Keep-Alive header should be filtered out");
            Assert.False(response.Headers.Contains("Upgrade"), "Upgrade header should be filtered out");
            Assert.False(response.Headers.Contains("Proxy-Connection"), "Proxy-Connection header should be filtered out");

            // Verify body is properly decoded (not chunked)
            var content = await response.Content.ReadAsStringAsync();
            Assert.False(content.Contains("\r\n"), "Response body should not contain CRLF from chunked encoding");

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Data from API", json.GetProperty("message").GetString());
        }
        finally
        {
            await TeardownTestAsync(clientApp, gatewayServer, httpClient);
        }
    }

    [Fact]
    public async Task Test_Request_Response_With_202_Delayed()
    {
        // Arrange - Use short timeout so slow request triggers 202
        var (clientApp, gatewayServer, httpClient) = await SetupTestAsync(timeoutSeconds: 3);

        try
        {
            // Act - Make a request that takes longer than the timeout
            var response = await httpClient.GetAsync("/api/slow");

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
            var resultResponse = await httpClient.GetAsync(location);
            Assert.Equal(HttpStatusCode.OK, resultResponse.StatusCode);

            // Verify custom headers are present
            Assert.True(resultResponse.Headers.Contains("X-Processing-Time-Ms"));
            Assert.True(resultResponse.Headers.Contains("X-Server-Started-At-Utc"));
            Assert.True(resultResponse.Headers.Contains("X-Server-Completed-At-Utc"));

            // Verify we get the actual response body, not JSON wrapper
            var resultBody = await resultResponse.Content.ReadAsStringAsync();
            var resultJson = JsonSerializer.Deserialize<JsonElement>(resultBody);
            Assert.Equal("Slow operation completed", resultJson.GetProperty("message").GetString());
        }
        finally
        {
            await TeardownTestAsync(clientApp, gatewayServer, httpClient);
        }
    }

    [Fact]
    public async Task Test_NonExistent_Endpoint_Returns_404()
    {
        // Arrange
        var (clientApp, gatewayServer, httpClient) = await SetupTestAsync();

        try
        {
            // Act
            var response = await httpClient.GetAsync("/api/nonexistent");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await TeardownTestAsync(clientApp, gatewayServer, httpClient);
        }
    }

    [Fact]
    public async Task Test_Exception_Returns_500()
    {
        // Arrange
        var (clientApp, gatewayServer, httpClient) = await SetupTestAsync();

        try
        {
            // Act
            var response = await httpClient.GetAsync("/api/error");

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        finally
        {
            await TeardownTestAsync(clientApp, gatewayServer, httpClient);
        }
    }

    [Fact]
    public async Task Test_Response_Headers_Verification()
    {
        // Arrange
        var (clientApp, gatewayServer, httpClient) = await SetupTestAsync();

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/headers");
            request.Headers.Add("X-Custom-Header", "test-value");

            // Act
            var response = await httpClient.SendAsync(request);

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
        finally
        {
            await TeardownTestAsync(clientApp, gatewayServer, httpClient);
        }
    }

    [Fact]
    public async Task Test_POST_Request_With_Body()
    {
        // Arrange
        var (clientApp, gatewayServer, httpClient) = await SetupTestAsync();

        try
        {
            var requestBody = new { name = "Test", value = 42 };
            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await httpClient.PostAsync("/api/echo", content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("POST", json.GetProperty("method").GetString());
            Assert.Equal("/api/echo", json.GetProperty("path").GetString());

            var receivedBody = json.GetProperty("receivedBody").GetString();
            Assert.Contains("Test", receivedBody);
            Assert.Contains("42", receivedBody);
        }
        finally
        {
            await TeardownTestAsync(clientApp, gatewayServer, httpClient);
        }
    }

    [Fact]
    public async Task Test_Multiple_Concurrent_Requests()
    {
        // Arrange
        var (clientApp, gatewayServer, httpClient) = await SetupTestAsync();

        try
        {
            var tasks = new List<Task<HttpResponseMessage>>();

            // Act - Send 10 concurrent requests
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(httpClient.GetAsync($"/api/data"));
            }

            var responses = await Task.WhenAll(tasks);

            // Assert - All should succeed
            foreach (var response in responses)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }
        finally
        {
            await TeardownTestAsync(clientApp, gatewayServer, httpClient);
        }
    }

    [Fact]
    public async Task Test_Out_Of_Order_Responses_Are_Correctly_Matched()
    {
        // Arrange
        var (clientApp, gatewayServer, httpClient) = await SetupTestAsync();

        try
        {
            // Send two requests: one slower (1 second), one fast (immediate)
            // The fast one should complete first but should be matched to the correct request

            // Act - Send slower request first (takes 1 second)
            var slowerTask = Task.Run(async () =>
            {
                var response = await httpClient.GetAsync("/api/medium");
                var content = await response.Content.ReadFromJsonAsync<JsonElement>();
                return new { Response = response, Content = content, RequestType = "slower" };
            });

            // Give the slower request a head start
            await Task.Delay(100);

            // Send fast request (returns immediately)
            var fastTask = Task.Run(async () =>
            {
                var response = await httpClient.GetAsync("/api/data");
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
        finally
        {
            await TeardownTestAsync(clientApp, gatewayServer, httpClient);
        }
    }
}
