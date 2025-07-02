# Service Overview

The service exposes a simple REST API that can be expanded with additional endpoints. Its default controller demonstrates how to return data from the service layer.

## Core services
The primary service project is **ACS.Service**, which provides command and query handlers. These handlers can be used by the Web API or other clients.

## Interfaces
The Web API publishes controllers in the `ACS.WebApi` project. Requests are routed to the appropriate handler within the service layer.

## Workflows
A typical request passes through the API, invokes a handler, performs database operations, and returns a response in JSON format.

## Domain layer
The service project defines domain entities such as `User`, `Role`, and `Group`. These types inherit from an abstract `Entity` base class that tracks parent and child relationships and a set of `Permission` objects. The entities expose methods to add or remove children and manage permissions. Many of these methods delegate to *normalizer* helpers under `ACS.Service/Delegates/Normalizers`. The normalizers currently work with in-memory lists and are marked internal for future database integration. Implementation is ongoing, with recent additions covering permission removal, bidirectional membership updates, and unit tests validating these helpers.

The API layer now begins with a `UsersController` that exposes basic CRUD operations backed by an in-memory `UserService`. Controllers for roles and groups will be added next. These endpoints demonstrate how future features can interact with the domain layer through service helpers and normalizers to persist entities and check permissions.

## Normalizer design
The normalizer classes act as delegates that mirror domain operations onto in-memory data model collections while maintaining bidirectional references. This centralizes update logic so future persistence layers can reuse the same algorithms when entities are saved to the database.
