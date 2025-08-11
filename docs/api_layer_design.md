# API Layer Design

This document describes the new API layer that replaces the sample `WeatherForecast` endpoint.

The Web API now references the `ACS.Service` project and exposes resource-based controllers.
Initial implementation covered users and now also includes roles, groups, and permissions following the same pattern.

The WebResources project now includes resource representations for users, roles, groups, and permissions.

## User Endpoints
- `GET /api/users` - list all users
- `GET /api/users/{id}` - retrieve a single user
- `POST /api/users` - create a new user in memory

## Group Endpoints
- `GET /api/groups` - list all groups
- `GET /api/groups/{id}` - retrieve a single group
- `POST /api/groups` - create a new group in memory

## Role Endpoints
- `GET /api/roles` - list all roles
- `GET /api/roles/{id}` - retrieve a single role
- `POST /api/roles` - create a new role in memory

## Permission Endpoints
- `GET /api/permissions` - list all permissions
- `GET /api/permissions/{id}` - retrieve a single permission
- `POST /api/permissions` - create a new permission in memory

The Web API now configures `ApplicationDbContext` with a SQL Server `DefaultConnection` string, though the `UserService` still stores data in memory pending full database integration.
