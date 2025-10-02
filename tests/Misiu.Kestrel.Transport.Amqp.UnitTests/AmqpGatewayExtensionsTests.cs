using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Misiu.Kestrel.Transport.Amqp.UnitTests;

public class AmqpGatewayExtensionsTests
{
    [Fact]
    public void AddAmqpGateway_WithConfigureAction_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAmqpGateway(options =>
        {
            options.HostName = "custom-host";
            options.Port = 15672;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AmqpTransportOptions>>().Value;
        options.HostName.Should().Be("custom-host");
        options.Port.Should().Be(15672);
    }

    [Fact]
    public void AddAmqpGateway_RegistersMemoryCache()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAmqpGateway(options => { });

        // Assert
        var provider = services.BuildServiceProvider();
        var cache = provider.GetService<IMemoryCache>();
        cache.Should().NotBeNull();
    }

    [Fact]
    public void AddAmqpGateway_WithConfiguration_BindsConfigurationToOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var configDict = new Dictionary<string, string?>
        {
            ["AmqpGateway:HostName"] = "rabbitmq.example.com",
            ["AmqpGateway:Port"] = "15672",
            ["AmqpGateway:UserName"] = "admin",
            ["AmqpGateway:Password"] = "secret",
            ["AmqpGateway:RequestQueue"] = "custom.requests",
            ["AmqpGateway:ResponseQueue"] = "custom.responses",
            ["AmqpGateway:ImmediateTimeoutSeconds"] = "5",
            ["AmqpGateway:ResultTtlMinutes"] = "30",
            ["AmqpGateway:PathPrefixToRemove"] = "/proxy"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act
        services.AddAmqpGateway(configuration);

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AmqpTransportOptions>>().Value;
        options.HostName.Should().Be("rabbitmq.example.com");
        options.Port.Should().Be(15672);
        options.UserName.Should().Be("admin");
        options.Password.Should().Be("secret");
        options.RequestQueue.Should().Be("custom.requests");
        options.ResponseQueue.Should().Be("custom.responses");
        options.ImmediateTimeoutSeconds.Should().Be(5);
        options.ResultTtlMinutes.Should().Be(30);
        options.PathPrefixToRemove.Should().Be("/proxy");
    }

    [Fact]
    public void AddAmqpGateway_WithConfiguration_AndCustomSectionName_UsesCustomSection()
    {
        // Arrange
        var services = new ServiceCollection();
        var configDict = new Dictionary<string, string?>
        {
            ["CustomGateway:HostName"] = "custom-rabbitmq",
            ["CustomGateway:Port"] = "5673"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act
        services.AddAmqpGateway(configuration, sectionName: "CustomGateway");

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AmqpTransportOptions>>().Value;
        options.HostName.Should().Be("custom-rabbitmq");
        options.Port.Should().Be(5673);
    }

    [Fact]
    public void AddAmqpGateway_WithEmptyConfiguration_UsesDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        services.AddAmqpGateway(configuration);

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AmqpTransportOptions>>().Value;
        options.HostName.Should().Be("localhost");
        options.Port.Should().Be(5672);
        options.ImmediateTimeoutSeconds.Should().Be(3);
        options.ResultTtlMinutes.Should().Be(15);
    }

    [Fact]
    public void AddAmqpGateway_ConfiguresAllOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAmqpGateway(options =>
        {
            options.HostName = "rabbitmq.prod.com";
            options.Port = 5671;
            options.VirtualHost = "/vhost";
            options.UserName = "produser";
            options.Password = "prodpass";
            options.RequestQueue = "prod.requests";
            options.ResponseQueue = "prod.responses";
            options.Persistent = false;
            options.ImmediateTimeoutSeconds = 10;
            options.ResultTtlMinutes = 60;
            options.PathPrefixToRemove = "/api";
            options.PathPrefixToAdd = "/internal";
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AmqpTransportOptions>>().Value;
        options.HostName.Should().Be("rabbitmq.prod.com");
        options.Port.Should().Be(5671);
        options.VirtualHost.Should().Be("/vhost");
        options.UserName.Should().Be("produser");
        options.Password.Should().Be("prodpass");
        options.RequestQueue.Should().Be("prod.requests");
        options.ResponseQueue.Should().Be("prod.responses");
        options.Persistent.Should().BeFalse();
        options.ImmediateTimeoutSeconds.Should().Be(10);
        options.ResultTtlMinutes.Should().Be(60);
        options.PathPrefixToRemove.Should().Be("/api");
        options.PathPrefixToAdd.Should().Be("/internal");
    }

    [Fact]
    public void AddAmqpGateway_WithConfigureAction_ReturnsServiceCollectionForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddAmqpGateway(options => { });

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddAmqpGateway_WithConfiguration_ReturnsServiceCollectionForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var result = services.AddAmqpGateway(configuration);

        // Assert
        result.Should().BeSameAs(services);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void AddAmqpGateway_WithDifferentTimeouts_ConfiguresCorrectly(int timeoutSeconds)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAmqpGateway(options =>
        {
            options.ImmediateTimeoutSeconds = timeoutSeconds;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AmqpTransportOptions>>().Value;
        options.ImmediateTimeoutSeconds.Should().Be(timeoutSeconds);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    public void AddAmqpGateway_WithDifferentTtl_ConfiguresCorrectly(int ttlMinutes)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAmqpGateway(options =>
        {
            options.ResultTtlMinutes = ttlMinutes;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AmqpTransportOptions>>().Value;
        options.ResultTtlMinutes.Should().Be(ttlMinutes);
    }

    [Theory]
    [InlineData("/proxy")]
    [InlineData("/api")]
    [InlineData("/gateway")]
    [InlineData(null)]
    public void AddAmqpGateway_WithDifferentPathPrefixToRemove_ConfiguresCorrectly(string? prefix)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAmqpGateway(options =>
        {
            options.PathPrefixToRemove = prefix;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AmqpTransportOptions>>().Value;
        options.PathPrefixToRemove.Should().Be(prefix);
    }

    [Theory]
    [InlineData("/api/v1")]
    [InlineData("/internal")]
    [InlineData("/v2")]
    [InlineData(null)]
    public void AddAmqpGateway_WithDifferentPathPrefixToAdd_ConfiguresCorrectly(string? prefix)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAmqpGateway(options =>
        {
            options.PathPrefixToAdd = prefix;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AmqpTransportOptions>>().Value;
        options.PathPrefixToAdd.Should().Be(prefix);
    }

    [Fact]
    public void AddAmqpGateway_WithBothPathTransformations_ConfiguresCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAmqpGateway(options =>
        {
            options.PathPrefixToRemove = "/external";
            options.PathPrefixToAdd = "/internal";
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AmqpTransportOptions>>().Value;
        options.PathPrefixToRemove.Should().Be("/external");
        options.PathPrefixToAdd.Should().Be("/internal");
    }
}
