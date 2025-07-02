# User Manual

This guide walks new users through the basic steps required to run the service and interact with its API.

## Getting Started
Build the solution using the .NET SDK 8 or later. Start `ACS.WebApi` and issue requests to the available endpoints.

## Features
The API exposes a `UsersController` with the following endpoints:
- `GET /api/users` - list all users
- `GET /api/users/{id}` - fetch a specific user
- `POST /api/users` - create a new user in memory

Controllers for roles and groups will be added next. Additional functionality can be included by extending the service layer and API endpoints.

## FAQs
**Q:** How do I set up the database?
**A:** Build the database project to generate a SQL deployment script and run it against your SQL Server instance.
