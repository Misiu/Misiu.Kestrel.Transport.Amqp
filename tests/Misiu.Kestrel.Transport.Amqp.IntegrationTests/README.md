# Integration Tests

This project contains comprehensive integration tests for the Misiu.Kestrel.Transport.Amqp library.

## Overview

The tests verify the complete end-to-end functionality of the AMQP transport library by:
- Spinning up a RabbitMQ container using Testcontainers
- Creating a Gateway server
- Creating client applications (both Transport and BackgroundService approaches)
- Testing the complete request/response cycle

## Test Coverage

### Transport Approach Tests (`TransportApproachTests.cs`)

Tests the Kestrel Transport approach where AMQP is used directly as a Kestrel transport:

- **Connection Tests**: Verifies RabbitMQ, server, and client can all connect
- **Reconnection Tests**: Tests automatic reconnection after RabbitMQ restarts
- **Immediate Response**: Tests fast requests that complete within the timeout
- **Delayed Response (202)**: Tests slow requests that return 202 Accepted with async result retrieval
- **404 Handling**: Tests non-existent endpoints return proper 404 responses
- **500 Error Handling**: Tests exception handling and 500 error responses
- **Header Verification**: Tests that request/response headers are properly passed through
- **POST Requests**: Tests POST requests with request bodies
- **Concurrent Requests**: Tests multiple simultaneous requests

### BackgroundService Approach Tests (`BackgroundServiceApproachTests.cs`)

Tests the BackgroundService approach where requests are forwarded to a local HTTP API:

- All the same test scenarios as Transport approach
- Additional test to distinguish between client errors and server errors

## Infrastructure

### RabbitMqFixture

Manages the lifecycle of the RabbitMQ container:
- Starts a RabbitMQ container before tests
- Provides connection details to tests
- Supports restarting RabbitMQ for reconnection tests
- Cleans up after tests complete

### TestServerFactory

Creates test server instances:
- `CreateGatewayServer()` - Creates a Gateway server for testing
- `CreateTransportClient()` - Creates a client using the Transport approach
- `CreateLocalApi()` - Creates a local HTTP API for BackgroundService tests
- `CreateBackgroundServiceClient()` - Creates a BackgroundService client
- `SetupTestEndpoints()` - Common test endpoints for all approaches

## Requirements

- Docker must be installed and running (for Testcontainers)
- .NET 9.0 SDK

## Running the Tests

### Run all tests
```bash
dotnet test
```

### Run Transport approach tests only
```bash
dotnet test --filter "FullyQualifiedName~TransportApproachTests"
```

### Run BackgroundService approach tests only
```bash
dotnet test --filter "FullyQualifiedName~BackgroundServiceApproachTests"
```

### Run a specific test
```bash
dotnet test --filter "FullyQualifiedName~Test_Request_Response_Immediate"
```

## Test Execution Time

These are integration tests that:
- Start Docker containers (RabbitMQ)
- Wait for services to be ready
- Test real network communication
- Some tests intentionally wait for timeouts (e.g., the 202 Accepted tests wait 5+ seconds)

Expected execution time: 30-60 seconds for the full suite.

## Important Notes

1. **Docker is Required**: Tests will fail if Docker is not available
2. **Port Allocation**: Tests use dynamic port allocation (127.0.0.1:0) to avoid conflicts
3. **Sequential Execution**: Tests in each class run sequentially due to the shared RabbitMQ fixture
4. **JSON Serialization**: The library uses camelCase JSON serialization for AMQP message interchange
5. **Test Isolation**: Each test class has its own isolated RabbitMQ container

## Troubleshooting

### Tests timeout
- Check that Docker is running: `docker ps`
- Ensure you have enough system resources
- Check firewall settings aren't blocking Docker networking

### RabbitMQ container fails to start
- Pull the image manually: `docker pull rabbitmq:3.13-management-alpine`
- Check Docker logs for errors

### Port conflicts
- The tests use dynamic port allocation, so this should be rare
- Check if something is interfering with Docker's port mapping

## Future Improvements

- Add performance benchmarks
- Add stress tests with high message volume
- Add tests for message persistence
- Add tests for various network failure scenarios
- Add tests for security configurations
