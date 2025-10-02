# Misiu.Kestrel.Transport.Amqp

[![CI](https://github.com/Misiu/Misiu.Kestrel.Transport.Amqp/actions/workflows/ci.yml/badge.svg)](https://github.com/Misiu/Misiu.Kestrel.Transport.Amqp/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Misiu.Kestrel.Transport.Amqp.svg)](https://www.nuget.org/packages/Misiu.Kestrel.Transport.Amqp/)

AMQP Transport for Kestrel server targeting .NET 9.

This library provides a custom transport for ASP.NET Core Kestrel that allows HTTP requests to be received via RabbitMQ (AMQP) instead of traditional TCP sockets. Inspired by the [Named Pipes transport](https://github.com/dotnet/aspnetcore/tree/main/src/Servers/Kestrel/Transport.NamedPipes) in ASP.NET Core.

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

## Usage

### Server

Configure your ASP.NET Core application to use AMQP transport:

```csharp
using Misiu.Kestrel.Transport.Amqp;

var builder = WebApplication.CreateBuilder(args);

// Configure AMQP transport
builder.Services.AddAmqpTransport(options =>
{
    options.HostName = "localhost";
    options.Port = 5672;
    options.UserName = "guest";
    options.Password = "guest";
    options.RequestQueue = "kestrel.amqp.requests";
    options.ResponseQueue = "kestrel.amqp.responses";
});

// Configure Kestrel to use AMQP transport alongside HTTP
builder.WebHost.ConfigureKestrel(kestrel =>
{
    // Standard HTTP endpoint
    kestrel.ListenLocalhost(5000);
    
    // AMQP endpoint
    kestrel.ListenAmqp("amqp-transport");
});

var app = builder.Build();

app.MapGet("/", () => "Hello from AMQP transport!");
app.Run();
```

### Client

Send HTTP requests via RabbitMQ:

```csharp
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

// Connect to RabbitMQ
var factory = new ConnectionFactory { HostName = "localhost" };
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

// Build HTTP request envelope
var request = new
{
    method = "GET",
    pathAndQuery = "/api/endpoint",
    headers = new Dictionary<string, string[]>
    {
        ["Accept"] = new[] { "application/json" }
    }
};

var requestJson = JsonSerializer.Serialize(request);
var requestBytes = Encoding.UTF8.GetBytes(requestJson);

// Send request
var props = channel.CreateBasicProperties();
props.CorrelationId = Guid.NewGuid().ToString();
channel.BasicPublish("", "kestrel.amqp.requests", props, requestBytes);

// Listen for response on kestrel.amqp.responses queue
```

## Features

- **Dual Transport Support**: Run HTTP and AMQP transports side-by-side
- **ASP.NET Core Integration**: Full support for middleware, routing, and minimal APIs
- **Request Serialization**: Automatic HTTP request/response serialization
- **Correlation IDs**: Built-in request tracking
- **Connection Resilience**: Automatic recovery and topology preservation

## How It Works

The transport implements a custom Kestrel connection listener that:

1. Consumes messages from a RabbitMQ request queue
2. Deserializes JSON envelopes into raw HTTP/1.1 requests
3. Feeds them through the standard Kestrel HTTP pipeline
4. Serializes HTTP responses and publishes them to a response queue
5. Uses correlation IDs to match requests and responses