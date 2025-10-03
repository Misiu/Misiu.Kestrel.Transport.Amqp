using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Misiu.Kestrel.Transport.Amqp;

Console.WriteLine("Sample AMQP Client (BackgroundService) - Processing requests from gateway");
Console.WriteLine("=========================================================================");
Console.WriteLine();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAmqpClient(builder.Configuration);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();

app.MapGet("/", Endpoints.GetRoot);
app.MapGet("/api/data", Endpoints.GetApiData);
app.MapPost("/api/echo", Endpoints.PostApiEcho);
app.MapGet("/api/slow", Endpoints.GetApiSlow);
app.MapGet("/api/image", Endpoints.GetApiImage);
app.MapGet("/api/csv", Endpoints.GetApiCsv);

Console.WriteLine("AMQP Transport Client started");
Console.WriteLine("Consuming from: amqp.gateway.requests");
Console.WriteLine("Publishing to: amqp.gateway.responses");
Console.WriteLine();
Console.WriteLine("This client receives HTTP requests via AMQP and processes them through Kestrel");
Console.WriteLine("Press Ctrl+C to exit");
Console.WriteLine();

await app.RunAsync();
