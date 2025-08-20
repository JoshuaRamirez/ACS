# ACS WebAPI Security Tests

This project contains comprehensive security tests for the ACS (Access Control System) WebAPI to ensure the application is protected against common security vulnerabilities and follows security best practices.

## Test Categories

### 1. Authentication Security Tests (`AuthenticationSecurityTests.cs`)
Tests authentication and authorization mechanisms:
- **JWT Token Security**: Valid/invalid credential handling, token validation, expiration
- **SQL Injection in Auth**: Protection against SQL injection in login endpoints
- **Brute Force Protection**: Rate limiting for login attempts
- **Token Content Security**: Ensures no sensitive data in JWT payload
- **Role-based Access Control**: Admin vs user access to protected endpoints

### 2. SQL Injection Security Tests (`SqlInjectionSecurityTests.cs`)
Comprehensive SQL injection protection testing:
- **Parameter Injection**: Testing malicious SQL in user IDs, search queries
- **Union-based Attacks**: UNION SELECT injection attempts
- **Boolean-based Attacks**: Conditional SQL injection
- **Time-based Attacks**: Timing attack prevention
- **Bulk Operations**: SQL injection in batch processing
- **Database Integrity**: Verification that attacks don't compromise data

### 3. Cross-Site Scripting (XSS) Tests (`XssSecurityTests.cs`)
XSS vulnerability testing across input vectors:
- **Stored XSS**: Script injection in user data, group descriptions
- **Reflected XSS**: Script injection in search parameters and error messages
- **DOM XSS**: Client-side script injection
- **Multiple Payload Types**: Various XSS attack vectors
- **Content Security Policy**: CSP header validation
- **Output Encoding**: Proper HTML encoding verification

### 4. CSRF Protection Tests (`CsrfSecurityTests.cs`)
Cross-Site Request Forgery protection:
- **Token Validation**: CSRF token requirement for state-changing operations
- **Token Uniqueness**: Session-specific token generation
- **Safe Methods**: GET/HEAD requests don't require CSRF tokens
- **Double Submit Cookie**: Cookie-to-header CSRF protection
- **Bulk Operations**: CSRF protection in batch endpoints

### 5. Security Headers Tests (`SecurityHeadersTests.cs`)
HTTP security headers validation:
- **X-Content-Type-Options**: MIME type sniffing prevention
- **X-Frame-Options**: Clickjacking protection
- **Content Security Policy**: Script execution restrictions
- **Strict Transport Security**: HTTPS enforcement (when applicable)
- **Referrer Policy**: Referrer information control
- **Information Disclosure**: Prevention of server information exposure

### 6. Input Validation Tests (`InputValidationSecurityTests.cs`)
Comprehensive input validation and sanitization:
- **Length Validation**: Protection against buffer overflow attacks
- **Format Validation**: Email, password, and data format enforcement
- **Character Encoding**: Special character and Unicode handling
- **Malicious Input**: Script tags, SQL commands, path traversal attempts
- **Content Type Validation**: Proper media type handling
- **Payload Size Limits**: Protection against DoS via large payloads

## Test Infrastructure

### SecurityTestWebApplicationFactory
Custom `WebApplicationFactory` for security testing:
- In-memory database for isolated testing
- Disabled HTTPS redirection for testing
- Enhanced logging for security events
- Specialized configuration for security scenarios

### SecurityTestBase
Base class providing common security test utilities:
- JWT token generation for authentication testing
- CSRF token extraction from responses
- Authorization header management
- Test client configuration

## Security Test Patterns

### 1. Negative Security Testing
Tests focus on what the application **should not** allow:
```csharp
[TestMethod]
public async Task Login_WithSqlInjectionAttempt_ShouldReturnBadRequest()
{
    // Arrange - Malicious input
    var maliciousLogin = new { Username = "admin'; DROP TABLE Users; --", Password = "password" };
    
    // Act - Attempt attack
    var response = await Client.PostAsync("/api/auth/login", content);
    
    // Assert - Attack should be blocked
    response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
}
```

### 2. Security Header Validation
Ensures proper security headers are present:
```csharp
[TestMethod]
public async Task GetRequest_ShouldIncludeXContentTypeOptionsHeader()
{
    var response = await Client.GetAsync("/api/users");
    response.Headers.Should().ContainKey("X-Content-Type-Options");
    response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
}
```

### 3. Input Boundary Testing
Tests edge cases and malicious input:
```csharp
[TestMethod]
public async Task CreateUser_WithExcessivelyLongName_ShouldReturnBadRequest()
{
    var longName = new string('A', 10000);
    var user = new { Name = longName, Email = "test@example.com" };
    // ... should be rejected
}
```

## Running Security Tests

### Prerequisites
- .NET 8.0 SDK
- ACS WebAPI project
- Test database (in-memory for isolation)

### Execution
```bash
# Run all security tests
dotnet test ACS.WebApi.Tests.Security

# Run specific test category
dotnet test ACS.WebApi.Tests.Security --filter "ClassName=AuthenticationSecurityTests"

# Run with verbose output
dotnet test ACS.WebApi.Tests.Security --verbosity normal
```

### Test Coverage
Security tests cover:
- ✅ **Authentication & Authorization**: JWT, RBAC, session management
- ✅ **Injection Attacks**: SQL injection, NoSQL injection, command injection
- ✅ **Cross-Site Scripting**: Stored, reflected, and DOM-based XSS
- ✅ **CSRF Protection**: Token validation and state-changing operations
- ✅ **Security Headers**: OWASP recommended security headers
- ✅ **Input Validation**: Length, format, encoding, and sanitization
- ✅ **Information Disclosure**: Preventing data leakage in responses
- ✅ **Rate Limiting**: Brute force and DoS protection

## Security Standards Compliance

These tests help ensure compliance with:
- **OWASP Top 10**: Covers major web application security risks
- **NIST Cybersecurity Framework**: Security controls implementation
- **ISO 27001**: Information security management
- **PCI DSS**: Payment card industry security standards
- **GDPR**: Data protection and privacy requirements

## Continuous Security Testing

### Integration with CI/CD
```yaml
# Example GitHub Actions workflow step
- name: Run Security Tests
  run: dotnet test ACS.WebApi.Tests.Security --configuration Release --logger trx --results-directory TestResults
  
- name: Security Test Results
  uses: dorny/test-reporter@v1
  if: success() || failure()
  with:
    name: Security Test Results
    path: TestResults/*.trx
    reporter: dotnet-trx
```

### Security Regression Testing
- Run security tests on every code change
- Automated security scanning in build pipeline
- Security test results as deployment gates
- Regular security test maintenance and updates

## Best Practices

### 1. Defense in Depth
Security tests validate multiple layers:
- Input validation at API boundary
- Authentication and authorization
- Data access controls
- Output encoding and headers

### 2. Fail Secure
Tests ensure secure defaults:
- Unauthorized requests are denied
- Invalid input is rejected
- Errors don't expose sensitive information
- Security headers are always present

### 3. Comprehensive Coverage
Tests cover various attack vectors:
- Known vulnerability patterns (OWASP Top 10)
- Edge cases and boundary conditions
- Different input types and sources
- Multiple authentication scenarios

## Maintenance

### Regular Updates
- Update test cases for new vulnerabilities
- Review and enhance security patterns
- Update security headers and policies
- Add tests for new features and endpoints

### Security Test Review
- Review tests with security team
- Validate against current threat landscape
- Update tests based on security audits
- Ensure tests reflect production configuration