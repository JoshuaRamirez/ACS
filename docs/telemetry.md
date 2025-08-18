# OpenTelemetry Distributed Tracing

The ACS system implements comprehensive distributed tracing using OpenTelemetry to provide observability across the multi-tenant vertical architecture.

## Overview

The tracing system captures detailed information about:
- HTTP requests to the WebApi
- gRPC communication between WebApi and tenant processes  
- Command processing within tenant processes
- Database operations via Entity Framework Core
- Circuit breaker operations and retry logic
- Error conditions and performance metrics

## Architecture

### WebApi Layer (ACS.WebApi)
- **Service**: `TelemetryService`
- **Activity Sources**: 
  - `ACS.WebApi` - Main WebApi operations
  - `ACS.TenantGrpcClient` - gRPC client calls
  - `ACS.CircuitBreaker` - Circuit breaker state changes

### VerticalHost Layer (ACS.VerticalHost)  
- **Service**: `VerticalHostTelemetryService`
- **Activity Sources**:
  - `ACS.VerticalHost` - Main tenant process operations
  - `ACS.CommandProcessor` - Command processing operations
  - `ACS.DomainNormalizer` - Domain normalizer operations

## Configuration

### appsettings.json
```json
{
  "OpenTelemetry": {
    "Exporter": "Console",
    "OtlpEndpoint": "http://localhost:4317"
  }
}
```

### Exporters
- **Console**: Development/debugging (default)
- **OTLP**: Production with Jaeger, Zipkin, or other OTLP-compatible systems

### Environment Variables
- `ASPNETCORE_ENVIRONMENT`: Development/Production
- `TENANT_ID`: For VerticalHost processes
- `OTEL_EXPORTER_OTLP_ENDPOINT`: Override OTLP endpoint

## Trace Structure

### WebApi Request Flow
```
HTTP Request → Controller → TenantGrpcClient → Circuit Breaker → gRPC Call
     ↓              ↓             ↓                ↓             ↓
   aspnet      custom        grpc.client      circuit      grpc.retry
```

### VerticalHost Command Flow  
```
gRPC Request → Command Processing → Domain Logic → Database Operations
     ↓               ↓                   ↓              ↓
   grpc         command.process      normalizer      ef.core
```

## Key Spans and Tags

### WebApi Spans
- `tenant.{operation}` - High-level tenant operations
- `grpc.client.{method}` - gRPC client method calls
- `grpc.retry.attempt_{n}` - Individual retry attempts
- `circuit_breaker.{state}` - Circuit breaker state transitions

### VerticalHost Spans
- `grpc.service.{method}` - gRPC service method handling
- `command.process.{type}` - Command processing
- `normalizer.{type}` - Domain normalizer execution
- `database.{operation}` - Database operations

### Common Tags
- `tenant.id` - Tenant identifier for multi-tenant correlation
- `command.type` - Type of command being processed
- `command.request_id` - Request correlation ID
- `error` - Boolean indicating error state
- `error.type` - Exception type name
- `error.message` - Error message
- `retry.attempt` - Current retry attempt number
- `circuit_breaker.state` - Current circuit breaker state

## Usage Examples

### Custom Spans in Controllers
```csharp
using var activity = TelemetryService.StartTenantActivity("custom_operation", tenantId);
activity?.SetTag("custom.parameter", value);
// ... operation logic
TelemetryService.RecordError(activity, exception); // if error occurs
```

### Command Processing Telemetry
```csharp
using var activity = VerticalHostTelemetryService.StartCommandProcessingActivity(
    commandType, requestId);
// ... command processing
VerticalHostTelemetryService.RecordCommandMetrics(activity, elapsed, successful);
```

### Database Operation Tracking
```csharp
using var activity = VerticalHostTelemetryService.StartDatabaseActivity("create", "User");
// ... database operation
VerticalHostTelemetryService.RecordDatabaseMetrics(activity, recordsAffected, queryTime);
```

## Monitoring Queries

### High-Level Metrics
- Request success/failure rates by tenant
- Command processing latency percentiles
- Circuit breaker state changes
- Error rates by component

### Performance Analysis
- Slow database queries (>500ms)
- Slow command processing (>1000ms)
- gRPC retry patterns
- Multi-tenant resource usage

### Error Investigation
- Exception correlation across services
- Failed command types and frequencies
- Circuit breaker open events
- Tenant-specific error patterns

## Integration with Monitoring Systems

### Jaeger
1. Set `"Exporter": "otlp"` in appsettings.json
2. Configure OTLP endpoint to Jaeger collector
3. Use Jaeger UI for trace visualization

### Zipkin
1. Set `"Exporter": "otlp"` in appsettings.json  
2. Configure OTLP endpoint to Zipkin collector
3. Use Zipkin UI for trace analysis

### Prometheus + Grafana
1. Use OTLP exporter with OTEL Collector
2. Configure collector to export metrics to Prometheus
3. Build Grafana dashboards for visualization

## Best Practices

### Span Naming
- Use hierarchical naming: `component.operation.detail`
- Include operation type: `grpc.client.GetUsers`
- Be consistent across services

### Tag Usage
- Always include `tenant.id` for multi-tenant correlation
- Add `error` and error details for failed operations
- Include performance indicators (timing, counts)
- Use structured data for complex values

### Sampling
- Use head-based sampling for high-volume endpoints
- Always sample error traces
- Adjust sampling rates based on environment

### Performance
- Use `using` statements for automatic span disposal
- Avoid excessive span creation in tight loops
- Cache activity sources for performance
- Use conditional tag setting for expensive operations

## Troubleshooting

### Common Issues
1. **Missing traces**: Check exporter configuration and network connectivity
2. **High overhead**: Reduce sampling rate or disable non-critical spans
3. **Incomplete traces**: Verify activity source registration and span propagation
4. **Missing tenant correlation**: Ensure `tenant.id` tags are set consistently

### Debug Configuration
```json
{
  "Logging": {
    "LogLevel": {
      "OpenTelemetry": "Debug"
    }
  }
}
```