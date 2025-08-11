# Service Overview

The service exposes a simple REST API that can be expanded with additional endpoints. Its default controller demonstrates how to return data from the service layer.

## Core services
The primary service project is **ACS.Service**, which provides command and query handlers. These handlers can be used by the Web API or other clients.

## Interfaces
The Web API publishes controllers in the `ACS.WebApi` project. Requests are routed to the appropriate handler within the service layer.

## Workflows
A typical request passes through the API, invokes a handler, performs database operations, and returns a response in JSON format.

## Domain layer
The service project defines domain entities such as `User`, `Role`, and `Group`. These types inherit from an abstract `Entity` base class that tracks parent and child relationships and a set of `Permission` objects. The domain methods now invoke *normalizer* helpers under `ACS.Service/Delegates/Normalizers` whenever permissions or memberships are modified. These normalizers update in-memory data model collections while keeping references consistent. They are marked internal for future database integration. Implementation is ongoing, with recent additions covering permission removal, bidirectional membership updates for groups and roles, helpers for maintaining group hierarchies, and unit tests validating both the helpers and their domain invocations. Groups can be added from either the parent or child perspective, and cycles are prevented via `InvalidOperationException`. Normalizers also map HTTP verb enums to `VerbType` records case-insensitively.

The API layer now includes controllers for users, groups, roles, and permissions backed by in-memory services. These endpoints demonstrate how future features can interact with the domain layer through service helpers and normalizers to persist entities and check permissions.

The Web API is configured with an `ApplicationDbContext` using a SQL Server `DefaultConnection` string, preparing the application for a future transition from in-memory storage to a persistent database.

## Normalizer design
The normalizer classes act as delegates that mirror domain operations onto in-memory data model collections while maintaining bidirectional references. This centralizes update logic so future persistence layers can reuse the same algorithms when entities are saved to the database.
