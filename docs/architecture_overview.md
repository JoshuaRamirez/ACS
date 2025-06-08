# Architecture Overview

The ACS solution is organized into separate projects to keep concerns isolated. The Web API layer communicates with a service library which in turn interacts with a SQL database project.

## System components
- **ACS.WebApi** &ndash; hosts REST endpoints built with ASP.NET Core.
- **ACS.Service** &ndash; contains business logic and data access via Entity Framework Core.
- **ACS.Database** &ndash; SQL Server project that defines tables and seed data.

## Data flow
1. A client sends an HTTP request to the Web API.
2. Controllers forward calls to the service layer.
3. The service layer uses EF Core to query or update the database.
4. Results are returned to the caller through the API.

## Key technologies
- .NET 8
- ASP.NET Core Web API
- Entity Framework Core 8
- SQL Server
