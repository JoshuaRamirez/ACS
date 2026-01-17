# ACS WebAPI End-to-End Tests

This project contains comprehensive end-to-end tests for the ACS (Access Control System) WebAPI that validate complete business workflows and user scenarios from a user's perspective.

## Test Categories

### 1. User Management E2E Tests (`Scenarios/UserManagementE2ETests.cs`)
Tests complete user lifecycle and management workflows:

#### **Complete User Lifecycle Test**
- **User Creation**: Admin creates new user with credentials
- **Group Assignment**: User added to appropriate department/team groups
- **Role Assignment**: User assigned appropriate roles and permissions
- **First Login**: New user logs in and accesses their profile
- **Permission Verification**: User can access permitted resources
- **Profile Updates**: Admin updates user information
- **Account Deactivation**: User account suspended and login blocked
- **Account Reactivation**: User account restored and access verified

#### **Permission Inheritance Test**
- **Initial State**: User starts with no permissions
- **Group Membership**: User added to groups and inherits permissions
- **Role Assignment**: User assigned roles and gains additional permissions
- **Access Verification**: User can access resources based on inherited permissions
- **Role Promotion**: User promoted to higher role with expanded permissions
- **Permission Testing**: Verify user can perform operations based on new role

#### **Bulk Operations Test**
- **Bulk User Creation**: Multiple users created simultaneously
- **Group Assignment**: Users assigned to groups in bulk operations
- **Role Assignment**: Roles assigned to multiple users at once
- **Concurrent Updates**: Multiple user updates processed simultaneously
- **Data Consistency**: All bulk operations maintain referential integrity
- **Bulk Deactivation**: Multiple users deactivated together

#### **Search and Filtering Test**
- **Name Search**: Search users by name with partial matching
- **Email Search**: Search users by email domain or pattern
- **Status Filtering**: Filter users by active/inactive status
- **Pagination**: Test pagination with various page sizes
- **Sorting**: Test sorting by different user attributes
- **Complex Filtering**: Multi-criteria filtering with role and group filters

### 2. Permission Management E2E Tests (`Scenarios/PermissionManagementE2ETests.cs`)
Tests authorization and permission workflows:

#### **Role-Based Access Control Test**
- **Administrator Access**: Full system access to all endpoints and operations
- **Manager Access**: Limited administrative access with user management
- **Employee Access**: Read-only access to appropriate resources
- **Guest Access**: Minimal access with strict limitations
- **Unauthorized Access**: Verify unauthenticated requests are blocked

#### **Permission Inheritance Test**
- **Group Hierarchy**: Create nested group structure (Parent → Child)
- **Role Assignment**: Assign roles to parent groups
- **User Assignment**: Add users to child groups
- **Inheritance Verification**: Users inherit permissions from parent groups
- **Access Testing**: Verify users can perform inherited operations

#### **Dynamic Permission Changes Test**
- **Initial Permissions**: User starts with Employee (read-only) permissions
- **Permission Verification**: Confirm user cannot perform write operations
- **Role Upgrade**: Admin promotes user to Manager role
- **Immediate Effect**: Verify new permissions take effect immediately
- **Role Downgrade**: Admin removes Manager role from user
- **Permission Revocation**: Verify write permissions are immediately revoked

#### **Resource-Based Permissions Test**
- **Custom Role Creation**: Create role with specific resource permissions
- **Limited Access**: User can read and update but not create or delete
- **Resource Boundaries**: User cannot access unauthorized resources
- **Operation Restrictions**: Specific HTTP methods blocked based on permissions

#### **Audit Trail Test**
- **Permission Changes**: Track role assignments and removals
- **Group Membership**: Track group membership changes
- **Audit Completeness**: Verify all permission changes are logged
- **Audit Details**: Verify audit entries contain required information

#### **Conditional Permissions Test**
- **Self-Access**: Users can access their own profiles
- **Cross-User Restrictions**: Users cannot access other users' data
- **Modification Restrictions**: Users cannot modify other users' profiles
- **Security Boundaries**: Verify access control boundaries are maintained

