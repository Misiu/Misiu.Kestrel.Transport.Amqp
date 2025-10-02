using Misiu.Kestrel.Transport.Amqp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

Console.WriteLine("Sample AMQP Client (BackgroundService) - Processing requests from gateway");
Console.WriteLine("=========================================================================");
Console.WriteLine();

var builder = Host.CreateApplicationBuilder(args);

// Configure AMQP Client - using appsettings.json
builder.Services.AddAmqpClient(builder.Configuration);

// Alternative: Configure programmatically
// builder.Services.AddAmqpClient(options =>
// {
//     options.HostName = "localhost";
//     options.Port = 5672;
//     options.UserName = "guest";
//     options.Password = "guest";
//     options.RequestQueue = "amqp.gateway.requests";
//     options.ResponseQueue = "amqp.gateway.responses";
//     options.LocalApiBaseUrl = "http://localhost:5001"; // Or leave empty for auto-detection
//     options.PrefetchCount = 10;
// });

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var host = builder.Build();

Console.WriteLine("Starting AMQP Client Consumer...");
Console.WriteLine($"Forwarding requests to: http://localhost:5001");
Console.WriteLine($"Consuming from: amqp.gateway.requests");
Console.WriteLine();
Console.WriteLine("Press Ctrl+C to exit");
Console.WriteLine();

await host.RunAsync();
