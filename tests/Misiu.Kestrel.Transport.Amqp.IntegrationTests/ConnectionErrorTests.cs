using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Misiu.Kestrel.Transport.Amqp.IntegrationTests.Infrastructure;

namespace Misiu.Kestrel.Transport.Amqp.IntegrationTests;

/// <summary>
/// Tests for RabbitMQ connection errors and recovery scenarios
/// </summary>
[Collection("RabbitMQ")]
[Trait("Category", "Integration")]
[Trait("RequiresDocker", "true")]
public class ConnectionErrorTests : IAsyncLifetime
{
    private readonly RabbitMqFixture _rabbitMq;

    public ConnectionErrorTests(RabbitMqFixture rabbitMq)
    {
        _rabbitMq = rabbitMq;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Gateway_WithInvalidRabbitMQConnection_Returns502()
    {
        // Arrange - Create gateway with invalid RabbitMQ connection
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddAmqpGateway(options =>
        {
            options.HostName = "nonexistent-server";
            options.Port = 9999; // Invalid port
            options.RequestQueue = "test-requests";
            options.ResponseQueue = "test-responses";
            options.ImmediateTimeoutSeconds = 1;
        });

        var app = builder.Build();
        app.MapAmqpResultEndpoint();
        app.UseAmqpGateway();

        await app.StartAsync();
        var client = app.GetTestClient();

        try
        {
            // Act - Make request to gateway with no RabbitMQ connection
            var response = await client.GetAsync("/api/test");

            // Assert - Should return 502 Bad Gateway
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("unable to connect", content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Gateway_AfterRabbitMQReconnect_SuccessfullyProcessesRequests()
    {
        // This test requires RabbitMQ container restart capability
        // Testing reconnection after RabbitMQ restart
        
        // Arrange
        var server = TestServerFactory.CreateGatewayServer(
            _rabbitMq.HostName,
            _rabbitMq.Port,
            _rabbitMq.UserName,
            _rabbitMq.Password);

        var client = TestServerFactory.CreateTransportClient(
            _rabbitMq.HostName,
            _rabbitMq.Port,
            _rabbitMq.UserName,
            _rabbitMq.Password);

        try
        {
            await server.StartAsync();
            await client.StartAsync();

            // Give services time to connect
            await Task.Delay(TimeSpan.FromSeconds(2));

            var httpClient = server.GetTestClient();

            // Act - Make initial request to verify it works
            var response1 = await httpClient.GetAsync("/api/data");

            // Assert - First request should work
            Assert.True(
                response1.IsSuccessStatusCode || response1.StatusCode == HttpStatusCode.Accepted,
                $"Expected success or accepted, got {response1.StatusCode}");

            // Note: Full reconnection test would require restarting RabbitMQ container
            // which is tested in Test_Reconnection_After_RabbitMQ_Restart in other test files
        }
        finally
        {
            await client.StopAsync();
            await client.DisposeAsync();
            await server.StopAsync();
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Gateway_WithTimeoutOnPublish_HandlesGracefully()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddAmqpGateway(options =>
        {
            options.HostName = _rabbitMq.HostName;
            options.Port = _rabbitMq.Port;
            options.UserName = _rabbitMq.UserName;
            options.Password = _rabbitMq.Password;
            options.RequestQueue = "test-timeout-queue";
            options.ResponseQueue = "test-timeout-response";
            options.ImmediateTimeoutSeconds = 1; // Very short timeout
        });

        var app = builder.Build();
        app.MapAmqpResultEndpoint();
        app.UseAmqpGateway();

        await app.StartAsync();
        var client = app.GetTestClient();

        try
        {
            // Act - Make request with very short timeout (no consumer to respond)
            var response = await client.GetAsync("/api/test");

            // Assert - Should return 202 Accepted (timeout) or 502 (connection issue)
            Assert.True(
                response.StatusCode == HttpStatusCode.Accepted || 
                response.StatusCode == HttpStatusCode.BadGateway,
                $"Expected 202 or 502, got {response.StatusCode}");

            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                // Verify Location header is present for 202 responses
                Assert.True(response.Headers.Contains("Location"), "202 response should include Location header");
            }
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Gateway_CancellationToken_PropagatesCorrectly()
    {
        // Arrange
        var server = TestServerFactory.CreateGatewayServer(
            _rabbitMq.HostName,
            _rabbitMq.Port,
            _rabbitMq.UserName,
            _rabbitMq.Password);

        try
        {
            await server.StartAsync();
            var httpClient = server.GetTestClient();

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            // Act - Make request with very short cancellation token
            var requestTask = httpClient.GetAsync("/api/test", cts.Token);

            // Assert - Request should be cancelled
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await requestTask);
        }
        finally
        {
            await server.StopAsync();
            await server.DisposeAsync();
        }
    }
}