### 3. Complete Workflow E2E Tests (`Scenarios/WorkflowE2ETests.cs`)
Tests end-to-end business processes:

#### **New Employee Onboarding Workflow**
- **Account Creation**: HR creates new employee account
- **Department Assignment**: Employee assigned to department group
- **Role Assignment**: Basic employee role assigned
- **Resource Provisioning**: Workspace and resource access created
- **First Login**: Employee logs in and accesses basic resources
- **Permission Verification**: Employee has appropriate access levels
- **Audit Trail**: Complete onboarding process is audited

#### **Department Reorganization Workflow**
- **Initial Structure**: Create department with sub-teams
- **Employee Assignment**: Assign employees to teams
- **Reorganization**: Create new teams and move employees
- **Permission Updates**: Update roles and permissions during move
- **Access Verification**: Verify employees have correct new permissions
- **Data Integrity**: Ensure all relationships remain consistent
- **Audit Documentation**: All organizational changes are tracked

#### **Security Incident Response Workflow**
- **Normal Operations**: User performs normal activities
- **Incident Detection**: Suspicious activity identified
- **Immediate Response**: User account suspended instantly
- **Access Revocation**: All permissions and group memberships removed
- **Password Reset**: Force password change for security
- **Audit Investigation**: Generate detailed activity reports
- **System Integrity**: Verify other users unaffected
- **Recovery Preparation**: Document incident and response actions

#### **Compliance Audit Workflow**
- **Access Reports**: Generate user access and permission reports
- **Role Reports**: Document all role assignments and hierarchies
- **Group Reports**: Document group memberships and structure
- **Audit Logs**: Generate comprehensive audit activity reports
- **Data Export**: Export audit data for compliance review
- **Health Verification**: Verify system security and integrity
- **Permission Compliance**: Verify proper access control enforcement
- **Compliance Summary**: Generate summary reports for auditors

## Test Infrastructure

### E2ETestWebApplicationFactory
Custom `WebApplicationFactory` for realistic end-to-end testing:
- **Database Options**: Configurable in-memory or SQL Server database
- **Comprehensive Seeding**: Realistic test data with proper relationships
- **Logging Configuration**: Detailed logging for test debugging
- **Environment Simulation**: Testing environment configuration

### E2ETestBase
Base class providing common E2E test utilities:
- **Authentication Management**: Login/logout with different user roles
- **HTTP Helpers**: Simplified GET/POST/PUT/DELETE operations
- **JSON Serialization**: Automatic request/response JSON handling
- **Authorization Headers**: Automatic token management
- **Pagination Support**: Helper methods for paginated results
- **Wait Conditions**: Async wait utilities for eventual consistency

### Test Data
Comprehensive test data representing realistic scenarios:
- **Users**: Admin, Manager, Employees with different roles
- **Groups**: Hierarchical department and team structure
- **Roles**: Administrator, Manager, Employee, Guest with appropriate permissions
- **Resources**: API endpoints with different access levels
- **Permissions**: Realistic permission assignments based on business roles

## Running E2E Tests

### Prerequisites
- .NET 8.0 SDK
- ACS WebAPI application
- SQL Server (optional, for realistic database testing)
- Chrome/ChromeDriver (for UI tests if added)

### Test Execution
```bash
# Run all E2E tests
dotnet test ACS.WebApi.Tests.E2E

# Run specific scenario tests
dotnet test ACS.WebApi.Tests.E2E --filter "ClassName=UserManagementE2ETests"
dotnet test ACS.WebApi.Tests.E2E --filter "ClassName=PermissionManagementE2ETests"
dotnet test ACS.WebApi.Tests.E2E --filter "ClassName=WorkflowE2ETests"

# Run with detailed output
dotnet test ACS.WebApi.Tests.E2E --verbosity normal --logger "console;verbosity=detailed"

# Run with real database (for more realistic testing)
dotnet test ACS.WebApi.Tests.E2E --environment "E2E_USE_REAL_DB=true"
```

