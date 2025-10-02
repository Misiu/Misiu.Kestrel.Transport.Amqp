# Implementation Approaches

This library provides **two different approaches** for implementing the AMQP client that consumes requests from the gateway and forwards them to your local API.

## Approach 1: Custom Kestrel Transport (IConnectionListener)

### Overview
Uses a custom Kestrel transport that treats AMQP messages as raw HTTP connections. Each AMQP message becomes a synthetic `ConnectionContext` that Kestrel's HTTP parser processes natively.

### How It Works
1. `AmqpConnectionListener` implements `IConnectionListener`
2. Consumes messages from AMQP queue
3. Creates `ConnectionContext` with raw HTTP/1.1 bytes
4. Kestrel's HTTP parser handles the request
5. Response flows back through the same connection context
6. Published to AMQP response queue

### Pros
- ✅ **Direct Kestrel Integration**: Uses Kestrel's native HTTP/1.1 parser
- ✅ **Lower Overhead**: No HttpClient involved
- ✅ **Better Performance**: Fewer serialization/deserialization steps
- ✅ **Native ASP.NET Core**: Requests flow through normal middleware pipeline
- ✅ **Connection-like Semantics**: Each request is treated as a real connection

### Cons
- ❌ **More Complex**: Requires understanding of Kestrel internals (pipes, ConnectionContext)
- ❌ **Tightly Coupled**: Depends on Kestrel's connection abstractions
- ❌ **Server-Side Only**: Designed for hosting endpoints, not forwarding
- ❌ **Harder to Debug**: Lower-level abstractions make troubleshooting more difficult

### Code Example

```csharp
using Misiu.Kestrel.Transport.Amqp;

var builder = WebApplication.CreateBuilder(args);

// Register AMQP transport
builder.Services.AddAmqpTransport(options =>
{
    options.HostName = "localhost";
    options.Port = 5672;
    options.RequestQueue = "amqp.gateway.requests";
    options.ResponseQueue = "amqp.gateway.responses";
});

// Configure Kestrel to listen on AMQP
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.ListenAmqp("amqp-client");
});

var app = builder.Build();

// Your endpoints - accessible via AMQP
app.MapGet("/api/data", () => new { data = "Hello!" });
app.MapPost("/api/echo", async (HttpRequest req) => { /* ... */ });

app.Run();
```

### When to Use
- You want **maximum performance**
- Your API is built with ASP.NET Core
- You want to use Kestrel's native HTTP parsing
- You're comfortable with lower-level abstractions
- Your API endpoints are defined in the same application

### Sample
See `samples/Sample.ClientTransport/`

---

## Approach 2: Background Service + HttpClient

### Overview
Uses a background service (IHostedService) that consumes AMQP messages, deserializes them into HTTP requests, and forwards them to a local API using HttpClient.

### How It Works
1. `AmqpClientConsumer` runs as a background service
2. Consumes messages from AMQP queue
3. Deserializes JSON envelope to HTTP request details
4. Creates HttpRequestMessage
5. Forwards to local API via HttpClient
6. Serializes response back to JSON envelope
7. Published to AMQP response queue

