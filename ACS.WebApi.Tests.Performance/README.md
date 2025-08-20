# ACS WebAPI Performance Tests

This project contains comprehensive performance tests for the ACS (Access Control System) WebAPI to measure system performance under various load conditions and identify bottlenecks.

## Test Categories

### 1. Load Tests (`LoadTests/ApiLoadTests.cs`)
Tests system performance under normal expected load:
- **Get Users Load Test**: Measures performance of user retrieval with pagination
- **Create Users Load Test**: Tests user creation performance under steady load
- **Mixed API Operations**: Simulates realistic load with 70% read/30% write operations
- **Search Operations**: Tests search functionality across users, groups, and roles
- **Pagination Operations**: Validates pagination performance with various page sizes
- **Authentication Operations**: Measures login/authentication performance

### 2. Stress Tests (`StressTests/ApiStressTests.cs`)
Identifies system breaking points and behavior under extreme conditions:
- **Gradual Load Increase**: Progressively increases load to find breaking point
- **High Concurrency**: Tests system stability under maximum concurrent users
- **Database Intensive Operations**: Stresses database with complex queries and bulk operations
- **Memory Intensive Operations**: Tests memory usage with large payloads and result sets
- **Burst Traffic**: Simulates sudden traffic spikes and recovery
- **Long Running Connections**: Tests connection pool handling over time
- **Error Recovery**: Validates system resilience with mixed valid/invalid requests

### 3. Benchmark Tests (`BenchmarkTests/ServiceBenchmarks.cs`)
Micro-benchmarks for service layer operations using BenchmarkDotNet:
- **Individual Service Operations**: Measures performance of core CRUD operations
- **Database Query Optimization**: Tests EF Core query performance
- **Permission Evaluation**: Benchmarks authorization logic
- **Bulk Operations**: Compares performance of single vs batch operations
- **Concurrent Operations**: Tests thread safety and concurrent access patterns

### 4. End-to-End Tests (`EndToEndTests/CriticalFlowTests.cs`)
Performance tests for complete user workflows:
- **User Registration and Permission Flow**: Complete new user onboarding process
- **Admin Workflow**: Complex administrative operations with multiple API calls
- **Permission Evaluation Flow**: User permission checking across multiple resources
- **Search and Filter Flow**: Complex search and filtering operations
- **Concurrent User Operations**: Tests data consistency under concurrent access

## Test Infrastructure

### PerformanceTestWebApplicationFactory
Custom `WebApplicationFactory` optimized for performance testing:
- Configurable in-memory or SQL Server database
- Optimized logging configuration (minimal logging for performance)
- Automatic test data seeding (1000 users, 100 groups, 50 roles)
- Performance-optimized service configuration

### PerformanceTestBase
Base class providing common performance test utilities:
- JWT token generation and management
- HTTP request builders with authentication
- Performance result analysis and reporting
- Configurable test duration and load levels

## Performance Configuration

### Load Test Configuration (`load-test-config.json`)
Centralized configuration for different environments:
- **Development**: Short tests with low load for rapid feedback
- **Staging**: Medium load tests for validation
- **Production**: Full-scale performance testing

### Test Scenarios
Weighted scenarios based on real-world usage patterns:
- **User Operations (40%)**: CRUD operations on user entities
- **Group Operations (25%)**: Group management and hierarchy operations
- **Role Operations (20%)**: Role and permission management
- **Search Operations (10%)**: Search and filtering across entities
- **Admin Operations (5%)**: Administrative and reporting functions

## Running Performance Tests

### Prerequisites
- .NET 8.0 SDK
- ACS WebAPI project
- SQL Server (for realistic database performance testing)
- Minimum 8GB RAM for stress testing

### Execution Commands

```bash
# Run all performance tests
dotnet test ACS.WebApi.Tests.Performance

# Run specific test categories
dotnet test ACS.WebApi.Tests.Performance --filter "Category=LoadTest"
dotnet test ACS.WebApi.Tests.Performance --filter "Category=StressTest"

# Run benchmarks (requires Release configuration)
dotnet test ACS.WebApi.Tests.Performance --configuration Release
# OR
set RUN_BENCHMARKS=true && dotnet test ACS.WebApi.Tests.Performance

# Run with detailed output
dotnet test ACS.WebApi.Tests.Performance --verbosity normal --logger "console;verbosity=detailed"
```

### Environment Variables
- `CI=true`: Enables longer test duration and higher load for CI/CD environments
- `RUN_BENCHMARKS=true`: Forces benchmark execution
- `Configuration=Release`: Optimized builds for accurate performance measurement

## Performance Metrics and Thresholds

