# API Layer Design

This document describes the new API layer that replaces the sample `WeatherForecast` endpoint.

The Web API now references the `ACS.Service` project and exposes resource-based controllers.
Initial implementation focuses on users but the same pattern will be applied to roles and groups.

## User Endpoints
- `GET /api/users` - list all users
- `GET /api/users/{id}` - retrieve a single user
- `POST /api/users` - create a new user in memory

The `UserService` class currently stores data in memory. It will later integrate with the database context.
