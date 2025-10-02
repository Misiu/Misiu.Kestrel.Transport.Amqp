using Testcontainers.RabbitMq;

namespace Misiu.Kestrel.Transport.Amqp.IntegrationTests.Infrastructure;

/// <summary>
/// Fixture for RabbitMQ container that is shared across tests
/// </summary>
public class RabbitMqFixture : IAsyncLifetime
{
    private RabbitMqContainer? _container;

    public string HostName => "localhost";
    public int Port { get; private set; }
    public string UserName => "guest";
    public string Password => "guest";
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _container = new RabbitMqBuilder()
            .WithImage("rabbitmq:3.13-management-alpine")
            .WithUsername(UserName)
            .WithPassword(Password)
            .Build();

        await _container.StartAsync();
        Port = _container.GetMappedPublicPort(5672);
        ConnectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    public async Task RestartAsync()
    {
        if (_container != null)
        {
            await _container.StopAsync();
            await Task.Delay(1000); // Give it time to fully stop
            await _container.StartAsync();
            await Task.Delay(2000); // Give it time to fully start and recover connections
        }
    }
}
