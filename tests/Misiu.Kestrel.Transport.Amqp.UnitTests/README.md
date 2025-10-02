# Misiu.Kestrel.Transport.Amqp Unit Tests

This project contains comprehensive unit tests for the Misiu.Kestrel.Transport.Amqp library.

## Test Coverage

### Total Tests: 245

The test suite covers all major components of the library with extensive edge case testing:

### Test Classes

#### 1. **AmqpTransportOptionsTests** (23 tests)
Tests for the main configuration options class covering:
- Default values verification
- All property setters with various valid inputs
- Edge cases (null, empty, boundary values)
- Multiple property combinations

#### 2. **AmqpEndPointTests** (12 tests)
Tests for the AMQP endpoint class:
- Constructor variations (with/without options name)
- ToString formatting
- Property immutability
- Various endpoint naming conventions

#### 3. **HttpRequestEnvelopeTests** (26 tests)
Comprehensive tests for HTTP request serialization:
- Property getters and setters
- All HTTP methods (GET, POST, PUT, DELETE, etc.)
- Path and query string handling
- Header collections
- Body serialization/deserialization
- Round-trip JSON serialization
- Null and empty value handling

#### 4. **HttpResponseEnvelopeTests** (33 tests)
Extensive tests for HTTP response handling:
- All HTTP status codes (2xx, 3xx, 4xx, 5xx)
- Response headers and body
- Timing properties (ServerStartedAtUtc, ServerCompletedAtUtc, ProcessingMilliseconds)
- JSON serialization round-trips
- Edge cases with null/empty values

#### 5. **KestrelAmqpExtensionsTests** (35 tests)
Tests for Kestrel integration extension methods:
- Service registration (AddAmqpTransport)
- Configuration binding (programmatic and appsettings.json)
- Custom options names
- Multiple registration attempts
- Endpoint creation (ListenAmqp)
- Various configuration scenarios:
  - Default values
  - Custom sections
  - Empty configuration
  - All options combined
  - Method chaining

#### 6. **AmqpClientExtensionsTests** (26 tests)
Tests for AMQP client configuration:
- Service registration (AddAmqpClient)
- HttpClient factory registration
- Hosted service registration
- Configuration binding from appsettings
- Custom section names
- Various client configurations:
  - Different hostnames
  - Different ports
  - Different prefetch counts
- Method chaining

#### 7. **AmqpGatewayExtensionsTests** (24 tests)
Tests for AMQP gateway configuration:
- Service registration (AddAmqpGateway)
- Memory cache registration
- Configuration binding
- Gateway-specific options:
  - ImmediateTimeoutSeconds
  - ResultTtlMinutes
  - PathPrefixToRemove
  - PathPrefixToAdd
- Combined path transformations

#### 8. **PathTransformationTests** (47 tests)
Comprehensive tests for URL path transformation logic:
- **PathPrefixToRemove** scenarios:
  - Basic prefix removal
  - Case-insensitive matching
  - Non-matching paths (no modification)
  - Prefix without leading slash
  - Empty path handling
  - Query string preservation
- **PathPrefixToAdd** scenarios:
  - Basic prefix addition
  - Prefix without leading slash
  - Trailing slash handling
  - Query string preservation
- **Combined transformations** (remove + add)
- **Edge cases**:
  - Null/empty prefixes
  - Root path handling
  - Multiple slashes
  - Complex query strings

#### 9. **AmqpConnectionListenerFactoryTests** (19 tests)
Tests for connection listener factory:
- Constructor initialization
- Endpoint type validation
- AmqpEndPoint acceptance
- Rejection of non-AMQP endpoints (IPEndPoint, DnsEndPoint, UnixDomainSocketEndPoint)
- Options name handling (default and custom)
- Cancellation token support
- Interface implementation verification

## Testing Frameworks

- **XUnit**: Test runner and assertion framework
- **FluentAssertions**: Fluent assertion library for readable test code
- **Moq**: Mocking framework for dependencies
- **Microsoft.AspNetCore.TestHost**: ASP.NET Core testing utilities

## Running Tests

```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity detailed

# Run tests in release mode
dotnet test --configuration Release

# Run tests with code coverage
dotnet test /p:CollectCoverage=true
```

## Test Patterns

### Configuration Testing Pattern
Tests verify that configuration can be provided through:
1. Programmatic configuration (Action delegates)
2. IConfiguration binding (appsettings.json)
3. Custom section names
4. Default values when not configured

### Edge Case Testing Pattern
Each test class includes extensive edge case coverage:
- Null values
- Empty strings
- Boundary values (min/max ports, timeout values)
- Special characters
- Case sensitivity/insensitivity
- Query string preservation
- Path normalization

### Service Registration Testing Pattern
Extension method tests verify:
- Services are registered correctly
- Dependencies are resolved
- Method chaining works
- Multiple registrations don't duplicate services

## Future Enhancements

Potential areas for additional test coverage:
- Integration tests with actual RabbitMQ instance
- Performance tests for path transformation
- Concurrency tests for middleware
- End-to-end tests with complete request/response cycles
- Memory leak detection tests
- Load testing scenarios

## Test Maintenance

When adding new features:
1. Add corresponding test class if new component
2. Follow existing test patterns (Arrange-Act-Assert)
3. Use descriptive test names: `Method_Scenario_ExpectedResult`
4. Add Theory tests for multiple similar scenarios
5. Include both positive and negative test cases
6. Test edge cases and boundary conditions
7. Ensure all public APIs have test coverage
