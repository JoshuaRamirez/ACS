# Web API Overview

This section describes the HTTP interface provided by the `ACS.WebApi` project.

## Endpoints
The API layer now provides resource based controllers for users, groups, roles, and permissions.

- `GET /api/users` returns a `UsersResponse` containing the collection of users.
- `GET /api/users/{id}` returns a `UserResponse` for the specified user.
- `POST /api/users` accepts a `CreateUserRequest` and returns a `UserResponse`.
- `GET /api/groups` returns a `GroupsResponse` containing the collection of groups.
- `GET /api/groups/{id}` returns a `GroupResponse` for the specified group.
- `POST /api/groups` accepts a `CreateGroupRequest` and returns a `GroupResponse`.
- `GET /api/roles` returns a `RolesResponse` containing the collection of roles.
- `GET /api/roles/{id}` returns a `RoleResponse` for the specified role.
- `POST /api/roles` accepts a `CreateRoleRequest` and returns a `RoleResponse`.
- `GET /api/permissions` returns a `PermissionsResponse` containing the collection of permissions.
- `GET /api/permissions/{id}` returns a `PermissionResponse` for the specified permission.
- `POST /api/permissions` accepts a `CreatePermissionRequest` and returns a `PermissionResponse`.

These actions map between domain entities and WebResource DTOs via dedicated mapping helpers.

## Authentication
Authentication is not enabled in the sample project but can be added using standard ASP.NET Core mechanisms such as JWT bearer tokens.

## Error Handling
Requests that fail validation or encounter server errors return appropriate HTTP status codes along with problem details where possible.
