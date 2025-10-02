using System.Text.Json;
using FluentAssertions;

namespace Misiu.Kestrel.Transport.Amqp.UnitTests;

public class HttpResponseEnvelopeTests
{
    [Fact]
    public void Constructor_CreatesInstanceWithDefaults()
    {
        // Arrange & Act
        var envelope = new HttpResponseEnvelope();

        // Assert
        envelope.CorrelationId.Should().Be(Guid.Empty);
        envelope.StatusCode.Should().Be(0);
        envelope.Headers.Should().NotBeNull();
        envelope.Headers.Should().BeEmpty();
        envelope.Body.Should().BeNull();
        envelope.ContentType.Should().BeNull();
        envelope.ServerStartedAtUtc.Should().Be(default(DateTimeOffset));
        envelope.ServerCompletedAtUtc.Should().Be(default(DateTimeOffset));
        envelope.ProcessingMilliseconds.Should().Be(0);
        envelope.GatewayEnqueuedAtUtc.Should().Be(default(DateTimeOffset));
    }

    [Fact]
    public void CorrelationId_CanBeSet()
    {
        // Arrange
        var envelope = new HttpResponseEnvelope();
        var correlationId = Guid.NewGuid();

        // Act
        envelope.CorrelationId = correlationId;

        // Assert
        envelope.CorrelationId.Should().Be(correlationId);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(204)]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public void StatusCode_CanBeSet_WithVariousHttpStatusCodes(int statusCode)
    {
        // Arrange
        var envelope = new HttpResponseEnvelope();

        // Act
        envelope.StatusCode = statusCode;

        // Assert
        envelope.StatusCode.Should().Be(statusCode);
    }

    [Fact]
    public void Headers_CanBeSet_WithMultipleHeaders()
    {
        // Arrange
        var envelope = new HttpResponseEnvelope();
        var headers = new Dictionary<string, string[]>
        {
            ["Content-Type"] = new[] { "application/json" },
            ["Cache-Control"] = new[] { "no-cache" },
            ["Set-Cookie"] = new[] { "session=abc123", "user=john" }
        };

        // Act
        envelope.Headers = headers;

        // Assert
        envelope.Headers.Should().HaveCount(3);
        envelope.Headers["Content-Type"].Should().Equal("application/json");
        envelope.Headers["Cache-Control"].Should().Equal("no-cache");
        envelope.Headers["Set-Cookie"].Should().Equal("session=abc123", "user=john");
    }

    [Fact]
    public void Body_CanBeSet_WithByteArray()
    {
        // Arrange
        var envelope = new HttpResponseEnvelope();
        var body = System.Text.Encoding.UTF8.GetBytes("Response body content");

        // Act
        envelope.Body = body;

        // Assert
        envelope.Body.Should().Equal(body);
    }

    [Fact]
    public void Body_CanBeNull()
    {
        // Arrange
        var envelope = new HttpResponseEnvelope();

        // Act
        envelope.Body = null;

        // Assert
        envelope.Body.Should().BeNull();
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("application/xml")]
    [InlineData("text/html")]
    [InlineData("text/plain")]
    public void ContentType_CanBeSet_WithVariousTypes(string contentType)
    {
        // Arrange
        var envelope = new HttpResponseEnvelope();

        // Act
        envelope.ContentType = contentType;

        // Assert
        envelope.ContentType.Should().Be(contentType);
    }

    [Fact]
    public void ServerStartedAtUtc_CanBeSet()
    {
        // Arrange
        var envelope = new HttpResponseEnvelope();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        envelope.ServerStartedAtUtc = timestamp;

        // Assert
        envelope.ServerStartedAtUtc.Should().Be(timestamp);
    }

