using FluentAssertions;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Misiu.Kestrel.Transport.Amqp.UnitTests;

public class KestrelAmqpExtensionsTests
{
    [Fact]
    public void AddAmqpTransport_WithoutConfiguration_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Required dependency for AmqpConnectionListenerFactory

        // Act
        services.AddAmqpTransport();

        // Assert
        var provider = services.BuildServiceProvider();
        var factory = provider.GetServices<IConnectionListenerFactory>();
        factory.Should().NotBeEmpty();
        factory.Should().Contain(f => f is AmqpConnectionListenerFactory);
    }

    [Fact]
    public void AddAmqpTransport_WithConfigureAction_RegistersServicesAndConfiguresOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAmqpTransport(options =>
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
    public void AddAmqpTransport_WithNullConfiguration_RegistersServicesWithDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Required dependency for AmqpConnectionListenerFactory

        // Act
        services.AddAmqpTransport(configure: null);

        // Assert
        var provider = services.BuildServiceProvider();
        var factory = provider.GetServices<IConnectionListenerFactory>();
        factory.Should().NotBeEmpty();
    }

    [Fact]
    public void AddAmqpTransport_WithCustomOptionsName_UsesCustomName()
    {
        // Arrange
        var services = new ServiceCollection();
        const string customOptionsName = "CustomOptions";

        // Act
        services.AddAmqpTransport(options =>
        {
            options.HostName = "custom-host";
        }, customOptionsName);

        // Assert
        var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<AmqpTransportOptions>>();
        var options = optionsMonitor.Get(customOptionsName);
        options.HostName.Should().Be("custom-host");
    }

    [Fact]
    public void AddAmqpTransport_WithConfiguration_BindsConfigurationToOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var configDict = new Dictionary<string, string?>
        {
            ["AmqpTransport:HostName"] = "rabbitmq.example.com",
            ["AmqpTransport:Port"] = "15672",
            ["AmqpTransport:UserName"] = "admin",
            ["AmqpTransport:Password"] = "secret",
            ["AmqpTransport:RequestQueue"] = "custom.requests",
            ["AmqpTransport:ResponseQueue"] = "custom.responses"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act
        services.AddAmqpTransport(configuration);

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AmqpTransportOptions>>().Value;
        options.HostName.Should().Be("rabbitmq.example.com");
        options.Port.Should().Be(15672);
        options.UserName.Should().Be("admin");
        options.Password.Should().Be("secret");
        options.RequestQueue.Should().Be("custom.requests");
        options.ResponseQueue.Should().Be("custom.responses");
    }

    [Fact]
    public void AddAmqpTransport_WithConfiguration_AndCustomSectionName_UsesCustomSection()
    {
        // Arrange
        var services = new ServiceCollection();
        var configDict = new Dictionary<string, string?>
        {
            ["CustomSection:HostName"] = "custom-rabbitmq",
            ["CustomSection:Port"] = "5673"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act
        services.AddAmqpTransport(configuration, sectionName: "CustomSection");

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AmqpTransportOptions>>().Value;
        options.HostName.Should().Be("custom-rabbitmq");
        options.Port.Should().Be(5673);
    }

    [Fact]
    public void AddAmqpTransport_MultipleCalls_DoesNotDuplicateFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Required dependency for AmqpConnectionListenerFactory

        // Act
        services.AddAmqpTransport();
        services.AddAmqpTransport();

        // Assert
        var provider = services.BuildServiceProvider();
        var factories = provider.GetServices<IConnectionListenerFactory>()
            .Where(f => f is AmqpConnectionListenerFactory)
            .ToList();
        factories.Should().HaveCount(1);
    }

    [Fact]
    public void AddAmqpTransport_WithAllConfigurationOptions_BindsCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var configDict = new Dictionary<string, string?>
        {
            ["AmqpTransport:HostName"] = "rabbitmq.prod.com",
            ["AmqpTransport:Port"] = "5671",
            ["AmqpTransport:VirtualHost"] = "/vhost",
            ["AmqpTransport:UserName"] = "produser",
            ["AmqpTransport:Password"] = "prodpass",
            ["AmqpTransport:RequestQueue"] = "prod.requests",
            ["AmqpTransport:ResponseQueue"] = "prod.responses",
            ["AmqpTransport:Persistent"] = "false",
            ["AmqpTransport:PrefetchCount"] = "50",
            ["AmqpTransport:MaxRequestBodyBytes"] = "20000000",
            ["AmqpTransport:ImmediateTimeoutSeconds"] = "5",
            ["AmqpTransport:ResultTtlMinutes"] = "30",
            ["AmqpTransport:LocalApiBaseUrl"] = "http://localhost:8080",
            ["AmqpTransport:PathPrefixToRemove"] = "/proxy",
            ["AmqpTransport:PathPrefixToAdd"] = "/api"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act
        services.AddAmqpTransport(configuration);

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
        options.PrefetchCount.Should().Be(50);
        options.MaxRequestBodyBytes.Should().Be(20000000);
        options.ImmediateTimeoutSeconds.Should().Be(5);
        options.ResultTtlMinutes.Should().Be(30);
        options.LocalApiBaseUrl.Should().Be("http://localhost:8080");
        options.PathPrefixToRemove.Should().Be("/proxy");
        options.PathPrefixToAdd.Should().Be("/api");
    }

    [Fact]
    public void AddAmqpTransport_WithEmptyConfiguration_UsesDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        services.AddAmqpTransport(configuration);

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AmqpTransportOptions>>().Value;
        options.HostName.Should().Be("localhost");
        options.Port.Should().Be(5672);
    }

    [Fact]
    public void ListenAmqp_CreatesAmqpEndPoint()
    {
        // Arrange
        var kestrelOptions = new KestrelServerOptions();
        const string endpointName = "test-endpoint";

        // Act
        var result = kestrelOptions.ListenAmqp(endpointName);

        // Assert
        result.Should().BeSameAs(kestrelOptions);
        // Note: We can't easily verify the endpoint was added without internal access,
        // but we can verify the method returns the options for chaining
    }

    [Fact]
    public void ListenAmqp_WithCustomOptionsName_CreatesEndPointWithOptionsName()
    {
        // Arrange
        var kestrelOptions = new KestrelServerOptions();
        const string endpointName = "test-endpoint";
        const string optionsName = "custom-options";

        // Act
        var result = kestrelOptions.ListenAmqp(endpointName, optionsName);

        // Assert
        result.Should().BeSameAs(kestrelOptions);
    }

    [Fact]
    public void ListenAmqp_WithConfigureAction_ReturnsKestrelOptions()
    {
        // Arrange
        var kestrelOptions = new KestrelServerOptions();
        const string endpointName = "test-endpoint";

        // Act
        var result = kestrelOptions.ListenAmqp(endpointName, options =>
        {
            options.HostName = "custom-host";
        });

        // Assert
        result.Should().BeSameAs(kestrelOptions);
    }

    [Theory]
    [InlineData("endpoint1")]
    [InlineData("my-endpoint")]
    [InlineData("test_endpoint_123")]
    public void ListenAmqp_WithVariousEndpointNames_Succeeds(string endpointName)
    {
        // Arrange
        var kestrelOptions = new KestrelServerOptions();

        // Act
        var result = kestrelOptions.ListenAmqp(endpointName);

        // Assert
        result.Should().BeSameAs(kestrelOptions);
    }

    [Fact]
    public void AddAmqpTransport_ReturnsServiceCollectionForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddAmqpTransport();

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddAmqpTransport_WithConfiguration_ReturnsServiceCollectionForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var result = services.AddAmqpTransport(configuration);

        // Assert
        result.Should().BeSameAs(services);
    }
}
