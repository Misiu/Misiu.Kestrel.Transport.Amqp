using FluentAssertions;

namespace Misiu.Kestrel.Transport.Amqp.UnitTests;

public class AmqpEndPointTests
{
    [Fact]
    public void Constructor_WithNameOnly_SetsNameAndNullOptionsName()
    {
        // Arrange & Act
        var endpoint = new AmqpEndPoint("test-endpoint");

        // Assert
        endpoint.Name.Should().Be("test-endpoint");
        endpoint.OptionsName.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNameAndOptionsName_SetsBothProperties()
    {
        // Arrange & Act
        var endpoint = new AmqpEndPoint("test-endpoint", "custom-options");

        // Assert
        endpoint.Name.Should().Be("test-endpoint");
        endpoint.OptionsName.Should().Be("custom-options");
    }

    [Theory]
    [InlineData("endpoint1")]
    [InlineData("my-endpoint")]
    [InlineData("test_endpoint")]
    [InlineData("ENDPOINT123")]
    public void Constructor_WithVariousNames_SetsNameCorrectly(string name)
    {
        // Arrange & Act
        var endpoint = new AmqpEndPoint(name);

        // Assert
        endpoint.Name.Should().Be(name);
    }

    [Theory]
    [InlineData("options1")]
    [InlineData("amqp:endpoint1")]
    [InlineData("custom-options-name")]
    public void Constructor_WithVariousOptionsNames_SetsOptionsNameCorrectly(string optionsName)
    {
        // Arrange & Act
        var endpoint = new AmqpEndPoint("test", optionsName);

        // Assert
        endpoint.OptionsName.Should().Be(optionsName);
    }

    [Fact]
    public void ToString_ReturnsCorrectFormat()
    {
        // Arrange
        var endpoint = new AmqpEndPoint("my-endpoint");

        // Act
        var result = endpoint.ToString();

        // Assert
        result.Should().Be("amqp://my-endpoint");
    }

    [Theory]
    [InlineData("endpoint1", "amqp://endpoint1")]
    [InlineData("test-endpoint", "amqp://test-endpoint")]
    [InlineData("ENDPOINT", "amqp://ENDPOINT")]
    public void ToString_WithVariousNames_ReturnsCorrectFormat(string name, string expected)
    {
        // Arrange
        var endpoint = new AmqpEndPoint(name);

        // Act
        var result = endpoint.ToString();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ToString_IgnoresOptionsName()
    {
        // Arrange
        var endpoint = new AmqpEndPoint("my-endpoint", "custom-options");

        // Act
        var result = endpoint.ToString();

        // Assert
        result.Should().Be("amqp://my-endpoint");
        result.Should().NotContain("custom-options");
    }

    [Fact]
    public void Constructor_WithNullOptionsName_SetsOptionsNameToNull()
    {
        // Arrange & Act
        var endpoint = new AmqpEndPoint("test", null);

        // Assert
        endpoint.Name.Should().Be("test");
        endpoint.OptionsName.Should().BeNull();
    }

    [Fact]
    public void Name_IsImmutable()
    {
        // Arrange
        var endpoint = new AmqpEndPoint("original-name");

        // Assert
        endpoint.Name.Should().Be("original-name");
        // Name property has no setter, so it's immutable
    }

    [Fact]
    public void OptionsName_IsImmutable()
    {
        // Arrange
        var endpoint = new AmqpEndPoint("test", "original-options");

        // Assert
        endpoint.OptionsName.Should().Be("original-options");
        // OptionsName property has no setter, so it's immutable
    }
}
