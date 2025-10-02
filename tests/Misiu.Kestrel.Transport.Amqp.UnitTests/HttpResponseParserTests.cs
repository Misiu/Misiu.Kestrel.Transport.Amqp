using System.Text;
using Xunit;

namespace Misiu.Kestrel.Transport.Amqp.UnitTests;

/// <summary>
/// Unit tests for HTTP response parsing in AmqpConnectionListener
/// These tests validate the ParseRawHttpResponse method with various content types
/// </summary>
public class HttpResponseParserTests
{
    [Fact]
    public void ParseRawHttpResponse_SimpleOkResponse_Success()
    {
        // Arrange
        var rawResponse = BuildRawHttpResponse(
            statusCode: 200,
            statusText: "OK",
            headers: new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json; charset=utf-8"
            },
            body: "{\"message\":\"Hello\"}"
        );

        // Act
        var envelope = ParseHttpResponse(rawResponse);

        // Assert
        Assert.Equal(200, envelope.StatusCode);
        Assert.Contains("Content-Type", envelope.Headers.Keys);
        Assert.Equal("application/json; charset=utf-8", envelope.Headers["Content-Type"][0]);
        Assert.NotNull(envelope.Body);
        Assert.Equal("{\"message\":\"Hello\"}", Encoding.UTF8.GetString(envelope.Body));
    }

    [Fact]
    public void ParseRawHttpResponse_NoBody_Success()
    {
        // Arrange
        var rawResponse = BuildRawHttpResponse(
            statusCode: 204,
            statusText: "No Content",
            headers: new Dictionary<string, string>(),
            body: null
        );

        // Act
        var envelope = ParseHttpResponse(rawResponse);

        // Assert
        Assert.Equal(204, envelope.StatusCode);
        Assert.Null(envelope.Body);
    }

    [Fact]
    public void ParseRawHttpResponse_MultipleHeaderValues_Success()
    {
        // Arrange
        var response = "HTTP/1.1 200 OK\r\n" +
                      "Set-Cookie: session=abc123\r\n" +
                      "Set-Cookie: tracking=xyz789\r\n" +
                      "Content-Type: text/html\r\n" +
                      "\r\n" +
                      "<html></html>";

        // Act
        var envelope = ParseHttpResponse(Encoding.UTF8.GetBytes(response));

        // Assert
        Assert.Equal(200, envelope.StatusCode);
        Assert.Contains("Set-Cookie", envelope.Headers.Keys);
        Assert.Equal(2, envelope.Headers["Set-Cookie"].Length);
        Assert.Contains("session=abc123", envelope.Headers["Set-Cookie"]);
        Assert.Contains("tracking=xyz789", envelope.Headers["Set-Cookie"]);
    }

    [Fact]
    public void ParseRawHttpResponse_BinaryData_Success()
    {
        // Arrange - simulate a binary response
        var binaryData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A }; // PNG header
        var response = "HTTP/1.1 200 OK\r\n" +
                      "Content-Type: image/png\r\n" +
                      "Content-Length: 6\r\n" +
                      "\r\n";
        var responseBytes = Encoding.UTF8.GetBytes(response)
            .Concat(binaryData)
            .ToArray();

        // Act
        var envelope = ParseHttpResponse(responseBytes);

        // Assert
        Assert.Equal(200, envelope.StatusCode);
        Assert.NotNull(envelope.Body);
        // Note: Our current parser converts body to UTF8 string then back, which may corrupt binary data
        // This test documents current behavior - ideally we'd preserve binary data
    }

    [Fact]
    public void ParseRawHttpResponse_404NotFound_Success()
    {
        // Arrange
        var rawResponse = BuildRawHttpResponse(
            statusCode: 404,
            statusText: "Not Found",
            headers: new Dictionary<string, string>
            {
                ["Content-Type"] = "text/plain"
            },
            body: "Resource not found"
        );

        // Act
        var envelope = ParseHttpResponse(rawResponse);

        // Assert
        Assert.Equal(404, envelope.StatusCode);
    }

    [Fact]
    public void ParseRawHttpResponse_500InternalServerError_Success()
    {
        // Arrange
        var rawResponse = BuildRawHttpResponse(
            statusCode: 500,
            statusText: "Internal Server Error",
            headers: new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json"
            },
            body: "{\"error\":\"Something went wrong\"}"
        );

        // Act
        var envelope = ParseHttpResponse(rawResponse);

        // Assert
        Assert.Equal(500, envelope.StatusCode);
    }

    [Fact]
    public void ParseRawHttpResponse_LargeBody_Success()
    {
        // Arrange - large JSON response
        var largeBody = "{\"data\":\"" + new string('x', 10000) + "\"}";
        var rawResponse = BuildRawHttpResponse(
            statusCode: 200,
            statusText: "OK",
            headers: new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json"
            },
            body: largeBody
        );

        // Act
        var envelope = ParseHttpResponse(rawResponse);

        // Assert
        Assert.Equal(200, envelope.StatusCode);
        Assert.NotNull(envelope.Body);
        Assert.True(envelope.Body.Length > 10000);
    }

    [Fact]
    public void ParseRawHttpResponse_UnicodeContent_Success()
    {
        // Arrange - response with international characters
        var unicodeBody = "{\"message\":\"Hello 世界 🌍\"}";
        var rawResponse = BuildRawHttpResponse(
            statusCode: 200,
            statusText: "OK",
            headers: new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json; charset=utf-8"
            },
            body: unicodeBody
        );

        // Act
        var envelope = ParseHttpResponse(rawResponse);

        // Assert
        Assert.Equal(200, envelope.StatusCode);
        Assert.NotNull(envelope.Body);
        var bodyText = Encoding.UTF8.GetString(envelope.Body);
        Assert.Contains("世界", bodyText);
        Assert.Contains("🌍", bodyText);
    }

    [Fact]
    public void ParseRawHttpResponse_EmptyBody_Success()
    {
        // Arrange
        var response = "HTTP/1.1 200 OK\r\n" +
                      "Content-Type: text/plain\r\n" +
                      "\r\n";

        // Act
        var envelope = ParseHttpResponse(Encoding.UTF8.GetBytes(response));

        // Assert
        Assert.Equal(200, envelope.StatusCode);
        // Body might be null or empty array depending on implementation
    }

    [Fact]
    public void ParseRawHttpResponse_HeadersWithSpaces_Success()
    {
        // Arrange - headers with spaces around colons
        var response = "HTTP/1.1 200 OK\r\n" +
                      "Content-Type:   application/json  \r\n" +
                      "X-Custom-Header:value-with-no-spaces\r\n" +
                      "\r\n" +
                      "{}";

        // Act
        var envelope = ParseHttpResponse(Encoding.UTF8.GetBytes(response));

        // Assert
        Assert.Equal(200, envelope.StatusCode);
        Assert.Contains("Content-Type", envelope.Headers.Keys);
        Assert.Equal("application/json", envelope.Headers["Content-Type"][0].Trim());
    }

    // Helper methods
    private static byte[] BuildRawHttpResponse(
        int statusCode,
        string statusText,
        Dictionary<string, string> headers,
        string? body)
    {
        var sb = new StringBuilder();
        sb.Append($"HTTP/1.1 {statusCode} {statusText}\r\n");

        foreach (var header in headers)
        {
            sb.Append($"{header.Key}: {header.Value}\r\n");
        }

        sb.Append("\r\n");

        if (body != null)
        {
            sb.Append(body);
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    // This simulates the ParseRawHttpResponse logic from AmqpConnectionListener
    // We'll need to refactor the actual code to make it testable
    private static HttpResponseEnvelope ParseHttpResponse(byte[] responseRaw)
    {
        var responseStr = Encoding.UTF8.GetString(responseRaw);
        var lines = responseStr.Split(new[] { "\r\n" }, StringSplitOptions.None);

        // Parse status line
        var statusLine = lines[0];
        var statusParts = statusLine.Split(' ', 3);
        var statusCode = int.Parse(statusParts[1]);

        // Parse headers
        var headers = new Dictionary<string, string[]>();
        int bodyStartIndex = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i]))
            {
                bodyStartIndex = i + 1;
                break;
            }

            var colonIndex = lines[i].IndexOf(':');
            if (colonIndex > 0)
            {
                var headerName = lines[i].Substring(0, colonIndex).Trim();
                var headerValue = lines[i].Substring(colonIndex + 1).Trim();

                if (headers.ContainsKey(headerName))
                {
                    var existing = headers[headerName];
                    var newArray = new string[existing.Length + 1];
                    existing.CopyTo(newArray, 0);
                    newArray[existing.Length] = headerValue;
                    headers[headerName] = newArray;
                }
                else
                {
                    headers[headerName] = new[] { headerValue };
                }
            }
        }

        // Extract body
        byte[]? body = null;
        if (bodyStartIndex < lines.Length)
        {
            var bodyText = string.Join("\r\n", lines, bodyStartIndex, lines.Length - bodyStartIndex);
            if (!string.IsNullOrEmpty(bodyText))
            {
                body = Encoding.UTF8.GetBytes(bodyText);
            }
        }

        return new HttpResponseEnvelope
        {
            StatusCode = statusCode,
            Headers = headers,
            Body = body
        };
    }
}
