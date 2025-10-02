using System.Text.Json;
using FluentAssertions;

namespace Misiu.Kestrel.Transport.Amqp.UnitTests;

public class HttpRequestEnvelopeTests
{
    [Fact]
    public void Constructor_CreatesInstanceWithDefaults()
    {
        // Arrange & Act
        var envelope = new HttpRequestEnvelope();

        // Assert
        envelope.CorrelationId.Should().Be(Guid.Empty);
        envelope.Method.Should().Be("GET");
        envelope.PathAndQuery.Should().Be("/");
        envelope.Headers.Should().NotBeNull();
        envelope.Headers.Should().BeEmpty();
        envelope.Body.Should().BeNull();
        envelope.ContentType.Should().BeNull();
        envelope.GatewayEnqueuedAtUtc.Should().Be(default(DateTimeOffset));
    }

    [Fact]
    public void CorrelationId_CanBeSet()
    {
        // Arrange
        var envelope = new HttpRequestEnvelope();
        var correlationId = Guid.NewGuid();

        // Act
        envelope.CorrelationId = correlationId;

        // Assert
        envelope.CorrelationId.Should().Be(correlationId);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public void Method_CanBeSet_WithVariousHttpMethods(string method)
    {
        // Arrange
        var envelope = new HttpRequestEnvelope();

        // Act
        envelope.Method = method;

        // Assert
        envelope.Method.Should().Be(method);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/api/users")]
    [InlineData("/api/users?page=1&limit=10")]
    [InlineData("/path/to/resource?query=value&other=data")]
    public void PathAndQuery_CanBeSet_WithVariousPaths(string path)
    {
        // Arrange
        var envelope = new HttpRequestEnvelope();

        // Act
        envelope.PathAndQuery = path;

        // Assert
        envelope.PathAndQuery.Should().Be(path);
    }

    [Fact]
    public void Headers_CanBeSet_WithMultipleHeaders()
    {
        // Arrange
        var envelope = new HttpRequestEnvelope();
        var headers = new Dictionary<string, string[]>
        {
            ["Content-Type"] = new[] { "application/json" },
            ["Authorization"] = new[] { "Bearer token123" },
            ["Accept"] = new[] { "application/json", "text/plain" }
        };

        // Act
        envelope.Headers = headers;

        // Assert
        envelope.Headers.Should().HaveCount(3);
        envelope.Headers["Content-Type"].Should().Equal("application/json");
        envelope.Headers["Authorization"].Should().Equal("Bearer token123");
        envelope.Headers["Accept"].Should().Equal("application/json", "text/plain");
    }

    [Fact]
    public void Body_CanBeSet_WithByteArray()
    {
        // Arrange
        var envelope = new HttpRequestEnvelope();
        var body = System.Text.Encoding.UTF8.GetBytes("Test body content");

        // Act
        envelope.Body = body;

        // Assert
        envelope.Body.Should().Equal(body);
    }

    [Fact]
    public void Body_CanBeNull()
    {
        // Arrange
        var envelope = new HttpRequestEnvelope();

        // Act
        envelope.Body = null;

        // Assert
        envelope.Body.Should().BeNull();
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("application/xml")]
    [InlineData("text/plain")]
    [InlineData("multipart/form-data")]
    public void ContentType_CanBeSet_WithVariousTypes(string contentType)
    {
        // Arrange
        var envelope = new HttpRequestEnvelope();

        // Act
        envelope.ContentType = contentType;

        // Assert
        envelope.ContentType.Should().Be(contentType);
    }

    [Fact]
    public void GatewayEnqueuedAtUtc_CanBeSet()
    {
        // Arrange
        var envelope = new HttpRequestEnvelope();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        envelope.GatewayEnqueuedAtUtc = timestamp;

        // Assert
        envelope.GatewayEnqueuedAtUtc.Should().Be(timestamp);
    }

    [Fact]
    public void Serialization_RoundTrip_PreservesAllProperties()
    {
        // Arrange
        var original = new HttpRequestEnvelope
        {
            CorrelationId = Guid.NewGuid(),
            Method = "POST",
            PathAndQuery = "/api/test?param=value",
            Headers = new Dictionary<string, string[]>
            {
                ["Content-Type"] = new[] { "application/json" },
                ["X-Custom-Header"] = new[] { "custom-value" }
            },
            Body = System.Text.Encoding.UTF8.GetBytes("test body"),
            ContentType = "application/json",
            GatewayEnqueuedAtUtc = DateTimeOffset.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<HttpRequestEnvelope>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.CorrelationId.Should().Be(original.CorrelationId);
        deserialized.Method.Should().Be(original.Method);
        deserialized.PathAndQuery.Should().Be(original.PathAndQuery);
        deserialized.Headers.Should().HaveCount(original.Headers.Count);
        deserialized.Body.Should().Equal(original.Body!);
        deserialized.ContentType.Should().Be(original.ContentType);
        deserialized.GatewayEnqueuedAtUtc.Should().BeCloseTo(original.GatewayEnqueuedAtUtc, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void Serialization_WithNullBody_WorksCorrectly()
    {
        // Arrange
        var original = new HttpRequestEnvelope
        {
            CorrelationId = Guid.NewGuid(),
            Method = "GET",
            PathAndQuery = "/",
            Body = null
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<HttpRequestEnvelope>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Body.Should().BeNull();
    }

    [Fact]
    public void Serialization_WithEmptyHeaders_WorksCorrectly()
    {
        // Arrange
        var original = new HttpRequestEnvelope
        {
            CorrelationId = Guid.NewGuid(),
            Method = "GET",
            PathAndQuery = "/",
            Headers = new Dictionary<string, string[]>()
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<HttpRequestEnvelope>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Headers.Should().BeEmpty();
    }
}
