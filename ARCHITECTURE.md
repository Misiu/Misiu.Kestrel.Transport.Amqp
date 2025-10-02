# Architecture

## Overview

The AMQP Gateway enables you to expose internal APIs (behind firewalls, NAT, or mobile networks) through a public HTTP gateway using RabbitMQ as the message broker.

## Request Flow

```
┌─────────────┐
│   Internet  │
│   Client    │
└──────┬──────┘
       │ 1. HTTP Request (GET /api/data)
       │
       ▼
┌─────────────────────────────────────────────┐
│         Public Gateway Server               │
│  ┌─────────────────────────────────────┐   │
│  │   AmqpGatewayMiddleware             │   │
│  │  - Captures HTTP Request            │   │
│  │  - Serializes to JSON envelope      │   │
│  │  - Generates Correlation ID         │   │
│  └──────────────┬──────────────────────┘   │
└─────────────────┼──────────────────────────┘
                  │ 2. Publish to AMQP
                  │    (amqp.gateway.requests)
                  ▼
         ┌────────────────┐
         │   RabbitMQ     │
         │   (Message     │
         │    Broker)     │
         └────────┬───────┘
                  │ 3. Consume from queue
                  │
         ┌────────▼────────────────────────────┐
         │  Client (Behind Firewall/NAT)       │
         │  ┌──────────────────────────────┐   │
         │  │  AmqpClientConsumer          │   │
         │  │  - Consumes from AMQP        │   │
         │  │  - Deserializes envelope     │   │
         │  │  - Creates HttpRequest       │   │
         │  └──────────┬───────────────────┘   │
         └─────────────┼───────────────────────┘
                       │ 4. Forward HTTP Request
                       │
                  ┌────▼─────────┐
                  │   Local API  │
                  │ (localhost)  │
                  └────┬─────────┘
                       │ 5. HTTP Response
                       │
         ┌─────────────▼───────────────────────┐
         │  Client                              │
         │  ┌──────────────────────────────┐   │
         │  │  AmqpClientConsumer          │   │
         │  │  - Serializes response       │   │
         │  │  - Publishes to AMQP         │   │
         │  └──────────┬───────────────────┘   │
         └─────────────┼───────────────────────┘
                       │ 6. Publish response
                       │    (amqp.gateway.responses)
                       ▼
              ┌────────────────┐
              │   RabbitMQ     │
              └────────┬───────┘
                       │ 7. Consume response
                       │
┌──────────────────────▼──────────────────────┐
│         Public Gateway Server               │
│  ┌─────────────────────────────────────┐   │
│  │   AmqpGatewayMiddleware             │   │
│  │  - Matches Correlation ID           │   │
│  │  - Deserializes response            │   │
│  │  - Returns to HTTP caller           │   │
│  └─────────────────────────────────────┘   │
└──────────────┬──────────────────────────────┘
               │ 8. HTTP Response
               ▼
        ┌─────────────┐
        │   Internet  │
        │   Client    │
        └─────────────┘
```

## Components

### Gateway Server (Public)

**Purpose**: Accept HTTP requests from the internet and forward them to internal APIs via AMQP.

**Key Components**:
- `AmqpGatewayMiddleware`: ASP.NET Core middleware that intercepts all HTTP requests
- Response consumer: Background process that listens for responses
- Result cache: Stores responses for delayed retrieval

**Behavior**:
- If response arrives within timeout (default 3s): Returns immediately
- If response takes longer: Returns 202 Accepted with correlation ID
- Caches results for configurable period (default 15 minutes)

### Client (Internal)

**Purpose**: Consume requests from AMQP and forward to local HTTP API.

**Key Components**:
- `AmqpClientConsumer`: Background service (IHostedService)
- HttpClient: Configured to forward to local API
- Request processor: Deserializes, forwards, and serializes responses

**Benefits**:
- No incoming ports required
- Works behind NAT/firewall
- Can run on mobile networks
- No static IP needed

## Message Format

### Request Envelope

```json
{
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "method": "POST",
  "pathAndQuery": "/api/data?id=123",
  "headers": {
    "Content-Type": ["application/json"],
    "Authorization": ["Bearer token123"]
  },
  "body": "base64-encoded-bytes",
  "contentType": "application/json",
  "gatewayEnqueuedAtUtc": "2024-10-02T10:30:00Z"
}
```

### Response Envelope

```json
{
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "statusCode": 200,
  "headers": {
    "Content-Type": ["application/json"]
  },
  "body": "base64-encoded-bytes",
  "contentType": "application/json",
  "serverStartedAtUtc": "2024-10-02T10:30:01Z",
  "serverCompletedAtUtc": "2024-10-02T10:30:02Z",
  "processingMilliseconds": 1200,
  "gatewayEnqueuedAtUtc": "2024-10-02T10:30:00Z"
}
```

## Timeout Handling

### Fast Response (< timeout)

```
Client → Gateway → (wait 3s) → Response → Client
         (200 OK with data)
```

### Slow Response (> timeout)

```
Client → Gateway → (timeout) → 202 Accepted
                               (with correlation ID)

Later:
Client → Gateway → /amqp/result/{id} → Response
                   (200 OK with data)
```

## Security Considerations

1. **Gateway Server**:
   - Use HTTPS in production
   - Implement authentication/authorization
   - Rate limiting recommended
   - Validate request sizes

2. **RabbitMQ**:
   - Use TLS for AMQP connections
   - Strong credentials
   - Network isolation
   - Enable authentication

3. **Client**:
   - Validate requests before forwarding
   - Implement request timeouts
   - Log all forwarded requests
   - Consider IP whitelisting on local API

## Scalability

### Horizontal Scaling

- **Gateway**: Multiple instances behind load balancer
- **Client**: Multiple instances for load distribution
- **RabbitMQ**: Clustered for high availability

### Vertical Scaling

- Increase `PrefetchCount` for more concurrent processing
- Adjust timeout values based on API response times
- Configure result cache size based on request volume

## Monitoring

### Metrics to Track

1. **Request Volume**: Requests per second through gateway
2. **Response Times**: Time from gateway to client response
3. **Timeout Rate**: Percentage of requests exceeding timeout
4. **Queue Depth**: Number of pending messages in RabbitMQ
5. **Client Health**: Client connection status and processing rate

### Headers Added by Gateway

- `X-CorrelationId`: Unique identifier for request tracking
- `X-Processing-Time-Ms`: Processing time on client side
- `Location`: URL for result retrieval (in 202 responses)

## Use Cases

1. **Mobile Backend**: Expose API running on mobile device/cellular network
2. **IoT Gateway**: Expose APIs on edge devices behind NAT
3. **Corporate Firewall**: Expose internal services without VPN
4. **Development**: Test webhooks/integrations with local API
5. **Multi-Region**: Access region-specific APIs through central gateway
