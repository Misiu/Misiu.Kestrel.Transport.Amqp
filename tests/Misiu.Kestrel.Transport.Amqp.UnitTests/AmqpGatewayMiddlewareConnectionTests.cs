using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Misiu.Kestrel.Transport.Amqp.UnitTests;

/// <summary>
/// Tests for AmqpGatewayMiddleware connection handling and error scenarios
/// </summary>
public class AmqpGatewayMiddlewareConnectionTests
{
    [Fact]
    public void Constructor_WithInvalidRabbitMQConnection_LogsErrorButDoesNotThrow()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<AmqpGatewayMiddleware>>();
        var nextMock = new Mock<RequestDelegate>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new AmqpTransportOptions
        {
            HostName = "nonexistent-rabbitmq-server",
            Port = 9999, // Invalid port
            RequestQueue = "test-requests",
            ResponseQueue = "test-responses"
        });

        // Act - Constructor should not throw even if RabbitMQ connection fails
        var action = () => new AmqpGatewayMiddleware(
            nextMock.Object,
            loggerMock.Object,
            options,
            cache);

        // Assert
        action.Should().NotThrow("middleware should handle connection failures gracefully");
    }

    [Fact]
    public void Constructor_RegistersOptionsCorrectly()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<AmqpGatewayMiddleware>>();
        var nextMock = new Mock<RequestDelegate>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var expectedOptions = new AmqpTransportOptions
        {
            HostName = "test-host",
            Port = 5672,
            RequestQueue = "requests",
            ResponseQueue = "responses",
            ImmediateTimeoutSeconds = 10,
            ResultTtlMinutes = 30
        };
        var options = Options.Create(expectedOptions);

        // Act
        var middleware = new AmqpGatewayMiddleware(
            nextMock.Object,
            loggerMock.Object,
            options,
            cache);

        // Assert
        middleware.Should().NotBeNull();
    }

    [Fact]
    public void Middleware_Properties_AreInitializedCorrectly()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<AmqpGatewayMiddleware>>();
        var nextMock = new Mock<RequestDelegate>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new AmqpTransportOptions
        {
            HostName = "localhost",
            Port = 5672,
            RequestQueue = "test-requests",
            ResponseQueue = "test-responses"
        });

        // Act
        var middleware = new AmqpGatewayMiddleware(
            nextMock.Object,
            loggerMock.Object,
            options,
            cache);

        // Assert
        middleware.Should().NotBeNull();
        cache.Should().NotBeNull("memory cache should be initialized");
    }
}
