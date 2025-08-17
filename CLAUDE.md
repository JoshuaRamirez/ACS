# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ACS (Access Control System) is a .NET 8 solution implementing a REST API with a layered architecture for access control and authorization management. The solution uses Entity Framework Core for data access and SQL Server for persistence.

## Solution Structure

The solution follows a multi-project architecture with clear separation of concerns:

- **ACS.WebApi**: ASP.NET Core Web API layer hosting REST endpoints
- **ACS.Service**: Business logic layer containing domain models, data access (EF Core), and service implementations
- **ACS.Database**: SQL Server database project with table definitions and seed data
- **ACS.WebResources**: Web resources library
- **Test Projects**: Unit and integration tests for each layer using MSTest framework

## Key Commands

### Build Commands
```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build ACS.WebApi/ACS.WebApi.csproj

# Clean build
dotnet clean && dotnet build
```

### Run Commands
```bash
# Run the Web API
dotnet run --project ACS.WebApi

# Run with specific environment
dotnet run --project ACS.WebApi --environment Development
```

### Test Commands
```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity normal

# Run specific test project
dotnet test ACS.Service.Tests.Unit/ACS.Service.Tests.Unit.csproj

# Run tests matching a filter
dotnet test --filter "FullyQualifiedName~GroupDomain"

# Run tests with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Database Commands
```bash
# Deploy database project (requires SQL Server)
msbuild ACS.Database/ACS.Database.sqlproj /p:Configuration=Release /p:Platform="Any CPU"
```

## Architecture Patterns

### Domain-Driven Design
The solution implements domain models in `ACS.Service/Domain/` with rich business logic:
- **Entity**: Base class for all domain entities with parent-child relationships and permissions
- **User**: Represents system users with role assignments
- **Group**: Hierarchical container for users, roles, and other groups with cycle prevention
- **Role**: Defines permission sets that can be assigned to users
- **Permission**: Represents access rights to resources

### Data Access Pattern
- Uses Entity Framework Core 8 with `ApplicationDbContext` in `ACS.Service/Data/`
- Database models are in `ACS.Service/Data/Models/`
- Follows code-first approach with migrations

### Normalizer Pattern
The solution uses normalizers in `ACS.Service/Delegates/Normalizers/` to handle domain operations that require database synchronization:
- Each normalizer handles a specific domain operation (e.g., `AddUserToGroupNormalizer`)
- Normalizers execute after domain model changes to update the database
- This pattern ensures consistency between in-memory domain models and persistent storage

### Service Layer
- Services are registered in `Program.cs` using dependency injection
- `IUserService` and `UserService` demonstrate the service interface pattern
- Services coordinate between controllers and domain/data layers

### Request Flow
1. HTTP request → WebApi Controller
2. Controller → Service Layer (via DI)
3. Service → Domain Logic & Normalizers
4. Normalizers → Entity Framework → Database
5. Response flows back through the layers

## Testing Approach
- **Unit Tests**: Test individual components in isolation (MSTest framework)
- **Integration Tests**: Test API endpoints and database interactions
- Test projects follow naming convention: `{ProjectName}.Tests.Unit` and `{ProjectName}.Tests.Integration`

## Database Design
The database schema includes:
- **Entity**: Base table for all entities with hierarchical relationships
- **User, Group, Role**: Specific entity types
- **PermissionScheme**: Links entities to permissions
- **Resource & UriAccess**: Define protected resources and access patterns
- **AuditLog**: Tracks system changes
- **VerbType**: HTTP verbs for URI-based access control

## Development Setup
For local development, run `scripts/setup_dotnet.sh` to install .NET SDK 8.0 if not already available.