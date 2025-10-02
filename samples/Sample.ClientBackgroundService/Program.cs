using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Misiu.Kestrel.Transport.Amqp;

Console.WriteLine("Sample AMQP Client (BackgroundService) - Processing requests from gateway");
Console.WriteLine("=========================================================================");
Console.WriteLine();

var builder = WebApplication.CreateBuilder(args);

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


var app = builder.Build();

// These endpoints will be accessible via the AMQP transport
app.MapGet("/", () =>
{
    Console.WriteLine("We got a request");
    return "Hello from AMQP Client Transport!";
});

app.MapGet("/api/data", () => Results.Ok(new
{
    Message = "Data from local API behind firewall",
    Timestamp = DateTimeOffset.UtcNow,
    Source = "AMQP Transport Client"
}));

app.MapPost("/api/echo", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    return Results.Ok(new
    {
        Method = request.Method,
        Path = request.Path.ToString(),
        ReceivedBody = body,
        Timestamp = DateTimeOffset.UtcNow
    });
});

app.MapGet("/api/slow", async () =>
{
    await Task.Delay(5000); // Simulate slow operation
    return Results.Ok(new { Message = "Slow operation completed", Timestamp = DateTimeOffset.UtcNow });
});

Console.WriteLine("AMQP Transport Client started");
Console.WriteLine("Consuming from: amqp.gateway.requests");
Console.WriteLine("Publishing to: amqp.gateway.responses");
Console.WriteLine();
Console.WriteLine("This client receives HTTP requests via AMQP and processes them through Kestrel");
Console.WriteLine("Press Ctrl+C to exit");
Console.WriteLine();

await app.RunAsync();
