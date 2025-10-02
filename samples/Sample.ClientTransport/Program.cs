using Misiu.Kestrel.Transport.Amqp;

Console.WriteLine("Sample AMQP Client (Transport) - Listening on AMQP and serving via Kestrel");
Console.WriteLine("===========================================================================");
Console.WriteLine();

var builder = WebApplication.CreateBuilder(args);

// Configure AMQP Transport - using appsettings.json
builder.Services.AddAmqpTransport(builder.Configuration);

// Alternative: Configure programmatically
// builder.Services.AddAmqpTransport(options =>
// {
//     options.HostName = "localhost";
//     options.Port = 5672;
//     options.UserName = "guest";
//     options.Password = "guest";
//     options.RequestQueue = "amqp.gateway.requests";
//     options.ResponseQueue = "amqp.gateway.responses";
// });

// Configure Kestrel to use AMQP transport (in addition to or instead of HTTP)
builder.WebHost.ConfigureKestrel(kestrel =>
{
    // Listen on AMQP - requests come from the gateway via RabbitMQ
    kestrel.ListenAmqp("amqp-client");
});

var app = builder.Build();

// These endpoints will be accessible via the AMQP transport
app.MapGet("/", () => "Hello from AMQP Client Transport!");

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

app.Run();
