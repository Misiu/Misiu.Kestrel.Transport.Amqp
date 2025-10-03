using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

static class Endpoints
{
    public static string GetRoot()
    {
        return "Hello from AMQP Client Transport!";
    }

    public static IResult GetApiData()
    {
        return Results.Ok(new
        {
            Message = "Data from local API behind firewall",
            Timestamp = DateTimeOffset.UtcNow,
            Source = "AMQP Transport Client"
        });
    }

    public static async Task<IResult> PostApiEcho(HttpRequest request)
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
    }

    public static async Task<IResult> GetApiSlow()
    {
        await Task.Delay(5000);
        return Results.Ok(new { Message = "Slow operation completed", Timestamp = DateTimeOffset.UtcNow });
    }

    public static async Task GetApiImage(HttpResponse response)
    {
        const int width = 400;
        const int height = 200;

        using var image = new Image<Rgba32>(width, height);

        var bg = new Rgba32(255, 255, 255, 255);
        var line = new Rgba32(25, 25, 112, 255);
        var border = new Rgba32(255, 69, 0, 255);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image[x, y] = bg;
            }
        }

        for (int i = 0; i < Math.Min(width, height); i++)
        {
            image[i, i] = line;
            var x2 = width - 1 - i;
            if (x2 >= 0 && x2 < width)
            {
                image[x2, i] = line;
            }
        }

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
    }

    public static IResult GetApiCsv()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("id,name,value");
        for (var i = 1; i <= 5; i++)
        {
            sb.AppendLine($"{i},Item {i},{Random.Shared.Next(0, 1000)}");
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        return Results.File(bytes, "text/csv", fileDownloadName: $"sample-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.csv");
    }
}
