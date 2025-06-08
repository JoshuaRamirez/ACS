# Web API Overview

This section describes the HTTP interface provided by the `ACS.WebApi` project.

## Endpoints
The default controller exposes a `/WeatherForecast` endpoint that returns sample data. Additional controllers can be added following the same pattern.

## Authentication
Authentication is not enabled in the sample project but can be added using standard ASP.NET Core mechanisms such as JWT bearer tokens.

## Error Handling
Requests that fail validation or encounter server errors return appropriate HTTP status codes along with problem details where possible.