### Pros
- ✅ **Simple and Maintainable**: Standard HttpClient usage
- ✅ **Flexible**: Can forward to any HTTP API (doesn't need to be ASP.NET Core)
- ✅ **Easy to Test**: Straightforward mocking and testing
- ✅ **Path Transformation**: Built-in support for path prefix manipulation
- ✅ **Decoupled**: Independent of the target API implementation
- ✅ **Easier to Debug**: Higher-level abstractions, clear request/response flow

### Cons
- ❌ **HttpClient Overhead**: Extra network hop and serialization
- ❌ **Extra Serialization**: Request → JSON → HttpClient → Local API
- ❌ **Not Using Kestrel Parser**: Custom HTTP envelope instead of raw HTTP/1.1

### Code Example

```csharp
using Misiu.Kestrel.Transport.Amqp;

var builder = Host.CreateApplicationBuilder(args);

// Register AMQP client background service
builder.Services.AddAmqpClient(options =>
{
    options.HostName = "localhost";
    options.Port = 5672;
    options.RequestQueue = "amqp.gateway.requests";
    options.ResponseQueue = "amqp.gateway.responses";
    options.LocalApiBaseUrl = "http://localhost:5001";
    options.PrefetchCount = 10;
    
    // Path transformation (optional)
    options.PathPrefixToRemove = "/proxy"; // Remove this prefix
    options.PathPrefixToAdd = "/api/v1";   // Add this prefix
});

var host = builder.Build();
await host.RunAsync();
```

### When to Use
- You want **simplicity and maintainability**
- You're forwarding to an existing API (any technology)
- You want **easier debugging** and testing
- Your local API is separate from the consumer application
- You can forward to the same API (self-referencing) by setting LocalApiBaseUrl to your app's address

### Sample
See `samples/Sample.ClientBackgroundService/`

---

## Comparison Table

| Feature | Custom Transport | BackgroundService |
|---------|-----------------|-------------------|
| Performance | ⭐⭐⭐⭐⭐ Best | ⭐⭐⭐⭐ Good |
| Simplicity | ⭐⭐ Complex | ⭐⭐⭐⭐⭐ Very Simple |
| Flexibility | ⭐⭐ ASP.NET only | ⭐⭐⭐⭐⭐ Any HTTP API |
| Path Transform | ✅ Yes (server-side) | ✅ Yes (server-side) |
| Debugging | ⭐⭐ Harder | ⭐⭐⭐⭐⭐ Easier |
| Testing | ⭐⭐⭐ Moderate | ⭐⭐⭐⭐⭐ Easy |
| HTTP Parsing | Kestrel native | HttpClient |
| Setup Complexity | ⭐⭐⭐ Moderate | ⭐⭐⭐⭐⭐ Very Easy |
| Self-Referencing | ❌ No | ✅ Yes |

---

## Path Transformation

**Important**: Path transformation is configured on the **SERVER** (gateway) side, not on the client.

Both approaches support path transformation because the gateway transforms the path before sending to AMQP, and both clients receive the already-transformed path.

### Configuration (Server-side)

```csharp
// In Sample.Server
builder.Services.AddAmqpGateway(options =>
{
    // Remove prefix from incoming requests
    options.PathPrefixToRemove = "/proxy";
    
    // Optionally add a prefix
    // options.PathPrefixToAdd = "/api/v1";
});
```

### Example Flow

1. **Client sends**: `GET /proxy/name`
2. **Gateway transforms**: `/proxy/name` → `/name`
3. **Sent via AMQP**: `GET /name`
4. **Client receives**: `GET /name`
5. **Forwards to local API**: `http://localhost:5001/name`
6. **Local API has endpoint**: `/name` → returns data
7. **Response flows back**: Data → Client → AMQP → Gateway → Original caller

If the endpoint doesn't exist (e.g., `/proxy/non-existing` → `/non-existing`), the local API returns 404, which propagates back through the chain.

### Self-Referencing with BackgroundService

The BackgroundService approach can forward to itself:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add AMQP client that forwards to this same app
builder.Services.AddAmqpClient(options =>
{
    options.LocalApiBaseUrl = "http://localhost:5000"; // This app's address
    // ... other options
});

var app = builder.Build();

// Define API endpoints
app.MapGet("/name", () => "John Doe");
app.MapGet("/hello", () => "Hello World");

app.Run("http://localhost:5000");
```

The app consumes AMQP messages and forwards them to itself via HttpClient. This works because:
- The AMQP consumer runs in a background thread
- HttpClient makes a separate HTTP request back to the app
- The app processes the request through its normal pipeline

---

## Which Should You Choose?

### Choose **Custom Transport** if:
- Performance is critical
- You're building a new ASP.NET Core API specifically for AMQP
- You want native Kestrel HTTP/1.1 parsing
- You're comfortable with advanced Kestrel concepts

### Choose **BackgroundService** if:
- You're forwarding to an existing API
- Simplicity and maintainability are priorities
- You want easier testing and debugging
- Your API isn't ASP.NET Core (or you don't want tight coupling)
- You need self-referencing (API forwarding to itself)

---

## Hybrid Approach

You can also use **both** approaches simultaneously:

- **Transport**: For high-performance endpoints you define directly
- **BackgroundService**: For forwarding to existing legacy APIs

This gives you the best of both worlds!

---

## Performance Considerations

### Custom Transport
- Raw HTTP/1.1 bytes → Kestrel parser → Response
- No HttpClient overhead
- Single serialization step (response to AMQP)
- Best for high-throughput scenarios

### BackgroundService
- JSON envelope → HttpClient → Local API → JSON envelope
- HttpClient connection pooling helps
- Two serialization steps
- Good enough for most use cases
- Path transformation adds negligible overhead

### Benchmark Results (Coming Soon)
We'll add comprehensive benchmarks to help you make an informed decision.

---

## Migration Path

**Starting with BackgroundService?** Easy to migrate later:
1. Keep BackgroundService for legacy API forwarding
2. Add Custom Transport for new high-performance endpoints
3. Route requests accordingly at the gateway level

**Starting with Custom Transport?** Can add BackgroundService:
1. Keep Custom Transport for main API
2. Add BackgroundService for auxiliary services
3. Use different queue names for routing
