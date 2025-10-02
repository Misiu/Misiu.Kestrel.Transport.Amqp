# Usage Example

This example shows how to expose a local API that's behind a firewall through a public gateway.

## Scenario

- **Local API**: Running at `http://localhost:5001` behind a firewall (no public IP)
- **Gateway Server**: Publicly accessible at `https://api.example.com`
- **RabbitMQ**: Running at `rabbitmq.example.com`

## Setup

### 1. Start RabbitMQ

```bash
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

### 2. Run Your Local API

This is your existing API that you want to expose:

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/api/data", () => new { data = "Hello from local API!" });
app.MapPost("/api/echo", async (HttpContext ctx) => 
{
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    return Results.Ok(new { received = body });
});

app.Run("http://localhost:5001");
```

### 3. Run the Gateway Server (Public)

Deploy this to your public server:

```csharp
using Misiu.Kestrel.Transport.Amqp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAmqpGateway(options =>
{
    options.HostName = "rabbitmq.example.com";
    options.Port = 5672;
    options.UserName = "user";
    options.Password = "password";
    options.RequestQueue = "amqp.gateway.requests";
    options.ResponseQueue = "amqp.gateway.responses";
    options.ImmediateTimeoutSeconds = 5;  // Wait 5 seconds for response
    options.ResultTtlMinutes = 15;        // Cache results for 15 minutes
});

var app = builder.Build();

// Endpoint to retrieve delayed results
app.MapAmqpResultEndpoint();

// Forward all requests to AMQP
app.UseAmqpGateway();

app.Run("https://api.example.com");
```

### 4. Run the Client (Behind Firewall)

Run this on the same network as your local API:

```csharp
using Misiu.Kestrel.Transport.Amqp;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAmqpClient(options =>
{
    options.HostName = "rabbitmq.example.com";
    options.Port = 5672;
    options.UserName = "user";
    options.Password = "password";
    options.RequestQueue = "amqp.gateway.requests";
    options.ResponseQueue = "amqp.gateway.responses";
    options.LocalApiBaseUrl = "http://localhost:5001";  // Your local API
    options.PrefetchCount = 10;
});

var host = builder.Build();
await host.RunAsync();
```

## Usage

### Fast Response (< 5 seconds)

```bash
curl https://api.example.com/api/data
# Returns: {"data":"Hello from local API!"}
```

### Slow Response (> 5 seconds)

If your API takes longer than the timeout:

```bash
curl https://api.example.com/api/slow-operation
# Returns: 202 Accepted
# {
#   "correlationId": "550e8400-e29b-41d4-a716-446655440000",
#   "status": "accepted",
#   "message": "Request is being processed. Check Location header for result.",
#   "location": "/amqp/result/550e8400-e29b-41d4-a716-446655440000"
# }

# Retrieve the result later
curl https://api.example.com/amqp/result/550e8400-e29b-41d4-a716-446655440000
# Returns the actual result when processing is complete
```

### POST Requests

```bash
curl -X POST https://api.example.com/api/echo \
  -H "Content-Type: application/json" \
  -d '{"message":"Hello"}'
# Returns: {"received":"{\"message\":\"Hello\"}"}
```

## Benefits

1. **No Port Forwarding**: Client doesn't need any incoming ports open
2. **Works Behind NAT**: Client can be on any network with outgoing internet access
3. **Mobile Networks**: Client can run on mobile/cellular connections without static IP
4. **Corporate Firewalls**: Bypasses restrictive firewall policies
5. **Multiple Clients**: Multiple internal APIs can connect to the same gateway
6. **Scalability**: Add more client instances for load distribution

## Monitoring

The gateway adds these headers to responses:
- `X-CorrelationId`: Unique request identifier
- `X-Processing-Time-Ms`: Time taken to process on client side

Use these for debugging and monitoring.
