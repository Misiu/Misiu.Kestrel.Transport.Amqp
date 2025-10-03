using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Misiu.Kestrel.Transport.Amqp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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

// Simple endpoint that generates an image (PNG) without external drawing packages
app.MapGet("/api/image", async (HttpResponse response) =>
{
    const int width = 400;
    const int height = 200;

    using var image = new Image<Rgba32>(width, height);

    // Fill background and draw simple primitives manually
    var bg = new Rgba32(255, 255, 255, 255);
    var line = new Rgba32(25, 25, 112, 255); // MidnightBlue
    var border = new Rgba32(255, 69, 0, 255); // OrangeRed

    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            image[x, y] = bg;
        }
    }

    // Two diagonal lines
    for (int i = 0; i < Math.Min(width, height); i++)
    {
        image[i, i] = line;
        var x2 = width - 1 - i;
        if (x2 >= 0 && x2 < width)
        {
            image[x2, i] = line;
        }
    }

    // Rectangle border
    for (int x = 20; x < width - 20; x++)
    {
        image[x, 20] = border;
        image[x, height - 21] = border;
    }
    for (int y = 20; y < height - 20; y++)
    {
        image[20, y] = border;
        image[width - 21, y] = border;
    }

    response.ContentType = "image/png";
    await image.SaveAsPngAsync(response.Body);
});

// Simple endpoint that returns a CSV file
app.MapGet("/api/csv", () =>
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("id,name,value");
    for (var i = 1; i <= 5; i++)
    {
        sb.AppendLine($"{i},Item {i},{Random.Shared.Next(0, 1000)}");
    }

    var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    return Results.File(bytes, "text/csv", fileDownloadName: $"sample-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.csv");
});

Console.WriteLine("AMQP Transport Client started");
Console.WriteLine("Consuming from: amqp.gateway.requests");
Console.WriteLine("Publishing to: amqp.gateway.responses");
Console.WriteLine();
Console.WriteLine("This client receives HTTP requests via AMQP and processes them through Kestrel");
Console.WriteLine("Press Ctrl+C to exit");
Console.WriteLine();

await app.RunAsync();
