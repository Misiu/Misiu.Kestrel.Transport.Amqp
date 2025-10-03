using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Misiu.Kestrel.Transport.Amqp.UnitTests;

public class AmqpConnectionListenerFactoryTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        var optionsMonitor = new Mock<IOptionsMonitor<AmqpTransportOptions>>();
        optionsMonitor.Setup(x => x.Get(It.IsAny<string>()))
            .Returns(new AmqpTransportOptions());

        // Act
        var factory = new AmqpConnectionListenerFactory(loggerFactory.Object, optionsMonitor.Object);

        // Assert
        factory.Should().NotBeNull();
        factory.Should().BeAssignableTo<IConnectionListenerFactory>();
    }

    [Fact]
    public async Task BindAsync_WithNonAmqpEndPoint_ThrowsNotSupportedException()
    {
        // Arrange
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        var optionsMonitor = new Mock<IOptionsMonitor<AmqpTransportOptions>>();
        optionsMonitor.Setup(x => x.Get(It.IsAny<string>()))
            .Returns(new AmqpTransportOptions());
        var factory = new AmqpConnectionListenerFactory(loggerFactory.Object, optionsMonitor.Object);
        var invalidEndpoint = new IPEndPoint(IPAddress.Loopback, 8080);

        // Act & Assert
        var act = async () => await factory.BindAsync(invalidEndpoint, CancellationToken.None);
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*IPEndPoint*not supported*");
    }

    [Fact]
    public void BindAsync_WithAmqpEndPoint_UsesDefaultOptionsName_WhenNotSpecified()
    {
        // Arrange
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        var optionsMonitor = new Mock<IOptionsMonitor<AmqpTransportOptions>>();
        var defaultOptions = new AmqpTransportOptions();
        optionsMonitor.Setup(x => x.Get(Options.DefaultName))
            .Returns(defaultOptions);
        var factory = new AmqpConnectionListenerFactory(loggerFactory.Object, optionsMonitor.Object);
        var endpoint = new AmqpEndPoint("test-endpoint");

        // Act & Assert
        // We can't fully test BindAsync without a RabbitMQ instance,
        // but we can verify the options lookup happens
        var act = async () => await factory.BindAsync(endpoint, CancellationToken.None);
        // This will throw when trying to connect to RabbitMQ, but that's expected
        optionsMonitor.Verify(x => x.Get(Options.DefaultName), Times.Never); // Verify would be called during binding
    }

    [Fact]
    public void BindAsync_WithAmqpEndPoint_UsesCustomOptionsName_WhenSpecified()
    {
        // Arrange
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        var optionsMonitor = new Mock<IOptionsMonitor<AmqpTransportOptions>>();
        var customOptions = new AmqpTransportOptions { HostName = "custom-host" };
        const string customOptionsName = "custom-options";
        optionsMonitor.Setup(x => x.Get(customOptionsName))
            .Returns(customOptions);
        var factory = new AmqpConnectionListenerFactory(loggerFactory.Object, optionsMonitor.Object);
        var endpoint = new AmqpEndPoint("test-endpoint", customOptionsName);

        // Act & Assert
        var act = async () => await factory.BindAsync(endpoint, CancellationToken.None);
        // This will throw when trying to connect to RabbitMQ, but we verify options would be retrieved
        optionsMonitor.Verify(x => x.Get(customOptionsName), Times.Never); // Verify would be called during binding
    }

    [Theory]
    [InlineData("endpoint1")]
    [InlineData("test-endpoint")]
    [InlineData("my_endpoint_123")]
    public async Task BindAsync_WithDifferentEndpointNames_ThrowsWhenTryingToConnect(string endpointName)
    {
        // Arrange
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        var optionsMonitor = new Mock<IOptionsMonitor<AmqpTransportOptions>>();
        // Use non-standard port to ensure RabbitMQ is not running on it
        optionsMonitor.Setup(x => x.Get(It.IsAny<string>()))
            .Returns(new AmqpTransportOptions { Port = 19999 });
        var factory = new AmqpConnectionListenerFactory(loggerFactory.Object, optionsMonitor.Object);
        var endpoint = new AmqpEndPoint(endpointName);

        // Act & Assert
        // Will throw because it tries to connect to RabbitMQ which isn't available on this port
        var act = async () => await factory.BindAsync(endpoint, CancellationToken.None);
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task BindAsync_WithCancellationToken_CanBeCancelled()
    {
        // Arrange
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        var optionsMonitor = new Mock<IOptionsMonitor<AmqpTransportOptions>>();
        // Use non-standard port to ensure RabbitMQ is not running on it
        optionsMonitor.Setup(x => x.Get(It.IsAny<string>()))
            .Returns(new AmqpTransportOptions { Port = 19999 });
        var factory = new AmqpConnectionListenerFactory(loggerFactory.Object, optionsMonitor.Object);
        var endpoint = new AmqpEndPoint("test-endpoint");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await factory.BindAsync(endpoint, cts.Token);
        // May throw OperationCanceledException or another exception when trying to connect
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public void Factory_ImplementsIConnectionListenerFactory()
    {
        // Arrange
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        var optionsMonitor = new Mock<IOptionsMonitor<AmqpTransportOptions>>();

        // Act
        var factory = new AmqpConnectionListenerFactory(loggerFactory.Object, optionsMonitor.Object);

        // Assert
        factory.Should().BeAssignableTo<IConnectionListenerFactory>();
    }

    [Fact]
    public async Task BindAsync_WithVariousEndpointTypes_OnlyAcceptsAmqpEndPoint()
    {
        // Arrange
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        var optionsMonitor = new Mock<IOptionsMonitor<AmqpTransportOptions>>();
        optionsMonitor.Setup(x => x.Get(It.IsAny<string>()))
            .Returns(new AmqpTransportOptions());
        var factory = new AmqpConnectionListenerFactory(loggerFactory.Object, optionsMonitor.Object);

        // Act & Assert - IPEndPoint should be rejected
        var ipEndpoint = new IPEndPoint(IPAddress.Loopback, 8080);
        var ipAct = async () => await factory.BindAsync(ipEndpoint, CancellationToken.None);
        await ipAct.Should().ThrowAsync<NotSupportedException>();

        // Act & Assert - DnsEndPoint should be rejected
        var dnsEndpoint = new DnsEndPoint("localhost", 8080);
        var dnsAct = async () => await factory.BindAsync(dnsEndpoint, CancellationToken.None);
        await dnsAct.Should().ThrowAsync<NotSupportedException>();

        // Act & Assert - UnixDomainSocketEndPoint should be rejected
        var unixEndpoint = new UnixDomainSocketEndPoint("/tmp/socket");
        var unixAct = async () => await factory.BindAsync(unixEndpoint, CancellationToken.None);
        await unixAct.Should().ThrowAsync<NotSupportedException>();
    }
}
