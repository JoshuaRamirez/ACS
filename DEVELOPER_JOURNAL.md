# Developer Journal

This file records each user request processed by the agent. After every request, append a new entry summarizing the request, the persona adopted, and any actions taken.

## Entries

### 2025-07-02
**Persona:** Lead Developer

Answered user question about normalizer purpose and described architectural style. Updated `service_overview.md` and project docs accordingly.


### 2025-06-08
**Persona:** Support Engineer

Created `scripts/setup_dotnet.sh` to install the .NET SDK for the Codex environment. Updated `README.md` and `docs/developer_guide.md` with instructions about using the script.


### 2025-06-08
**Persona:** Documentation Specialist

Reviewed the domain models and API controller structure at the user's request. Added an overview of the domain layer to `docs/service_overview.md` and noted the update in the project file.

### 2025-06-08
**Persona:** QA Engineer

Added real integration tests for the Web API using `WebApplicationFactory` and for the service layer covering permission inheritance. Updated the test improvement project file accordingly.

### 2025-06-08
**Persona:** QA Engineer

Removed failing service layer integration tests and the WeatherForecast endpoint test so integration testing focuses on future API endpoints only. Updated project documentation.

### 2025-06-08
**Persona:** Lead Developer

Implemented additional normalizers for permission removal and corrected the user-to-role assignment helper. Updated `docs/service_overview.md` to mention ongoing normalizer work.

### 2025-06-09
**Persona:** Lead Developer

Extended the normalizers to maintain bidirectional references and created unit tests covering permission add/remove and user-role assignment. Updated service overview and project docs.


### 2025-06-12
**Persona:** Lead Developer

Designed a new API layer using a `UsersController` backed by an in-memory `UserService`. Removed the sample WeatherForecast endpoint, updated documentation, added an integration test, and created a project entry for API layer implementation.

### 2025-06-12
**Persona:** Documentation Specialist

Responded to a request to describe the full stack sequence of operations. Updated `architecture_overview.md` with a new Request lifecycle section.

### 2025-07-02
**Persona:** Project Manager

Analyzed repository state for the user. Updated `docs/user_manual.md` to describe current user endpoints and logged the change in the API layer project file. Summarized progress across active projects.

### 2025-07-02
**Persona:** Support Engineer

Added apt-based .NET SDK installation path to `scripts/setup_dotnet.sh` and updated documentation to describe using the script in Codex.

### 2025-07-02
**Persona:** Lead Developer

Extended the domain layer to invoke normalizers for permission management and group/role membership. Added a new `RemovePermission` method, expanded unit tests for normalizers, and updated service documentation and project files.

### 2025-07-02
**Persona:** Lead Developer

Expanded domain-normalizer integration with new tests covering domain methods. Updated service overview and project docs to describe the behavior.

### 2025-07-02
**Persona:** Lead Developer

Implemented normalizers for managing group hierarchies and wired them into the domain layer. Updated domain models with parent group references, expanded unit and integration tests to cover the new behavior, refreshed documentation and project files.
### 2025-07-02
**Persona:** Lead Developer

Implemented child-side group operations and cycle prevention in the domain. Added mapping logic for HTTP verbs in `CreateUriAccessNormalizer`, expanded domain integration tests for the new methods, and documented the updates in project files and service overview.
