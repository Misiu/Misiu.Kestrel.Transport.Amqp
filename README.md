# Misiu.Kestrel.Transport.Amqp

[![CI](https://github.com/Misiu/Misiu.Kestrel.Transport.Amqp/actions/workflows/ci.yml/badge.svg)](https://github.com/Misiu/Misiu.Kestrel.Transport.Amqp/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Misiu.Kestrel.Transport.Amqp.svg)](https://www.nuget.org/packages/Misiu.Kestrel.Transport.Amqp/)

AMQP Gateway for ASP.NET Core targeting .NET 9.

This library provides a reverse proxy over AMQP/RabbitMQ that enables you to expose internal APIs (behind firewalls, on mobile networks, etc.) through a public HTTP gateway. Perfect for scenarios where the backend API cannot be directly accessed due to network restrictions.

## Structure

- `src/` - Source code for the library
- `samples/` - Sample applications demonstrating usage
  - `Sample.Server` - Sample server application
  - `Sample.Client` - Sample client application

## Building

```bash
dotnet restore
dotnet build
```

## Installation

```bash
dotnet add package Misiu.Kestrel.Transport.Amqp
```

## Architecture

```
[Internet] → [Public Gateway Server] ←AMQP→ [Client Behind Firewall] → [Local API]
```

**Gateway Server (Public)**:
- Receives normal HTTP requests from the internet
- Serializes requests and sends them via AMQP/RabbitMQ
- Waits for responses (with timeout)
- Returns responses to original HTTP callers
- If timeout occurs, returns 202 Accepted with correlation ID for later retrieval

**Client (Internal/Behind Firewall)**:
- Runs behind firewall, NAT, or mobile network (no static IP needed)
- Consumes requests from AMQP queue
- Forwards to local HTTP API
- Returns responses via AMQP

## Usage

### Gateway Server (Public)

Deploy this on a publicly accessible server:

```csharp
using Misiu.Kestrel.Transport.Amqp;

var builder = WebApplication.CreateBuilder(args);

// Configure AMQP Gateway
builder.Services.AddAmqpGateway(options =>
{
    options.HostName = "your-rabbitmq-server.com";
    options.Port = 5672;
    options.UserName = "guest";
    options.Password = "guest";
    options.RequestQueue = "amqp.gateway.requests";
    options.ResponseQueue = "amqp.gateway.responses";
    options.ImmediateTimeoutSeconds = 3;
    options.ResultTtlMinutes = 15;
});

var app = builder.Build();

// Map endpoint to retrieve delayed results
app.MapAmqpResultEndpoint();

// Forward all requests to AMQP
app.UseAmqpGateway();

app.Run();
```

### Client (Behind Firewall)

Run this on your internal network:

```csharp
using Misiu.Kestrel.Transport.Amqp;

var builder = Host.CreateApplicationBuilder(args);

// Configure AMQP Client
builder.Services.AddAmqpClient(options =>
{
    options.HostName = "your-rabbitmq-server.com";
    options.Port = 5672;
    options.UserName = "guest";
    options.Password = "guest";
    options.RequestQueue = "amqp.gateway.requests";
    options.ResponseQueue = "amqp.gateway.responses";
    options.LocalApiBaseUrl = "http://localhost:5000"; // Your local API
    options.PrefetchCount = 10;
});

var host = builder.Build();
await host.RunAsync();
```

## Features

- **Expose Internal APIs**: Make APIs behind firewalls/NAT accessible via public gateway
- **No Static IP Required**: Client can run on mobile networks, behind corporate firewalls, etc.
- **Async Processing**: Supports long-running requests with 202 Accepted responses
- **Correlation Tracking**: Built-in request/response matching
- **Connection Resilience**: Automatic recovery and topology preservation
- **Full HTTP Support**: All HTTP methods, headers, and body types

## How It Works

1. **HTTP Request arrives** at public gateway server
2. **Gateway serializes** the request (method, path, headers, body) into JSON
3. **Published to AMQP** queue with correlation ID
4. **Client consumes** from queue (even behind firewall)
5. **Client forwards** to local HTTP API
6. **Response returns** via AMQP with same correlation ID
7. **Gateway returns** response to original HTTP caller

If the client doesn't respond within timeout:
- Gateway returns **202 Accepted** with correlation ID
- Client can retrieve result later via `/amqp/result/{correlationId}`