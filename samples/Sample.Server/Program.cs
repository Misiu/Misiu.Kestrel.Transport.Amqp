using Misiu.Kestrel.Transport.Amqp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.Configure<AmqpTransportOptions>(options =>
{
    options.ConnectionString = "amqp://localhost";
    options.MaxMessageSize = 65536;
});

var app = builder.Build();

app.MapGet("/", () => "Sample AMQP Server");

app.Run();
