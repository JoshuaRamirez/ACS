# Developer Guide

This guide explains how to set up the development environment and contribute changes.

## Setup
Install the .NET 8 SDK and a recent version of SQL Server. Run `scripts/setup_dotnet.sh` to install the SDK. In Codex environments the script will detect `apt-get` and install `dotnet-sdk-8.0` from the package repositories when direct downloads are blocked. Clone the repository and open `ACS.sln` using your preferred IDE.

## Coding Standards
Follow standard C# conventions and keep business logic within the service layer. Database schema definitions belong in the SQL project.

## Contribution Guide
Fork the repository, create a new branch, and submit a pull request describing your changes.

## Running Tests
After installing the .NET SDK, execute `dotnet test` from the repository root to run all unit and integration tests.
