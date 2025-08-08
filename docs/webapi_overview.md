# Web API Overview

This section describes the HTTP interface provided by the `ACS.WebApi` project.

## Endpoints
The API layer now provides resource based controllers.

- `GET /api/users` returns a `UsersResponse` containing the collection of users.
- `GET /api/users/{id}` returns a `UserResponse` for the specified user.
- `POST /api/users` accepts a `CreateUserRequest` and returns a `UserResponse`.

These actions map between domain entities and WebResource DTOs via dedicated mapping helpers.

Additional controllers for roles and groups will follow the same pattern.

## Authentication
Authentication is not enabled in the sample project but can be added using standard ASP.NET Core mechanisms such as JWT bearer tokens.

## Error Handling
Requests that fail validation or encounter server errors return appropriate HTTP status codes along with problem details where possible.
