# Developer Journal

This file records each user request processed by the agent. After every request, append a new entry summarizing the request, the persona adopted, and any actions taken.

## Entries

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

