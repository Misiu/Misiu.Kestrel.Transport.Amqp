using Misiu.Kestrel.Transport.Amqp;

Console.WriteLine("Sample AMQP Client");

var options = new AmqpTransportOptions
{
    ConnectionString = "amqp://localhost",
    MaxMessageSize = 65536
};

Console.WriteLine($"Connection String: {options.ConnectionString}");
Console.WriteLine($"Max Message Size: {options.MaxMessageSize}");