### Key Performance Indicators (KPIs)
- **Mean Latency**: < 1000ms for standard operations
- **P95 Latency**: < 2000ms (95% of requests faster than 2s)
- **P99 Latency**: < 5000ms (99% of requests faster than 5s)
- **Throughput**: > 10 RPS minimum sustained throughput
- **Success Rate**: > 95% request success rate
- **Error Rate**: < 5% error rate under normal load

### Performance Benchmarks by Operation Type
- **Authentication**: < 1500ms mean latency
- **User CRUD**: < 1000ms mean latency
- **Search Operations**: < 800ms mean latency
- **Pagination**: < 600ms mean latency
- **Complex Admin Operations**: < 5000ms mean latency
- **Permission Evaluation**: < 2000ms for complete flow

## Test Data and Scenarios

### Seed Data
- **1000 Users**: Realistic user base for testing pagination and search
- **100 Groups**: Hierarchical group structure for testing relationships
- **50 Roles**: Various role types with different permission sets
- **Random Relationships**: Users assigned to groups and roles randomly

### Realistic Load Patterns
- **Read-Heavy Workload**: 70% read operations, 30% write operations
- **Search Patterns**: Common search terms and filters
- **User Behavior**: Mixed concurrent operations simulating real usage
- **Admin Activities**: Periodic bulk operations and reporting

## Continuous Performance Testing

### CI/CD Integration
```yaml
# Example GitHub Actions workflow step
- name: Run Performance Tests
  run: |
    dotnet test ACS.WebApi.Tests.Performance \
      --configuration Release \
      --logger trx \
      --results-directory TestResults \
      --verbosity normal
  env:
    CI: true
    RUN_BENCHMARKS: true

- name: Performance Test Results
  uses: dorny/test-reporter@v1
  if: success() || failure()
  with:
    name: Performance Test Results
    path: TestResults/*.trx
    reporter: dotnet-trx
```

### Performance Regression Detection
- Baseline performance metrics stored in CI/CD pipeline
- Automated alerts for performance degradation > 20%
- Trend analysis for long-term performance monitoring
- Integration with monitoring tools (Application Insights, Prometheus)

## Troubleshooting Performance Issues

### Common Performance Bottlenecks
1. **Database N+1 Queries**: Use Include() statements or projection
2. **Inefficient Pagination**: Implement cursor-based pagination for large datasets
3. **Memory Leaks**: Monitor disposal of DbContext and HttpClient instances
4. **Connection Pool Exhaustion**: Optimize connection string settings
5. **CPU-Intensive Operations**: Move to background services or optimize algorithms

### Performance Debugging
1. **Enable Detailed Logging**: Set `enableDetailedLogging: true` in test factory
2. **Database Profiling**: Use SQL Server Profiler or EF Core logging
3. **Memory Analysis**: Enable memory diagnoser in benchmarks
4. **Thread Contention**: Monitor lock contention in concurrent scenarios

### Load Test Troubleshooting
- **High Error Rates**: Check connection limits, timeout settings
- **Memory Issues**: Reduce concurrent users or optimize test data size
- **Database Locks**: Implement proper isolation levels and timeout handling
- **Network Issues**: Verify test environment network capacity

## Best Practices

### Test Development
1. **Realistic Test Data**: Use production-like data volumes and distributions
2. **Environment Isolation**: Use dedicated test environments for accurate results
3. **Baseline Establishment**: Record baseline metrics before code changes
4. **Gradual Load Increase**: Start with low load and gradually increase

### Performance Optimization
1. **Database Optimization**: Proper indexing, query optimization, connection pooling
2. **Caching Strategy**: Implement multi-level caching with appropriate TTLs
3. **Async/Await**: Use async patterns throughout the application stack
4. **Resource Management**: Proper disposal of resources and connection management

### Monitoring and Alerting
1. **Real-time Monitoring**: Application performance monitoring (APM) tools
2. **Performance Budgets**: Set and enforce performance thresholds
3. **Alerting**: Automated alerts for performance degradation
4. **Regular Testing**: Schedule regular performance test runs

## Results Analysis

### NBomber Reports
NBomber generates detailed HTML reports including:
- Request/response statistics
- Latency percentiles (P50, P95, P99)
- Throughput and error rates
- Timeline charts and distribution graphs

### BenchmarkDotNet Reports
BenchmarkDotNet provides micro-benchmark results:
- Execution time statistics
- Memory allocation analysis
- Garbage collection impact
- Statistical significance testing

### Performance Trends
- Track performance metrics over time
- Identify performance regressions early
- Correlate performance with code changes
- Plan capacity based on growth trends