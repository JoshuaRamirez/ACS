# User Manual

This guide walks new users through the basic steps required to run the service and interact with its API.

## Getting Started
Build the solution using the .NET SDK 8 or later. Start `ACS.WebApi` and issue requests to the available endpoints.

## Features
The API exposes controllers for users, groups, roles, and permissions with the following endpoints:
- `GET /api/users` - list all users and return a `UsersResponse`
- `GET /api/users/{id}` - fetch a specific user and return a `UserResponse`
- `POST /api/users` - create a new user in memory with a `CreateUserRequest` and return a `UserResponse`
- `GET /api/groups` - list all groups and return a `GroupsResponse`
- `GET /api/groups/{id}` - fetch a specific group and return a `GroupResponse`
- `POST /api/groups` - create a new group in memory with a `CreateGroupRequest` and return a `GroupResponse`
- `GET /api/roles` - list all roles and return a `RolesResponse`
- `GET /api/roles/{id}` - fetch a specific role and return a `RoleResponse`
- `POST /api/roles` - create a new role in memory with a `CreateRoleRequest` and return a `RoleResponse`
- `GET /api/permissions` - list all permissions and return a `PermissionsResponse`
- `GET /api/permissions/{id}` - fetch a specific permission and return a `PermissionResponse`
- `POST /api/permissions` - create a new permission in memory with a `CreatePermissionRequest` and return a `PermissionResponse`

These endpoints translate between domain models and WebResource DTOs. Additional functionality can be included by extending the service layer and API endpoints.

## FAQs
**Q:** How do I set up the database?
**A:** Build the database project to generate a SQL deployment script and run it against your SQL Server instance.