### Environment Variables
- `E2E_USE_REAL_DB=true`: Use SQL Server instead of in-memory database
- `E2E_DETAILED_LOGGING=true`: Enable detailed logging for debugging
- `E2E_TIMEOUT_MINUTES=10`: Set timeout for long-running tests

## Test Scenarios Coverage

### Business Process Coverage
- ✅ **User Lifecycle Management**: Complete CRUD operations with audit
- ✅ **Permission Management**: Role-based and resource-based access control
- ✅ **Group Hierarchy**: Multi-level organizational structure
- ✅ **Bulk Operations**: Mass user and permission management
- ✅ **Search and Filtering**: Complex query and pagination scenarios
- ✅ **Security Incident Response**: Rapid access revocation and investigation
- ✅ **Compliance Auditing**: Report generation and data export
- ✅ **Organizational Change**: Department restructuring and role changes

### Security Testing Coverage
- ✅ **Authentication**: Login/logout workflows with various user types
- ✅ **Authorization**: Role-based access control enforcement
- ✅ **Permission Inheritance**: Group-based permission propagation
- ✅ **Access Control**: Resource-level permission verification
- ✅ **Audit Logging**: Comprehensive activity tracking
- ✅ **Security Boundaries**: Cross-user access prevention
- ✅ **Dynamic Permissions**: Real-time permission updates

### Data Integrity Testing
- ✅ **Referential Integrity**: Consistent relationships during operations
- ✅ **Concurrent Operations**: Multi-user simultaneous access
- ✅ **Bulk Operations**: Large-scale data consistency
- ✅ **Transaction Boundaries**: Proper error handling and rollback
- ✅ **Audit Completeness**: No missing audit entries

## Integration with CI/CD

### Pipeline Integration
```yaml
# Example GitHub Actions workflow step
- name: Run E2E Tests
  run: |
    dotnet test ACS.WebApi.Tests.E2E \
      --configuration Release \
      --logger trx \
      --results-directory TestResults \
      --verbosity normal
  env:
    E2E_USE_REAL_DB: false
    E2E_TIMEOUT_MINUTES: 15

- name: E2E Test Results
  uses: dorny/test-reporter@v1
  if: success() || failure()
  with:
    name: E2E Test Results
    path: TestResults/*.trx
    reporter: dotnet-trx
```

### Performance Considerations
- **Test Duration**: E2E tests typically run 5-15 minutes
- **Database Setup**: In-memory database for speed, SQL Server for realism
- **Parallel Execution**: Tests designed for parallel execution where possible
- **Resource Cleanup**: Automatic cleanup to prevent test interference

## Best Practices

### Test Design
1. **Realistic Scenarios**: Tests mirror actual business workflows
2. **Data Independence**: Each test uses isolated test data
3. **Error Handling**: Tests verify both success and failure paths
4. **Comprehensive Coverage**: Tests cover happy path and edge cases

### Maintenance
1. **Regular Updates**: Update tests when business requirements change
2. **Data Refresh**: Keep test data current with production patterns
3. **Performance Monitoring**: Track test execution time and optimize
4. **Documentation**: Keep README updated with new scenarios

### Debugging
1. **Detailed Logging**: Enable verbose logging for test failures
2. **Step-by-Step Verification**: Each workflow step is individually verified
3. **Audit Trail Review**: Use audit logs to debug permission issues
4. **Database State**: Inspect database state at failure points

## Troubleshooting

### Common Issues
1. **Authentication Failures**: Check test user credentials and token generation
2. **Permission Denials**: Verify role and group assignments in test data
3. **Database Conflicts**: Ensure proper test isolation and cleanup
4. **Timing Issues**: Use wait conditions for asynchronous operations

### Debug Helpers
- Enable detailed logging with `E2E_DETAILED_LOGGING=true`
- Use real database for debugging with `E2E_USE_REAL_DB=true`
- Check audit logs for permission and access issues
- Verify test data seeding completed successfully