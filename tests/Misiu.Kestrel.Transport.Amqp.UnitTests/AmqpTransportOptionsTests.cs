using FluentAssertions;

namespace Misiu.Kestrel.Transport.Amqp.UnitTests;

public class AmqpTransportOptionsTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var options = new AmqpTransportOptions();

        // Assert
        options.HostName.Should().Be("localhost");
        options.Port.Should().Be(5672);
        options.VirtualHost.Should().Be("/");
        options.UserName.Should().Be("guest");
        options.Password.Should().Be("guest");
        options.RequestQueue.Should().Be("amqp.gateway.requests");
        options.ResponseQueue.Should().Be("amqp.gateway.responses");
        options.Persistent.Should().BeTrue();
        options.PrefetchCount.Should().Be(32);
        options.MaxRequestBodyBytes.Should().Be(10_000_000);
        options.ImmediateTimeoutSeconds.Should().Be(3);
        options.ResultTtlMinutes.Should().Be(15);
        options.LocalApiBaseUrl.Should().Be("http://localhost:5000");
        options.PathPrefixToRemove.Should().BeNull();
        options.PathPrefixToAdd.Should().BeNull();
    }

    [Theory]
    [InlineData("rabbitmq.example.com")]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public void HostName_CanBeSet_WithVariousValues(string hostName)
    {
        // Arrange
        var options = new AmqpTransportOptions();

        // Act
        options.HostName = hostName;

        // Assert
        options.HostName.Should().Be(hostName);
    }

    [Theory]
    [InlineData(5672)]
    [InlineData(5671)]
    [InlineData(15672)]
    [InlineData(1)]
    [InlineData(65535)]
    public void Port_CanBeSet_WithValidPorts(int port)
    {
        // Arrange
        var options = new AmqpTransportOptions();

        // Act
        options.Port = port;

        // Assert
        options.Port.Should().Be(port);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/vhost1")]
    [InlineData("/test")]
    [InlineData("")]
    public void VirtualHost_CanBeSet_WithVariousValues(string virtualHost)
    {
        // Arrange
        var options = new AmqpTransportOptions();

        // Act
        options.VirtualHost = virtualHost;

        // Assert
        options.VirtualHost.Should().Be(virtualHost);
    }

    [Theory]
    [InlineData("guest")]
    [InlineData("admin")]
    [InlineData("user123")]
    [InlineData("")]
    public void UserName_CanBeSet_WithVariousValues(string userName)
    {
        // Arrange
        var options = new AmqpTransportOptions();

        // Act
        options.UserName = userName;

        // Assert
        options.UserName.Should().Be(userName);
    }

    [Theory]
    [InlineData("guest")]
    [InlineData("P@ssw0rd!")]
    [InlineData("")]
    [InlineData("very-long-password-with-special-chars-123!@#$%")]
    public void Password_CanBeSet_WithVariousValues(string password)
    {
        // Arrange
        var options = new AmqpTransportOptions();

        // Act
        options.Password = password;

        // Assert
        options.Password.Should().Be(password);
    }

    [Theory]
    [InlineData("amqp.gateway.requests")]
    [InlineData("custom.request.queue")]
    [InlineData("q1")]
    [InlineData("my-queue-name")]
    public void RequestQueue_CanBeSet_WithVariousValues(string queueName)
    {
        // Arrange
        var options = new AmqpTransportOptions();

        // Act
        options.RequestQueue = queueName;

        // Assert
        options.RequestQueue.Should().Be(queueName);
    }

    [Theory]
    [InlineData("amqp.gateway.responses")]
    [InlineData("custom.response.queue")]
    [InlineData("q2")]
    [InlineData("my-response-queue")]
    public void ResponseQueue_CanBeSet_WithVariousValues(string queueName)
    {
        // Arrange
        var options = new AmqpTransportOptions();

        // Act
        options.ResponseQueue = queueName;

        // Assert
        options.ResponseQueue.Should().Be(queueName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Persistent_CanBeSet_WithBooleanValues(bool persistent)
    {
        // Arrange
        var options = new AmqpTransportOptions();

        // Act
        options.Persistent = persistent;

        // Assert
        options.Persistent.Should().Be(persistent);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(32)]
    [InlineData(100)]
    [InlineData(1000)]
    public void PrefetchCount_CanBeSet_WithVariousValues(ushort prefetchCount)
    {
        // Arrange
        var options = new AmqpTransportOptions();

        // Act
        options.PrefetchCount = prefetchCount;

        // Assert
        options.PrefetchCount.Should().Be(prefetchCount);
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(10_000_000)]
    [InlineData(100_000_000)]
    [InlineData(1)]
    public void MaxRequestBodyBytes_CanBeSet_WithVariousValues(long maxBytes)
    {
        // Arrange
        var options = new AmqpTransportOptions();

        // Act
        options.MaxRequestBodyBytes = maxBytes;

        // Assert
        options.MaxRequestBodyBytes.Should().Be(maxBytes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(60)]
    public void ImmediateTimeoutSeconds_CanBeSet_WithVariousValues(int seconds)
    {
        // Arrange
        var options = new AmqpTransportOptions();

        // Act
        options.ImmediateTimeoutSeconds = seconds;

        // Assert
        options.ImmediateTimeoutSeconds.Should().Be(seconds);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(60)]
    [InlineData(1440)]
    public void ResultTtlMinutes_CanBeSet_WithVariousValues(int minutes)
    {
        // Arrange
        var options = new AmqpTransportOptions();

        // Act
        options.ResultTtlMinutes = minutes;

        // Assert
        options.ResultTtlMinutes.Should().Be(minutes);
    }

    [Theory]
    [InlineData("http://localhost:5000")]
    [InlineData("http://localhost:8080")]
    [InlineData("https://api.example.com")]
    [InlineData("http://127.0.0.1:3000")]
    public void LocalApiBaseUrl_CanBeSet_WithVariousUrls(string url)
    {
        // Arrange
        var options = new AmqpTransportOptions();

        // Act
        options.LocalApiBaseUrl = url;

        // Assert
        options.LocalApiBaseUrl.Should().Be(url);
    }

    [Theory]
    [InlineData("/proxy")]
    [InlineData("/api")]
    [InlineData("/gateway")]
    [InlineData("/v1")]
    [InlineData(null)]
    public void PathPrefixToRemove_CanBeSet_WithVariousValues(string? prefix)
    {
        // Arrange
        var options = new AmqpTransportOptions();

        // Act
        options.PathPrefixToRemove = prefix;

        // Assert
        options.PathPrefixToRemove.Should().Be(prefix);
    }

    [Theory]
    [InlineData("/api/v1")]
    [InlineData("/api")]
    [InlineData("/internal")]
    [InlineData(null)]
    public void PathPrefixToAdd_CanBeSet_WithVariousValues(string? prefix)
    {
        // Arrange
        var options = new AmqpTransportOptions();

        // Act
        options.PathPrefixToAdd = prefix;

        // Assert
        options.PathPrefixToAdd.Should().Be(prefix);
    }

    [Fact]
    public void AllProperties_CanBeSet_Simultaneously()
    {
        // Arrange
        var options = new AmqpTransportOptions();

        // Act
        options.HostName = "custom-host";
        options.Port = 15672;
        options.VirtualHost = "/custom";
        options.UserName = "customuser";
        options.Password = "custompass";
        options.RequestQueue = "custom.requests";
        options.ResponseQueue = "custom.responses";
        options.Persistent = false;
        options.PrefetchCount = 50;
        options.MaxRequestBodyBytes = 20_000_000;
        options.ImmediateTimeoutSeconds = 10;
        options.ResultTtlMinutes = 30;
        options.LocalApiBaseUrl = "http://localhost:9000";
        options.PathPrefixToRemove = "/remove";
        options.PathPrefixToAdd = "/add";

        // Assert
        options.HostName.Should().Be("custom-host");
        options.Port.Should().Be(15672);
        options.VirtualHost.Should().Be("/custom");
        options.UserName.Should().Be("customuser");
        options.Password.Should().Be("custompass");
        options.RequestQueue.Should().Be("custom.requests");
        options.ResponseQueue.Should().Be("custom.responses");
        options.Persistent.Should().BeFalse();
        options.PrefetchCount.Should().Be(50);
        options.MaxRequestBodyBytes.Should().Be(20_000_000);
        options.ImmediateTimeoutSeconds.Should().Be(10);
        options.ResultTtlMinutes.Should().Be(30);
        options.LocalApiBaseUrl.Should().Be("http://localhost:9000");
        options.PathPrefixToRemove.Should().Be("/remove");
        options.PathPrefixToAdd.Should().Be("/add");
    }
}
