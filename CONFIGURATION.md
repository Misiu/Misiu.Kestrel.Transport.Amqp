# Configuration Guide

This library supports two configuration approaches:
1. **Programmatic Configuration** - Configure in code
2. **appsettings.json Configuration** - Configure via configuration files

## Programmatic Configuration

### Gateway Server

```csharp
using Misiu.Kestrel.Transport.Amqp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAmqpGateway(options =>
{
    options.HostName = "localhost";
    options.Port = 5672;
    options.UserName = "guest";
    options.Password = "guest";
    options.RequestQueue = "amqp.gateway.requests";
    options.ResponseQueue = "amqp.gateway.responses";
    options.ImmediateTimeoutSeconds = 3;
    options.ResultTtlMinutes = 15;
    options.PathPrefixToRemove = "/proxy";
});

var app = builder.Build();
app.MapAmqpResultEndpoint();
app.UseAmqpGateway();
app.Run();
```

### Client - BackgroundService

```csharp
using Misiu.Kestrel.Transport.Amqp;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAmqpClient(options =>
{
    options.HostName = "localhost";
    options.Port = 5672;
    options.UserName = "guest";
    options.Password = "guest";
    options.RequestQueue = "amqp.gateway.requests";
    options.ResponseQueue = "amqp.gateway.responses";
    options.LocalApiBaseUrl = "http://localhost:5001"; // Optional - auto-detects if empty
    options.PrefetchCount = 10;
});

await builder.Build().RunAsync();
```

### Client - Transport

```csharp
using Misiu.Kestrel.Transport.Amqp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAmqpTransport(options =>
{
    options.HostName = "localhost";
    options.Port = 5672;
    options.UserName = "guest";
    options.Password = "guest";
    options.RequestQueue = "amqp.gateway.requests";
    options.ResponseQueue = "amqp.gateway.responses";
});

builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.ListenAmqp("amqp-client");
});

var app = builder.Build();
app.MapGet("/api/data", () => new { data = "Hello!" });
app.Run();
```

## appsettings.json Configuration

### Gateway Server

**appsettings.json:**
```json
{
  "AmqpGateway": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "RequestQueue": "amqp.gateway.requests",
    "ResponseQueue": "amqp.gateway.responses",
    "ImmediateTimeoutSeconds": 3,
    "ResultTtlMinutes": 15,
    "PathPrefixToRemove": "/proxy"
  }
}
```

**Program.cs:**
```csharp
using Misiu.Kestrel.Transport.Amqp;

var builder = WebApplication.CreateBuilder(args);

// Simple one-liner configuration
builder.Services.AddAmqpGateway(builder.Configuration);

var app = builder.Build();
app.MapAmqpResultEndpoint();
app.UseAmqpGateway();
app.Run();
```

### Client - BackgroundService

**appsettings.json:**
```json
{
  "AmqpClient": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "RequestQueue": "amqp.gateway.requests",
    "ResponseQueue": "amqp.gateway.responses",
    "LocalApiBaseUrl": "http://localhost:5001",
    "PrefetchCount": 10
  }
}
```

**Note:** You can omit `LocalApiBaseUrl` and it will be auto-detected from the application's listening addresses.

**Program.cs:**
```csharp
using Misiu.Kestrel.Transport.Amqp;

var builder = Host.CreateApplicationBuilder(args);

// Simple one-liner configuration
builder.Services.AddAmqpClient(builder.Configuration);

await builder.Build().RunAsync();
```

### Client - Transport

**appsettings.json:**
```json
{
  "AmqpTransport": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "RequestQueue": "amqp.gateway.requests",
    "ResponseQueue": "amqp.gateway.responses"
  }
}
```

**Program.cs:**
```csharp
using Misiu.Kestrel.Transport.Amqp;

var builder = WebApplication.CreateBuilder(args);

// Simple one-liner configuration
builder.Services.AddAmqpTransport(builder.Configuration);

builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.ListenAmqp("amqp-client");
});

var app = builder.Build();
app.MapGet("/api/data", () => new { data = "Hello!" });
app.Run();
```

## Custom Configuration Section Names

You can use custom section names:

