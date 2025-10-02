using Misiu.Kestrel.Transport.Amqp;

var builder = WebApplication.CreateBuilder(args);

// Configure AMQP Gateway
builder.Services.AddAmqpGateway(options =>
{
    options.HostName = "localhost";
    options.Port = 5672;
    options.UserName = "guest";
    options.Password = "guest";
    options.RequestQueue = "amqp.gateway.requests";
    options.ResponseQueue = "amqp.gateway.responses";
    options.ImmediateTimeoutSeconds = 3;
    options.ResultTtlMinutes = 15;
});

var app = builder.Build();

// Map result retrieval endpoint
app.MapAmqpResultEndpoint();

// Use AMQP Gateway middleware to forward all requests
app.UseAmqpGateway();

// These endpoints won't be reached because the gateway forwards all requests
// They're here to show the pattern - in a real scenario, you might want conditional routing
app.MapGet("/", () => "Sample AMQP Gateway Server");

app.Run();
