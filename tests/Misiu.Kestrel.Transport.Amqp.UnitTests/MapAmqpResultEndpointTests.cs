using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Misiu.Kestrel.Transport.Amqp.UnitTests;

public class MapAmqpResultEndpointTests
{
    [Fact]
    public async Task MapAmqpResultEndpoint_WithValidCorrelationId_ReturnsProperHttpResponse()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var envelope = new HttpResponseEnvelope
        {
            CorrelationId = correlationId,
            StatusCode = 200,
            Headers = new Dictionary<string, string[]>
            {
                ["Content-Type"] = new[] { "application/json" },
                ["X-Custom-Header"] = new[] { "custom-value" }
            },
            Body = Encoding.UTF8.GetBytes("{\"message\":\"test\"}"),
            ContentType = "application/json",
            ServerStartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-2),
            ServerCompletedAtUtc = DateTimeOffset.UtcNow,
            ProcessingMilliseconds = 1500
        };

        using var host = await CreateTestHost(cache =>
        {
            var cacheKey = $"amqp:result:{correlationId:N}";
            cache.Set(cacheKey, envelope, TimeSpan.FromMinutes(15));
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync($"/amqp/result/{correlationId}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        // Check custom headers
        response.Headers.Should().Contain(h => h.Key == "X-Processing-Time-Ms");
        response.Headers.GetValues("X-Processing-Time-Ms").First().Should().Be("1500");

        response.Headers.Should().Contain(h => h.Key == "X-Server-Started-At-Utc");
        response.Headers.Should().Contain(h => h.Key == "X-Server-Completed-At-Utc");

        // Check original headers
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        response.Headers.Should().Contain(h => h.Key == "X-Custom-Header");
        response.Headers.GetValues("X-Custom-Header").First().Should().Be("custom-value");

        // Check body
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("{\"message\":\"test\"}");
    }

    [Fact]
    public async Task MapAmqpResultEndpoint_WithImageContent_ReturnsProperImageResponse()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        // Simulate PNG header
        var pngData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        var envelope = new HttpResponseEnvelope
        {
            CorrelationId = correlationId,
            StatusCode = 200,
            Headers = new Dictionary<string, string[]>
            {
                ["Content-Type"] = new[] { "image/png" }
            },
            Body = pngData,
            ContentType = "image/png",
            ServerStartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
            ServerCompletedAtUtc = DateTimeOffset.UtcNow,
            ProcessingMilliseconds = 800
        };

        using var host = await CreateTestHost(cache =>
        {
            var cacheKey = $"amqp:result:{correlationId:N}";
            cache.Set(cacheKey, envelope, TimeSpan.FromMinutes(15));
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync($"/amqp/result/{correlationId}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("image/png");

        var body = await response.Content.ReadAsByteArrayAsync();
        body.Should().Equal(pngData);
    }

    [Fact]
    public async Task MapAmqpResultEndpoint_WithTextContent_ReturnsProperTextResponse()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var textContent = "This is plain text content";

        var envelope = new HttpResponseEnvelope
        {
            CorrelationId = correlationId,
            StatusCode = 200,
            Headers = new Dictionary<string, string[]>
            {
                ["Content-Type"] = new[] { "text/plain" }
            },
            Body = Encoding.UTF8.GetBytes(textContent),
            ContentType = "text/plain",
            ServerStartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
            ServerCompletedAtUtc = DateTimeOffset.UtcNow,
            ProcessingMilliseconds = 500
        };

        using var host = await CreateTestHost(cache =>
        {
            var cacheKey = $"amqp:result:{correlationId:N}";
            cache.Set(cacheKey, envelope, TimeSpan.FromMinutes(15));
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync($"/amqp/result/{correlationId}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be(textContent);
    }

    [Fact]
    public async Task MapAmqpResultEndpoint_WithNullBody_ReturnsEmptyResponse()
    {
        // Arrange
        var correlationId = Guid.NewGuid();

        var envelope = new HttpResponseEnvelope
        {
            CorrelationId = correlationId,
            StatusCode = 204,
            Headers = new Dictionary<string, string[]>(),
            Body = null,
            ContentType = null,
            ServerStartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
            ServerCompletedAtUtc = DateTimeOffset.UtcNow,
            ProcessingMilliseconds = 100
        };

        using var host = await CreateTestHost(cache =>
        {
            var cacheKey = $"amqp:result:{correlationId:N}";
            cache.Set(cacheKey, envelope, TimeSpan.FromMinutes(15));
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync($"/amqp/result/{correlationId}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);
        response.Headers.Should().Contain(h => h.Key == "X-Processing-Time-Ms");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task MapAmqpResultEndpoint_WithInvalidCorrelationId_ReturnsNotFound()
    {
        // Arrange
        var correlationId = Guid.NewGuid();

        using var host = await CreateTestHost(cache => { }); // Empty cache

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync($"/amqp/result/{correlationId}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("not_found");
        body.Should().Contain(correlationId.ToString());
    }

    [Fact]
    public async Task MapAmqpResultEndpoint_WithMultipleHeaders_ReturnsAllHeaders()
    {
        // Arrange
        var correlationId = Guid.NewGuid();

        var envelope = new HttpResponseEnvelope
        {
            CorrelationId = correlationId,
            StatusCode = 200,
            Headers = new Dictionary<string, string[]>
            {
                ["Content-Type"] = new[] { "application/xml" },
                ["X-Header-1"] = new[] { "value1" },
                ["X-Header-2"] = new[] { "value2" },
                ["X-Multi-Value"] = new[] { "val1", "val2", "val3" }
            },
            Body = Encoding.UTF8.GetBytes("<root>test</root>"),
            ContentType = "application/xml",
            ServerStartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
            ServerCompletedAtUtc = DateTimeOffset.UtcNow,
            ProcessingMilliseconds = 300
        };

        using var host = await CreateTestHost(cache =>
        {
            var cacheKey = $"amqp:result:{correlationId:N}";
            cache.Set(cacheKey, envelope, TimeSpan.FromMinutes(15));
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync($"/amqp/result/{correlationId}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        response.Headers.Should().Contain(h => h.Key == "X-Header-1");
        response.Headers.Should().Contain(h => h.Key == "X-Header-2");
        response.Headers.Should().Contain(h => h.Key == "X-Multi-Value");

        response.Headers.GetValues("X-Multi-Value").Should().Equal("val1", "val2", "val3");
    }

    [Fact]
    public async Task MapAmqpResultEndpoint_With404Status_Returns404()
    {
        // Arrange
        var correlationId = Guid.NewGuid();

        var envelope = new HttpResponseEnvelope
        {
            CorrelationId = correlationId,
            StatusCode = 404,
            Headers = new Dictionary<string, string[]>
            {
                ["Content-Type"] = new[] { "application/json" }
            },
            Body = Encoding.UTF8.GetBytes("{\"error\":\"not found\"}"),
            ContentType = "application/json",
            ServerStartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
            ServerCompletedAtUtc = DateTimeOffset.UtcNow,
            ProcessingMilliseconds = 50
        };

        using var host = await CreateTestHost(cache =>
        {
            var cacheKey = $"amqp:result:{correlationId:N}";
            cache.Set(cacheKey, envelope, TimeSpan.FromMinutes(15));
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync($"/amqp/result/{correlationId}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("{\"error\":\"not found\"}");
    }

    [Fact]
    public async Task MapAmqpResultEndpoint_WithCustomStatusCode_ReturnsCorrectStatus()
    {
        // Arrange
        var correlationId = Guid.NewGuid();

        var envelope = new HttpResponseEnvelope
        {
            CorrelationId = correlationId,
            StatusCode = 418, // I'm a teapot
            Headers = new Dictionary<string, string[]>
            {
                ["Content-Type"] = new[] { "text/plain" }
            },
            Body = Encoding.UTF8.GetBytes("I'm a teapot"),
            ContentType = "text/plain",
            ServerStartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
            ServerCompletedAtUtc = DateTimeOffset.UtcNow,
            ProcessingMilliseconds = 1
        };

        using var host = await CreateTestHost(cache =>
        {
            var cacheKey = $"amqp:result:{correlationId:N}";
            cache.Set(cacheKey, envelope, TimeSpan.FromMinutes(15));
        });

        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync($"/amqp/result/{correlationId}");

        // Assert
        response.StatusCode.Should().Be((System.Net.HttpStatusCode)418);
    }

    private async Task<IHost> CreateTestHost(Action<IMemoryCache> setupCache)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddMemoryCache();
                    services.AddRouting();
                });
                webHost.Configure(app =>
                {
                    // Setup cache
                    var cache = app.ApplicationServices.GetRequiredService<IMemoryCache>();
                    setupCache(cache);

                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapAmqpResultEndpoint();
                    });
                });
            });

        var host = await hostBuilder.StartAsync();
        return host;
    }
}
