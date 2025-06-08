# Service Overview

The service exposes a simple REST API that can be expanded with additional endpoints. Its default controller demonstrates how to return data from the service layer.

## Core services
The primary service project is **ACS.Service**, which provides command and query handlers. These handlers can be used by the Web API or other clients.

## Interfaces
The Web API publishes controllers in the `ACS.WebApi` project. Requests are routed to the appropriate handler within the service layer.

## Workflows
A typical request passes through the API, invokes a handler, performs database operations, and returns a response in JSON format.
