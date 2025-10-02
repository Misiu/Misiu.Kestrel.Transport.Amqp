# Misiu.Kestrel.Transport.Amqp

[![CI](https://github.com/Misiu/Misiu.Kestrel.Transport.Amqp/actions/workflows/ci.yml/badge.svg)](https://github.com/Misiu/Misiu.Kestrel.Transport.Amqp/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Misiu.Kestrel.Transport.Amqp.svg)](https://www.nuget.org/packages/Misiu.Kestrel.Transport.Amqp/)

AMQP Transport for Kestrel server targeting .NET 9.

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

```csharp
using Misiu.Kestrel.Transport.Amqp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AmqpTransportOptions>(options =>
{
    options.ConnectionString = "amqp://localhost";
    options.MaxMessageSize = 65536;
});

var app = builder.Build();
app.Run();
```

## License

MIT