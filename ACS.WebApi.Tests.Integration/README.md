# ACS WebAPI Integration Tests

This project contains comprehensive integration tests for the ACS (Access Control System) WebAPI that test the full request-response cycle including database interactions.

## Overview

The integration tests verify that the API endpoints work correctly with real database connections and proper data persistence. They test the complete business workflows and ensure all components work seamlessly together.

## Test Structure

### Infrastructure (Infrastructure/)

- **TestWebApplicationFactory.cs**: Custom WebApplicationFactory for integration tests with in-memory database and mock services
- **TestServices.cs**: Mock implementations of gRPC services that return predictable test data
- **IntegrationTestBase.cs**: Base class providing common setup, authentication, and utility methods
- **TestDataBuilder.cs**: Builder pattern classes for creating test objects with fluent interface

### Controller Tests (Controllers/)

Individual controller integration tests that verify API endpoints:

- **UsersControllerIntegrationTests.cs**: Complete CRUD operations for users, group assignments, role assignments
- **GroupsControllerIntegrationTests.cs**: Group management, hierarchy operations, business rules validation
- **PermissionsControllerIntegrationTests.cs**: Permission evaluation, granting, denying, complex access control scenarios

### Scenario Tests (Scenarios/)

End-to-end workflow tests that span multiple controllers:

- **UserManagementWorkflowTests.cs**: Complete user lifecycle, group hierarchy, permission inheritance, bulk operations
- **ComplianceReportingWorkflowTests.cs**: Audit trails, compliance validation, data privacy, access reviews

## Features Tested

### API Endpoint Testing
- All HTTP methods (GET, POST, PUT, DELETE) for each controller
- Correct HTTP status codes for success and error scenarios
- Request/response serialization and content types
- Response headers and API contracts

### Database Integration
- Data persistence verification
- Entity relationships and foreign key constraints
- Query operations and data retrieval
- Transaction handling and rollback scenarios

### Authentication & Authorization
- Protected endpoints require authentication
- Role-based access control (RBAC)
- Unauthorized access returns proper HTTP codes
- Different user roles and permission levels

### Business Logic Integration
- Complex workflows spanning multiple entities
- Business rules enforcement at API level
- Validation rules and error responses
- Audit logging and compliance features

### Error Handling
- Invalid input validation
- Database constraint violations
- Concurrency and conflict scenarios
- Graceful error responses

## Test Configuration

### Database
- Uses Entity Framework Core with in-memory database
- Fresh database created for each test run
- Seeded with consistent test data
- Proper isolation between tests

### Authentication
- JWT token-based authentication for testing
- Configurable user roles and permissions
- Test helper methods for setting up auth contexts

### Mock Services
- Custom gRPC service implementations for testing
- Predictable responses for consistent test results
- Error simulation for negative testing scenarios

## Running the Tests

### Prerequisites
- .NET 9.0 SDK
- All project dependencies installed via NuGet

### Command Line
```bash
# Run all integration tests
dotnet test ACS.WebApi.Tests.Integration

# Run with detailed output
dotnet test ACS.WebApi.Tests.Integration --verbosity normal

# Run specific test class
dotnet test ACS.WebApi.Tests.Integration --filter "FullyQualifiedName~UsersControllerIntegrationTests"

# Run tests with code coverage
dotnet test ACS.WebApi.Tests.Integration --collect:"XPlat Code Coverage"
```

### Visual Studio
- Open Test Explorer
- Build solution to discover tests
- Run individual tests or test classes
- View detailed test results and coverage

## Test Data

### Default Test Data
The tests use consistent seed data including:
- 2 Users (John Doe, Jane Smith)
- 2 Groups (Administrators, Users) with hierarchy
- 2 Roles (Admin, User)
- Basic VerbTypes (GET, POST, PUT, DELETE)
- SchemeTypes for permissions

### Test Builders
Use TestDataBuilder for creating test objects:
```csharp
var user = TestDataBuilder.User()
    .WithName("Test User")
    .WithEmail("test@example.com")
    .Build();

var request = TestDataBuilder.CreateUserRequest()
    .WithName("New User")
    .WithGroupId(1)
    .Build();
```

## Best Practices Demonstrated

### Test Isolation
- Each test is independent and can run in any order
- Proper setup/teardown for database state
- No shared state between tests

### Realistic Scenarios
- Real-world usage patterns
- Edge cases and boundary conditions
- Realistic data volumes and relationships

### Security Testing
- Authentication and authorization verification
- Input validation and sanitization
- Error handling without information disclosure

### Performance Considerations
- Response time verification
- Database query efficiency
- Proper resource cleanup

## Contributing

When adding new integration tests:

1. **Follow the existing patterns**: Use IntegrationTestBase and TestDataBuilder
2. **Test both positive and negative scenarios**: Success cases and error conditions
3. **Verify complete workflows**: Test end-to-end business processes
4. **Include proper assertions**: Use FluentAssertions for readable tests
5. **Document complex scenarios**: Add comments explaining business logic
6. **Maintain test isolation**: Each test should be independent
7. **Clean up resources**: Proper disposal and cleanup in test teardown

## Dependencies

- **MSTest**: Testing framework
- **FluentAssertions**: Expressive assertion library
- **Microsoft.AspNetCore.Mvc.Testing**: ASP.NET Core test host
- **Microsoft.EntityFrameworkCore.InMemory**: In-memory database for testing
- **System.IdentityModel.Tokens.Jwt**: JWT token handling for auth tests

## Troubleshooting

### Common Issues

1. **Test failures due to database state**: Ensure proper test isolation and cleanup
2. **Authentication issues**: Verify JWT token configuration and test setup
3. **Service dependency errors**: Check mock service implementations
4. **Timing issues**: Use appropriate delays for async operations

### Debug Tips

1. **Enable detailed logging**: Set logging level to Information for EF Core
2. **Examine test output**: Check console output for detailed error messages
3. **Verify test data**: Ensure seed data matches test expectations
4. **Check authentication**: Verify JWT tokens are properly configured

## Future Enhancements

Potential areas for test expansion:

1. **Performance testing**: Load testing and stress testing scenarios
2. **Concurrency testing**: Multi-user concurrent access scenarios
3. **Integration with external services**: Testing with real external dependencies
4. **Chaos engineering**: Failure injection and resilience testing
5. **Security testing**: Penetration testing and vulnerability scanning