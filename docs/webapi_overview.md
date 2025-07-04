# Web API Overview

This section describes the HTTP interface provided by the `ACS.WebApi` project.

## Endpoints
The API layer now provides resource based controllers.

- `GET /api/users` returns the collection of users.
- `GET /api/users/{id}` returns a single user.
- `POST /api/users` creates a new user.

Additional controllers for roles and groups will follow the same pattern.

## Authentication
Authentication is not enabled in the sample project but can be added using standard ASP.NET Core mechanisms such as JWT bearer tokens.

## Error Handling
Requests that fail validation or encounter server errors return appropriate HTTP status codes along with problem details where possible.
