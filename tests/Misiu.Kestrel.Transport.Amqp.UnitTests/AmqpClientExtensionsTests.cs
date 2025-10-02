using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Misiu.Kestrel.Transport.Amqp.UnitTests;

public class AmqpClientExtensionsTests
{
    [Fact]
    public void AddAmqpClient_WithConfigureAction_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAmqpClient(options =>
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
    public void AddAmqpClient_RegistersHttpClient()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAmqpClient(options => { });

        // Assert
        var provider = services.BuildServiceProvider();
        var httpClientFactory = provider.GetService<IHttpClientFactory>();
        httpClientFactory.Should().NotBeNull();
    }

    [Fact]
    public void AddAmqpClient_RegistersHostedService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAmqpClient(options => { });

        // Assert
        var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>();
        hostedServices.Should().Contain(s => s is AmqpClientConsumer);
    }

    [Fact]
    public void AddAmqpClient_WithConfiguration_BindsConfigurationToOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var configDict = new Dictionary<string, string?>
        {
            ["AmqpClient:HostName"] = "rabbitmq.example.com",
            ["AmqpClient:Port"] = "15672",
            ["AmqpClient:UserName"] = "admin",
            ["AmqpClient:Password"] = "secret",
            ["AmqpClient:RequestQueue"] = "custom.requests",
            ["AmqpClient:ResponseQueue"] = "custom.responses",
            ["AmqpClient:LocalApiBaseUrl"] = "http://localhost:8080",
            ["AmqpClient:PrefetchCount"] = "50"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act
        services.AddAmqpClient(configuration);

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AmqpTransportOptions>>().Value;
        options.HostName.Should().Be("rabbitmq.example.com");
        options.Port.Should().Be(15672);
        options.UserName.Should().Be("admin");
        options.Password.Should().Be("secret");
        options.RequestQueue.Should().Be("custom.requests");
        options.ResponseQueue.Should().Be("custom.responses");
        options.LocalApiBaseUrl.Should().Be("http://localhost:8080");
        options.PrefetchCount.Should().Be(50);
    }

    [Fact]
    public void AddAmqpClient_WithConfiguration_AndCustomSectionName_UsesCustomSection()
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
        services.AddAmqpClient(configuration, sectionName: "CustomSection");

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AmqpTransportOptions>>().Value;
        options.HostName.Should().Be("custom-rabbitmq");
        options.Port.Should().Be(5673);
    }

    [Fact]
    public void AddAmqpClient_WithEmptyConfiguration_UsesDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        services.AddAmqpClient(configuration);

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AmqpTransportOptions>>().Value;
        options.HostName.Should().Be("localhost");
        options.Port.Should().Be(5672);
    }

    [Fact]
    public void AddAmqpClient_ConfiguresAllOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAmqpClient(options =>
        {
            options.HostName = "rabbitmq.prod.com";
            options.Port = 5671;
            options.VirtualHost = "/vhost";
            options.UserName = "produser";
            options.Password = "prodpass";
            options.RequestQueue = "prod.requests";
            options.ResponseQueue = "prod.responses";
            options.Persistent = false;
            options.PrefetchCount = 50;
            options.LocalApiBaseUrl = "http://localhost:9000";
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
        options.PrefetchCount.Should().Be(50);
        options.LocalApiBaseUrl.Should().Be("http://localhost:9000");
    }

    [Fact]
    public void AddAmqpClient_WithConfigureAction_ReturnsServiceCollectionForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddAmqpClient(options => { });

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddAmqpClient_WithConfiguration_ReturnsServiceCollectionForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var result = services.AddAmqpClient(configuration);

        // Assert
        result.Should().BeSameAs(services);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("rabbitmq.example.com")]
    [InlineData("127.0.0.1")]
    public void AddAmqpClient_WithDifferentHostNames_ConfiguresCorrectly(string hostName)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAmqpClient(options =>
        {
            options.HostName = hostName;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AmqpTransportOptions>>().Value;
        options.HostName.Should().Be(hostName);
    }

    [Theory]
    [InlineData(5672)]
    [InlineData(5671)]
    [InlineData(15672)]
    public void AddAmqpClient_WithDifferentPorts_ConfiguresCorrectly(int port)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAmqpClient(options =>
        {
            options.Port = port;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AmqpTransportOptions>>().Value;
        options.Port.Should().Be(port);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public void AddAmqpClient_WithDifferentPrefetchCounts_ConfiguresCorrectly(ushort prefetchCount)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAmqpClient(options =>
        {
            options.PrefetchCount = prefetchCount;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AmqpTransportOptions>>().Value;
        options.PrefetchCount.Should().Be(prefetchCount);
    }
}
