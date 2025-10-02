# Quick Comparison: Transport vs BackgroundService

## Visual Flow Comparison

### Approach 1: Custom Kestrel Transport

```
[Gateway] → AMQP Queue → [AmqpConnectionListener]
                              ↓
                         [Pipes/Duplex]
                              ↓
                    [Kestrel HTTP Parser]
                              ↓
                      [Your Endpoints]
                              ↓
                       [Raw HTTP/1.1]
                              ↓
                         AMQP Queue → [Gateway]
```

**Key Point**: Treats each AMQP message as a raw TCP-like connection

### Approach 2: BackgroundService + HttpClient

```
[Gateway] → AMQP Queue → [AmqpClientConsumer]
                              ↓
                    [Deserialize Envelope]
                              ↓
                      [Transform Path]
                              ↓
                       [HttpClient]
                              ↓
                   [Your Local API - ANY]
                              ↓
                    [Serialize Envelope]
                              ↓
                         AMQP Queue → [Gateway]
```

**Key Point**: Forwards to any existing HTTP API

---

## Side-by-Side Code Comparison

### Setup: Transport

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAmqpTransport(options =>
{
    options.HostName = "localhost";
    options.RequestQueue = "requests";
    options.ResponseQueue = "responses";
});

builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.ListenAmqp("my-amqp");
});

var app = builder.Build();
app.MapGet("/api/data", () => "Hello!");
app.Run();
```

### Setup: BackgroundService

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAmqpClient(options =>
{
    options.HostName = "localhost";
    options.RequestQueue = "requests";
    options.ResponseQueue = "responses";
    options.LocalApiBaseUrl = "http://localhost:5001";
    
    // Path transformation!
    options.PathPrefixToRemove = "/proxy";
});

await builder.Build().RunAsync();
```

---

## Decision Matrix

### Choose **Transport** When:

| Scenario | Why |
|----------|-----|
| Building new ASP.NET Core API | Native integration |
| Need maximum performance | No HttpClient overhead |
| Want Kestrel's HTTP parsing | Best-in-class parser |
| Endpoints in same app | Direct routing |

### Choose **BackgroundService** When:

| Scenario | Why |
|----------|-----|
| Forwarding to existing API | Works with anything |
| Need path transformation | Built-in support |
| Want simpler code | Easier to understand |
| Forwarding to non-.NET API | Language agnostic |
| Easier testing needed | Standard HttpClient |

---

## Performance Metrics (Estimated)

| Metric | Transport | BackgroundService |
|--------|-----------|-------------------|
| Latency | ~5ms | ~10ms |
| Throughput | 10,000 req/s | 8,000 req/s |
| Memory | Lower | Slightly higher |
| CPU | Lower | Moderate |

*Actual numbers depend on your specific workload and infrastructure*

---

## Common Questions

### Q: Can I use both?
**A**: Yes! Use Transport for high-performance endpoints and BackgroundService for legacy API forwarding.

### Q: Which is more "production ready"?
**A**: Both are production-ready. BackgroundService is simpler to maintain; Transport is more performant.

### Q: Can I switch later?
**A**: Yes, but requires code changes. Start with BackgroundService if unsure - easier to test and debug.

### Q: Does Transport support path transformation?
**A**: No, it processes raw HTTP/1.1. Use BackgroundService if you need path transformation.

### Q: Can BackgroundService forward to non-.NET APIs?
**A**: Yes! It works with any HTTP API (Node.js, Python, Java, etc.)

---

## Real-World Example

### Scenario: Expose internal company API

**Your Setup:**
- Internal API: ASP.NET Core at http://internal-api:5000
- Gateway: Public server with domain api.company.com
- Network: Internal API behind corporate firewall

**Best Choice: BackgroundService**

Why?
1. Forwards to existing API (no code changes)
2. Simple deployment (just run consumer)
3. Easy to test and debug
4. Path transformation if needed

```csharp
// On machine behind firewall
builder.Services.AddAmqpClient(options =>
{
    options.LocalApiBaseUrl = "http://internal-api:5000";
    options.RequestQueue = "company.requests";
    options.ResponseQueue = "company.responses";
});
```

### Scenario: New high-performance microservice

**Your Setup:**
- Building new API specifically for AMQP gateway
- Need maximum throughput
- Want to use Kestrel features

**Best Choice: Custom Transport**

Why?
1. Building from scratch anyway
2. Performance critical
3. Want native Kestrel integration
4. No external API to forward to

```csharp
// New microservice
builder.Services.AddAmqpTransport(/*...*/);
builder.WebHost.ConfigureKestrel(k => k.ListenAmqp("high-perf"));

app.MapGet("/api/data", async (DbContext db) => 
{
    return await db.Data.ToListAsync();
});
```

---

## Testing Strategy

### Transport Testing
```csharp
// Integration test
var factory = new WebApplicationFactory<Program>();
var client = factory.CreateClient();
// Test endpoints normally
```

### BackgroundService Testing
```csharp
// Mock HttpClient
var mockHandler = new Mock<HttpMessageHandler>();
var httpClient = new HttpClient(mockHandler.Object);
// Inject and test consumer
```

---

## Summary

- **Transport**: Performance, Native Kestrel, ASP.NET Core only
- **BackgroundService**: Simplicity, Flexibility, Path transformation

**When in doubt, start with BackgroundService** - it's easier to understand, test, and maintain!
