# Test Improvement Initiative

**Status:** In Progress

## Pending Tasks
- Review existing tests
- Increase coverage

## Current Tasks
- Expand test coverage

## Completed Tasks
- Created unit and integration test project scaffolds for each assembly
- Initial placeholder created
- Added basic integration tests for the Web API
- Removed service layer and WeatherForecast endpoint tests per updated requirements
- Added unit tests exercising normalizers
- Extended normalizer tests for group and role membership
- Added tests verifying domain methods trigger normalizers
- Added normalizer and domain tests for group hierarchy management
- Added tests for child-side group operations and cycle prevention
- Fixed null reference errors in group and membership normalizers so service tests run cleanly
- Converted group and membership normalizers to throw exceptions when collections or targets are missing
- Added tests ensuring normalizers throw when their backing collections are null
- Added guard tests for missing parents, groups, roles, and users in normalizers