```csharp
// Gateway with custom section name
builder.Services.AddAmqpGateway(builder.Configuration, "MyCustomGatewaySection");

// Client with custom section name
builder.Services.AddAmqpClient(builder.Configuration, "MyCustomClientSection");

// Transport with custom section name
builder.Services.AddAmqpTransport(builder.Configuration, "MyCustomTransportSection");
```

## Environment-Specific Configuration

Use standard ASP.NET Core environment-specific configuration:

**appsettings.Development.json:**
```json
{
  "AmqpGateway": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest"
  }
}
```

**appsettings.Production.json:**
```json
{
  "AmqpGateway": {
    "HostName": "rabbitmq.production.com",
    "Port": 5672,
    "UserName": "prod_user",
    "Password": "prod_password"
  }
}
```

## Command-Line Override

You can override any setting via command line:

```bash
# Override hostname
dotnet run --AmqpGateway:HostName=rabbitmq.example.com

# Override port
dotnet run --AmqpClient:Port=5673

# Override LocalApiBaseUrl (or omit for auto-detection)
dotnet run --AmqpClient:LocalApiBaseUrl=http://localhost:8080
```

## Auto-Detection of LocalApiBaseUrl

The BackgroundService approach can automatically detect the application's listening addresses:

**When LocalApiBaseUrl is not specified:**
1. The application starts and begins listening on configured ports
2. The BackgroundService waits 1 second for the server to start
3. It reads the server's listening addresses
4. It uses the first available address as the base URL

**Example:**

```bash
# Run your API on port 8080
dotnet run --urls=http://localhost:8080

# The BackgroundService will auto-detect http://localhost:8080
```

This is particularly useful when:
- Running from command line with different ports
- Using dynamic port assignment
- Testing on different environments
- Running multiple instances on different ports

**Benefits:**
- No need to recompile when changing ports
- No need to update configuration files
- Flexible deployment scenarios
- Works with `--urls` command-line argument

## All Configuration Options

### AmqpTransportOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| HostName | string | "localhost" | RabbitMQ hostname |
| Port | int | 5672 | RabbitMQ port |
| VirtualHost | string | "/" | RabbitMQ virtual host |
| UserName | string | "guest" | RabbitMQ username |
| Password | string | "guest" | RabbitMQ password |
| RequestQueue | string | "amqp.gateway.requests" | Request queue name |
| ResponseQueue | string | "amqp.gateway.responses" | Response queue name |
| Persistent | bool | true | Message persistence |
| PrefetchCount | ushort | 32 | Message prefetch count |
| MaxRequestBodyBytes | long | 10,000,000 | Max request body size |
| ImmediateTimeoutSeconds | int | 3 | Gateway timeout for immediate response |
| ResultTtlMinutes | int | 15 | Gateway result cache TTL |
| LocalApiBaseUrl | string | "http://localhost:5000" | Client local API URL (auto-detects if empty) |
| PathPrefixToRemove | string? | null | Gateway path prefix to remove |
| PathPrefixToAdd | string? | null | Gateway path prefix to add |

## Best Practices

1. **Use appsettings.json for configuration** - Easier to manage and change without recompilation
2. **Use environment-specific files** - Different settings for dev/prod
3. **Store sensitive data securely** - Use Azure Key Vault, user secrets, or environment variables
4. **Omit LocalApiBaseUrl** - Let auto-detection handle it for maximum flexibility
5. **Use command-line overrides** - For testing and development

## Examples

### Development Setup

```json
{
  "AmqpGateway": {
    "HostName": "localhost",
    "PathPrefixToRemove": "/proxy"
  }
}
```

```bash
# Run with defaults
dotnet run
```

### Production Setup

```json
{
  "AmqpGateway": {
    "HostName": "rabbitmq.prod.com",
    "UserName": "prod_user",
    "Password": "${RABBITMQ_PASSWORD}",
    "ImmediateTimeoutSeconds": 5,
    "PathPrefixToRemove": "/api"
  }
}
```

### Testing Different Ports

```bash
# Test on port 8080
dotnet run --urls=http://localhost:8080

# Test on port 9000
dotnet run --urls=http://localhost:9000

# No need to change LocalApiBaseUrl - auto-detected!
```
