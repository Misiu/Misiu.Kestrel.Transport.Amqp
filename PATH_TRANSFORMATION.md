# Path Transformation Guide

## Overview

Path transformation is configured on the **SERVER** (gateway) side and applies to both client approaches (Transport and BackgroundService).

## Configuration

### Server (Gateway)

```csharp
// Sample.Server/Program.cs
builder.Services.AddAmqpGateway(options =>
{
    options.HostName = "localhost";
    options.Port = 5672;
    options.RequestQueue = "amqp.gateway.requests";
    options.ResponseQueue = "amqp.gateway.responses";
    
    // Path transformation configuration
    options.PathPrefixToRemove = "/proxy";  // Optional
    options.PathPrefixToAdd = "/api/v1";    // Optional
});
```

### Client (Both Approaches)

No configuration needed! Clients receive already-transformed paths.

```csharp
// Client just needs to know where to forward
options.LocalApiBaseUrl = "http://localhost:5001";
```

## Example Scenarios

### Scenario 1: Remove Prefix

**Gateway Configuration:**
```csharp
options.PathPrefixToRemove = "/proxy";
```

**Request Flow:**
1. External client → `GET https://api.example.com/proxy/name`
2. Gateway transforms → `/proxy/name` → `/name`
3. Sent via AMQP → `GET /name`
4. Client receives → `GET /name`
5. Forwards to → `http://localhost:5001/name`
6. Local API endpoint → `/name` returns `"John Doe"`
7. Response flows back → `"John Doe"` → Client → AMQP → Gateway → External client

### Scenario 2: Add Prefix

**Gateway Configuration:**
```csharp
options.PathPrefixToAdd = "/api/v1";
```

**Request Flow:**
1. External client → `GET https://api.example.com/users`
2. Gateway transforms → `/users` → `/api/v1/users`
3. Sent via AMQP → `GET /api/v1/users`
4. Client receives → `GET /api/v1/users`
5. Forwards to → `http://localhost:5001/api/v1/users`
6. Local API endpoint → `/api/v1/users` returns user list
7. Response flows back

### Scenario 3: Remove and Add (Chained)

**Gateway Configuration:**
```csharp
options.PathPrefixToRemove = "/proxy";
options.PathPrefixToAdd = "/internal/api";
```

**Request Flow:**
1. External client → `GET https://api.example.com/proxy/data`
2. Gateway transforms:
   - Step 1: `/proxy/data` → `/data` (remove `/proxy`)
   - Step 2: `/data` → `/internal/api/data` (add `/internal/api`)
3. Sent via AMQP → `GET /internal/api/data`
4. Client forwards to → `http://localhost:5001/internal/api/data`

### Scenario 4: 404 Handling

**Gateway Configuration:**
```csharp
options.PathPrefixToRemove = "/proxy";
```

**Request Flow:**
1. External client → `GET https://api.example.com/proxy/non-existing`
2. Gateway transforms → `/proxy/non-existing` → `/non-existing`
3. Sent via AMQP → `GET /non-existing`
4. Client forwards to → `http://localhost:5001/non-existing`
5. Local API → 404 Not Found (endpoint doesn't exist)
6. Response flows back → 404 → Client → AMQP → Gateway → External client (404)

## Why Server-Side?

Server-side transformation has several advantages:

1. **Centralized Control**: Gateway controls routing for all clients
2. **Client Simplicity**: Clients don't need transformation logic
3. **Consistency**: All clients get the same transformed paths
4. **Security**: Gateway can filter/rewrite paths before forwarding
5. **Routing**: Gateway acts as a router, directing traffic appropriately

## Client Types

### Transport Approach

The Transport approach receives the envelope with the already-transformed path and constructs raw HTTP/1.1:

```
GET /name HTTP/1.1
Host: amqp-transport
...
```

Kestrel parses this and routes to your endpoints.

### BackgroundService Approach

The BackgroundService receives the envelope with the already-transformed path and creates an HttpRequestMessage:

```csharp
// Path already transformed by gateway
var request = new HttpRequestMessage(HttpMethod.Get, "/name");
await _httpClient.SendAsync(request);
```

## Self-Referencing (BackgroundService Only)

The BackgroundService approach can forward requests to itself:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add AMQP client
builder.Services.AddAmqpClient(options =>
{
    options.LocalApiBaseUrl = "http://localhost:5000"; // This app
    // ...
});

var app = builder.Build();

// API endpoints
app.MapGet("/name", () => "John Doe");
app.MapGet("/hello", () => "Hello World");

app.Run("http://localhost:5000");
```

**How it works:**
1. AMQP consumer runs in background thread
2. Receives message with path `/name`
3. HttpClient makes HTTP request to `http://localhost:5000/name`
4. App processes request through normal pipeline
5. Returns response to HttpClient
6. Consumer sends response back via AMQP

**Why Transport can't self-reference:**
- Transport integrates directly with Kestrel's connection pipeline
- Can't create HTTP request to itself (circular dependency)
- Designed for hosting endpoints, not forwarding

## Testing Path Transformation

### Test 1: Basic Remove

```bash
# Gateway configured with PathPrefixToRemove = "/proxy"

curl https://api.example.com/proxy/name
# Gateway sends: GET /name
# Response: "John Doe"
```

### Test 2: Non-Existing Endpoint

```bash
# Gateway configured with PathPrefixToRemove = "/proxy"

curl -i https://api.example.com/proxy/invalid
# Gateway sends: GET /invalid
# Response: 404 Not Found
```

### Test 3: Complex Path

```bash
# Gateway configured with PathPrefixToRemove = "/proxy"

curl https://api.example.com/proxy/api/users?page=1&limit=10
# Gateway sends: GET /api/users?page=1&limit=10
# Query parameters preserved
```

## Common Patterns

### Pattern 1: Proxy Prefix

Use case: Route specific paths through gateway

```csharp
options.PathPrefixToRemove = "/proxy";
```

- `/proxy/name` → `/name`
- `/proxy/users` → `/users`
- `/proxy/api/data` → `/api/data`

### Pattern 2: API Versioning

Use case: Add version prefix to all requests

```csharp
options.PathPrefixToAdd = "/api/v1";
```

- `/users` → `/api/v1/users`
- `/data` → `/api/v1/data`

### Pattern 3: Namespace Routing

Use case: Route to specific namespace in local API

```csharp
options.PathPrefixToRemove = "/external";
options.PathPrefixToAdd = "/internal";
```

- `/external/users` → `/internal/users`
- `/external/data` → `/internal/data`

## Edge Cases

### Empty Path

```csharp
options.PathPrefixToRemove = "/proxy";
```

- `/proxy` → `/` (root path)
- `/proxy/` → `/` (root path)

### Multiple Slashes

```csharp
options.PathPrefixToRemove = "/proxy";
```

- `/proxy//name` → `/name` (normalized)

### Case Sensitivity

Path prefix matching is **case-insensitive**:

```csharp
options.PathPrefixToRemove = "/proxy";
```

- `/proxy/name` → `/name` ✅
- `/Proxy/name` → `/name` ✅
- `/PROXY/name` → `/name` ✅

## Limitations

1. **No Pattern Matching**: Only exact prefix matching, no wildcards or regex
2. **No Route-Specific Transformation**: Same transformation applies to all routes
3. **No Request Inspection**: Transformation based on path only, not headers/body

For more advanced routing needs, consider implementing custom middleware.
