using Misiu.Kestrel.Transport.Amqp.IntegrationTests.Infrastructure;

namespace Misiu.Kestrel.Transport.Amqp.IntegrationTests;

/// <summary>
/// Basic smoke tests to verify test infrastructure works
/// </summary>
[Collection("RabbitMQ")]
public class BasicSmokeTests
{
    private readonly RabbitMqFixture _rabbitMq;

    public BasicSmokeTests(RabbitMqFixture rabbitMq)
    {
        _rabbitMq = rabbitMq;
    }

    [Fact]
    public void Test_RabbitMQ_Container_Started()
    {
        // Arrange & Act & Assert
        Assert.NotNull(_rabbitMq);
        Assert.True(_rabbitMq.Port > 0);
        Assert.Equal("localhost", _rabbitMq.HostName);
        Assert.Equal("guest", _rabbitMq.UserName);
        Assert.NotEmpty(_rabbitMq.ConnectionString);
    }

    [Fact]
    public async Task Test_Can_Create_Gateway_Server()
    {
        // Arrange
        var server = TestServerFactory.CreateGatewayServer(
            _rabbitMq.HostName,
            _rabbitMq.Port,
            _rabbitMq.UserName,
            _rabbitMq.Password);

        try
        {
            // Act
            await server.StartAsync();
            
            // Assert
            Assert.NotNull(server);
            Assert.NotEmpty(server.Urls);
        }
        finally
        {
            // Cleanup
            await server.StopAsync();
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Test_Can_Create_Local_API()
    {
        // Arrange
        var api = TestServerFactory.CreateLocalApi();

        try
        {
            // Act
            await api.StartAsync();
            
            // Assert
            Assert.NotNull(api);
            Assert.NotEmpty(api.Urls);
        }
        finally
        {
            // Cleanup
            await api.StopAsync();
            await api.DisposeAsync();
        }
    }
}