    [Fact]
    public void ServerCompletedAtUtc_CanBeSet()
    {
        // Arrange
        var envelope = new HttpResponseEnvelope();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        envelope.ServerCompletedAtUtc = timestamp;

        // Assert
        envelope.ServerCompletedAtUtc.Should().Be(timestamp);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(5000)]
    public void ProcessingMilliseconds_CanBeSet_WithVariousValues(long milliseconds)
    {
        // Arrange
        var envelope = new HttpResponseEnvelope();

        // Act
        envelope.ProcessingMilliseconds = milliseconds;

        // Assert
        envelope.ProcessingMilliseconds.Should().Be(milliseconds);
    }

    [Fact]
    public void GatewayEnqueuedAtUtc_CanBeSet()
    {
        // Arrange
        var envelope = new HttpResponseEnvelope();
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
        var original = new HttpResponseEnvelope
        {
            CorrelationId = Guid.NewGuid(),
            StatusCode = 200,
            Headers = new Dictionary<string, string[]>
            {
                ["Content-Type"] = new[] { "application/json" },
                ["X-Response-Time"] = new[] { "150ms" }
            },
            Body = System.Text.Encoding.UTF8.GetBytes("response body"),
            ContentType = "application/json",
            ServerStartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
            ServerCompletedAtUtc = DateTimeOffset.UtcNow,
            ProcessingMilliseconds = 150,
            GatewayEnqueuedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-2)
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<HttpResponseEnvelope>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.CorrelationId.Should().Be(original.CorrelationId);
        deserialized.StatusCode.Should().Be(original.StatusCode);
        deserialized.Headers.Should().HaveCount(original.Headers.Count);
        deserialized.Body.Should().Equal(original.Body!);
        deserialized.ContentType.Should().Be(original.ContentType);
        deserialized.ServerStartedAtUtc.Should().BeCloseTo(original.ServerStartedAtUtc, TimeSpan.FromMilliseconds(1));
        deserialized.ServerCompletedAtUtc.Should().BeCloseTo(original.ServerCompletedAtUtc, TimeSpan.FromMilliseconds(1));
        deserialized.ProcessingMilliseconds.Should().Be(original.ProcessingMilliseconds);
        deserialized.GatewayEnqueuedAtUtc.Should().BeCloseTo(original.GatewayEnqueuedAtUtc, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void Serialization_WithNullBody_WorksCorrectly()
    {
        // Arrange
        var original = new HttpResponseEnvelope
        {
            CorrelationId = Guid.NewGuid(),
            StatusCode = 204,
            Body = null
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<HttpResponseEnvelope>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Body.Should().BeNull();
    }

    [Fact]
    public void Serialization_WithEmptyHeaders_WorksCorrectly()
    {
        // Arrange
        var original = new HttpResponseEnvelope
        {
            CorrelationId = Guid.NewGuid(),
            StatusCode = 200,
            Headers = new Dictionary<string, string[]>()
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<HttpResponseEnvelope>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Headers.Should().BeEmpty();
    }

    [Fact]
    public void AllTimestampProperties_CanBeSetWithDifferentValues()
    {
        // Arrange
        var envelope = new HttpResponseEnvelope();
        var gatewayTime = DateTimeOffset.UtcNow.AddSeconds(-10);
        var startTime = DateTimeOffset.UtcNow.AddSeconds(-5);
        var endTime = DateTimeOffset.UtcNow;

        // Act
        envelope.GatewayEnqueuedAtUtc = gatewayTime;
        envelope.ServerStartedAtUtc = startTime;
        envelope.ServerCompletedAtUtc = endTime;

        // Assert
        envelope.GatewayEnqueuedAtUtc.Should().Be(gatewayTime);
        envelope.ServerStartedAtUtc.Should().Be(startTime);
        envelope.ServerCompletedAtUtc.Should().Be(endTime);
        envelope.ServerCompletedAtUtc.Should().BeAfter(envelope.ServerStartedAtUtc);
        envelope.ServerStartedAtUtc.Should().BeAfter(envelope.GatewayEnqueuedAtUtc);
    }
}
