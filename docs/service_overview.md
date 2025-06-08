# Service Overview

The service exposes a simple REST API that can be expanded with additional endpoints. Its default controller demonstrates how to return data from the service layer.

## Core services
The primary service project is **ACS.Service**, which provides command and query handlers. These handlers can be used by the Web API or other clients.

## Interfaces
The Web API publishes controllers in the `ACS.WebApi` project. Requests are routed to the appropriate handler within the service layer.

## Workflows
A typical request passes through the API, invokes a handler, performs database operations, and returns a response in JSON format.

## Domain layer
The service project defines domain entities such as `User`, `Role`, and `Group`. These types inherit from an abstract `Entity` base class that tracks parent and child relationships and a set of `Permission` objects. The entities expose methods to add or remove children and manage permissions. Many of these methods delegate to *normalizer* helpers under `ACS.Service/Delegates/Normalizers`. The normalizers currently work with in-memory lists and are marked internal for future database integration.

The only API controller (`WeatherForecastController`) does not yet interact with these classes. Future controllers would call into the domain layer through normalizers or service helpers to persist entities and check permissions.
