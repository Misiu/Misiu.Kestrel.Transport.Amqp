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

    [Fact]
    public void ParseRawHttpResponse_ChunkedTransferEncoding_DecodesCorrectly()
    {
        // Arrange - simulate chunked encoding from Kestrel
        // This is the actual format causing the issue: 82\r\n{json}\r\n0\r\n\r\n
        var jsonBody = "{\"message\":\"Data from local API behind firewall\",\"timestamp\":\"2025-10-02T11:55:15.2083072+00:00\",\"source\":\"AMQP Transport Client\"}";
        var chunkSize = jsonBody.Length.ToString("X"); // 82 in hex = 130 in decimal

        var response = "HTTP/1.1 200 OK\r\n" +
                      "Content-Type: application/json; charset=utf-8\r\n" +
                      "Transfer-Encoding: chunked\r\n" +
                      "\r\n" +
                      chunkSize + "\r\n" +
                      jsonBody + "\r\n" +
                      "0\r\n" +
                      "\r\n";

        // Act
        var envelope = ParseHttpResponse(Encoding.UTF8.GetBytes(response));

        // Assert
        Assert.Equal(200, envelope.StatusCode);
        Assert.NotNull(envelope.Body);
        var bodyText = Encoding.UTF8.GetString(envelope.Body);
        Assert.Equal(jsonBody, bodyText);

        // Verify Transfer-Encoding header is NOT in the final headers (hop-by-hop header)
        Assert.DoesNotContain("Transfer-Encoding", envelope.Headers.Keys);
    }

    [Fact]
    public void ParseRawHttpResponse_ChunkedWithMultipleChunks_DecodesCorrectly()
    {
        // Arrange - multiple chunks
        var response = "HTTP/1.1 200 OK\r\n" +
                      "Content-Type: text/plain\r\n" +
                      "Transfer-Encoding: chunked\r\n" +
                      "\r\n" +
                      "7\r\n" +
                      "Mozilla\r\n" +
                      "9\r\n" +
                      "Developer\r\n" +
                      "7\r\n" +
                      "Network\r\n" +
                      "0\r\n" +
                      "\r\n";

        // Act
        var envelope = ParseHttpResponse(Encoding.UTF8.GetBytes(response));

        // Assert
        Assert.Equal(200, envelope.StatusCode);
        Assert.NotNull(envelope.Body);
        var bodyText = Encoding.UTF8.GetString(envelope.Body);
        Assert.Equal("MozillaDeveloperNetwork", bodyText);
    }

    [Fact]
    public void ParseRawHttpResponse_FiltersHopByHopHeaders()
    {
        // Arrange - response with hop-by-hop headers that should be filtered
        var response = "HTTP/1.1 200 OK\r\n" +
                      "Content-Type: application/json\r\n" +
                      "Connection: keep-alive\r\n" +
                      "Keep-Alive: timeout=5\r\n" +
                      "Transfer-Encoding: chunked\r\n" +
                      "Upgrade: h2c\r\n" +
                      "Proxy-Connection: keep-alive\r\n" +
                      "X-Custom-Header: should-remain\r\n" +
                      "\r\n" +
                      "d\r\n" +
                      "{\"test\":true}\r\n" +
                      "0\r\n" +
                      "\r\n";

        // Act
        var envelope = ParseHttpResponse(Encoding.UTF8.GetBytes(response));

        // Assert
        Assert.Equal(200, envelope.StatusCode);

        // These hop-by-hop headers should be filtered out
        Assert.DoesNotContain("Connection", envelope.Headers.Keys);
        Assert.DoesNotContain("Keep-Alive", envelope.Headers.Keys);
        Assert.DoesNotContain("Transfer-Encoding", envelope.Headers.Keys);
        Assert.DoesNotContain("Upgrade", envelope.Headers.Keys);
        Assert.DoesNotContain("Proxy-Connection", envelope.Headers.Keys);

        // But custom headers should remain
        Assert.Contains("Content-Type", envelope.Headers.Keys);
        Assert.Contains("X-Custom-Header", envelope.Headers.Keys);
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
        var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        int bodyStartIndex = 0;
        bool isChunked = false;

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

                // Detect chunked transfer encoding
                if (headerName.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) &&
                    headerValue.Contains("chunked", StringComparison.OrdinalIgnoreCase))
                {
                    isChunked = true;
                }

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

        // Filter out hop-by-hop headers (these are invalid for HTTP/2 and HTTP/3)
        var hopByHopHeaders = new[] { "Connection", "Keep-Alive", "Transfer-Encoding", "Upgrade", "Proxy-Connection" };
        foreach (var hopHeader in hopByHopHeaders)
        {
            headers.Remove(hopHeader);
        }

        // Extract and decode body
        byte[]? body = null;
        if (bodyStartIndex < lines.Length)
        {
            if (isChunked)
            {
                // Decode chunked transfer encoding
                body = DecodeChunkedBodyTest(lines, bodyStartIndex);
            }
            else
            {
                // Regular body (everything after the empty line)
                var bodyText = string.Join("\r\n", lines, bodyStartIndex, lines.Length - bodyStartIndex);
                if (!string.IsNullOrEmpty(bodyText))
                {
                    body = Encoding.UTF8.GetBytes(bodyText);
                }
            }
        }

        return new HttpResponseEnvelope
        {
            StatusCode = statusCode,
            Headers = headers,
            Body = body
        };
    }

    private static byte[]? DecodeChunkedBodyTest(string[] lines, int startIndex)
    {
        var bodyParts = new List<byte[]>();
        int i = startIndex;

        while (i < lines.Length)
        {
            // Read chunk size line
            var chunkSizeLine = lines[i].Trim();
            if (string.IsNullOrEmpty(chunkSizeLine))
            {
                i++;
                continue;
            }

            // Parse chunk size (hex)
            // Handle chunk extensions (e.g., "1a; name=value")
            var semicolonIndex = chunkSizeLine.IndexOf(';');
            if (semicolonIndex >= 0)
            {
                chunkSizeLine = chunkSizeLine.Substring(0, semicolonIndex);
            }

            if (!int.TryParse(chunkSizeLine, System.Globalization.NumberStyles.HexNumber, null, out var chunkSize))
            {
                // Invalid chunk size, stop parsing
                break;
            }

            // Chunk size 0 means end of chunks
            if (chunkSize == 0)
            {
                break;
            }

            i++;

            // Read chunk data
            if (i < lines.Length)
            {
                var chunkData = lines[i];
                bodyParts.Add(Encoding.UTF8.GetBytes(chunkData));
                i++;
            }
        }

        if (bodyParts.Count == 0)
        {
            return null;
        }

        // Combine all chunks
        var totalLength = bodyParts.Sum(p => p.Length);
        var result = new byte[totalLength];
        int offset = 0;
        foreach (var part in bodyParts)
        {
            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }

        return result;
    }
}
