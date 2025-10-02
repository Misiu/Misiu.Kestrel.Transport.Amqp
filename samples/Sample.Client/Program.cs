using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

Console.WriteLine("Sample AMQP Client - Sending HTTP request via RabbitMQ");
Console.WriteLine("=======================================================");

// Connection settings
var factory = new ConnectionFactory
{
    HostName = "localhost",
    Port = 5672,
    UserName = "guest",
    Password = "guest"
};

using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

// Declare queues
var requestQueue = "kestrel.amqp.requests";
var responseQueue = "kestrel.amqp.responses";
channel.QueueDeclare(requestQueue, durable: true, exclusive: false, autoDelete: false);
channel.QueueDeclare(responseQueue, durable: true, exclusive: false, autoDelete: false);

// Create a unique correlation ID
var correlationId = Guid.NewGuid();
Console.WriteLine($"Correlation ID: {correlationId}");
Console.WriteLine();

// Build the HTTP request envelope
var request = new
{
    method = "GET",
    pathAndQuery = "/health",
    headers = new Dictionary<string, string[]>
    {
        ["Accept"] = new[] { "application/json" },
        ["User-Agent"] = new[] { "AMQP-Client/1.0" }
    }
};

var requestJson = JsonSerializer.Serialize(request);
var requestBytes = Encoding.UTF8.GetBytes(requestJson);

Console.WriteLine($"Sending request: {request.method} {request.pathAndQuery}");

// Send the request
var props = channel.CreateBasicProperties();
props.CorrelationId = correlationId.ToString();
props.Persistent = true;

channel.BasicPublish(
    exchange: "",
    routingKey: requestQueue,
    mandatory: false,
    basicProperties: props,
    body: requestBytes);

Console.WriteLine("Request sent. Waiting for response...");
Console.WriteLine();

// Set up consumer for responses
var responseReceived = false;
var consumer = new EventingBasicConsumer(channel);
consumer.Received += (sender, ea) =>
{
    if (ea.BasicProperties.CorrelationId == correlationId.ToString())
    {
        var responseText = Encoding.UTF8.GetString(ea.Body.ToArray());
        Console.WriteLine("Response received:");
        Console.WriteLine(responseText);
        Console.WriteLine();
        
        responseReceived = true;
        channel.BasicAck(ea.DeliveryTag, false);
    }
};

channel.BasicConsume(responseQueue, autoAck: false, consumer: consumer);

// Wait for response (with timeout)
var timeout = TimeSpan.FromSeconds(10);
var startTime = DateTime.UtcNow;
while (!responseReceived && (DateTime.UtcNow - startTime) < timeout)
{
    Thread.Sleep(100);
}

if (!responseReceived)
{
    Console.WriteLine("Timeout: No response received within 10 seconds.");
}

Console.WriteLine("Press any key to exit...");
Console.ReadKey();
