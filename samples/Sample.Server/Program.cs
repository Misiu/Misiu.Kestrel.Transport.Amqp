using Misiu.Kestrel.Transport.Amqp;

var builder = WebApplication.CreateBuilder(args);

// Configure AMQP transport
builder.Services.AddAmqpTransport(options =>
{
    options.HostName = "localhost";
    options.Port = 5672;
    options.UserName = "guest";
    options.Password = "guest";
    options.RequestQueue = "kestrel.amqp.requests";
    options.ResponseQueue = "kestrel.amqp.responses";
});

// Configure Kestrel to use AMQP transport alongside HTTP
builder.WebHost.ConfigureKestrel(kestrel =>
{
    // Standard HTTP endpoint
    kestrel.ListenLocalhost(5000);
    
    // AMQP endpoint
    kestrel.ListenAmqp("amqp-transport");
});

var app = builder.Build();

app.MapGet("/", () => "Sample AMQP Server - Hello from AMQP transport!");

app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Timestamp = DateTimeOffset.UtcNow
}));

app.MapPost("/echo", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    return Results.Ok(new
    {
        Method = request.Method,
        Path = request.Path.ToString(),
        Body = body
    });
});

app.Run();
