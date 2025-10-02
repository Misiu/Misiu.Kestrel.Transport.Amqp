namespace Misiu.Kestrel.Transport.Amqp.IntegrationTests.Infrastructure;

/// <summary>
/// Collection definition for RabbitMQ fixture sharing across test classes
/// </summary>
[CollectionDefinition("RabbitMQ")]
public class RabbitMqCollection : ICollectionFixture<RabbitMqFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
