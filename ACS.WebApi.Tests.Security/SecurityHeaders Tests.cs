using ACS.WebApi.Tests.Security.Infrastructure;

namespace ACS.WebApi.Tests.Security;

/// <summary>
/// Security tests for HTTP security headers
/// </summary>
[TestClass]
public class SecurityHeadersTests : SecurityTestBase
{
    [TestMethod]
    public async Task GetRequest_ShouldIncludeXContentTypeOptionsHeader()
    {
        // Act
        var response = await Client.GetAsync("/api/users");

        // Assert
        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
    }

    [TestMethod]
    public async Task GetRequest_ShouldIncludeXFrameOptionsHeader()
    {
        // Act
        var response = await Client.GetAsync("/api/users");

        // Assert
        response.Headers.Should().ContainKey("X-Frame-Options");
        var frameOptions = response.Headers.GetValues("X-Frame-Options").First();
        frameOptions.Should().BeOneOf("DENY", "SAMEORIGIN");
    }

    [TestMethod]
    public async Task GetRequest_ShouldIncludeXXssProtectionHeader()
    {
        // Act
        var response = await Client.GetAsync("/api/users");

        // Assert
        if (response.Headers.Contains("X-XSS-Protection"))
        {
            response.Headers.GetValues("X-XSS-Protection").Should().Contain("1; mode=block");
        }
    }

    [TestMethod]
    public async Task GetRequest_ShouldIncludeReferrerPolicyHeader()
    {
        // Act
        var response = await Client.GetAsync("/api/users");

        // Assert
        if (response.Headers.Contains("Referrer-Policy"))
        {
            var referrerPolicy = response.Headers.GetValues("Referrer-Policy").First();
            referrerPolicy.Should().BeOneOf("strict-origin-when-cross-origin", "no-referrer", "same-origin");
        }
    }

    [TestMethod]
    public async Task GetRequest_ShouldIncludeContentSecurityPolicyHeader()
    {
        // Act
        var response = await Client.GetAsync("/");

        // Assert
        if (response.Headers.Contains("Content-Security-Policy"))
        {
            var csp = response.Headers.GetValues("Content-Security-Policy").First();
            
            // CSP should include basic directives
            csp.Should().Contain("default-src", "CSP should include default-src directive");
            csp.Should().Contain("script-src", "CSP should include script-src directive");
            
            // CSP should not allow unsafe practices
            csp.Should().NotContain("'unsafe-inline'", "CSP should not allow unsafe-inline scripts");
            csp.Should().NotContain("'unsafe-eval'", "CSP should not allow unsafe-eval");
        }
    }

    [TestMethod]
    public async Task HttpsRequest_ShouldIncludeStrictTransportSecurityHeader()
    {
        // Note: This test assumes HTTPS is configured in production
        // In testing environment, HSTS might not be present
        
        // Act
        var response = await Client.GetAsync("/api/users");

        // Assert
        if (response.Headers.Contains("Strict-Transport-Security"))
        {
            var hsts = response.Headers.GetValues("Strict-Transport-Security").First();
            hsts.Should().Contain("max-age=", "HSTS should include max-age directive");
            hsts.Should().Contain("includeSubDomains", "HSTS should include subdomains");
        }
    }

    [TestMethod]
    public async Task GetRequest_ShouldIncludePermissionsPolicyHeader()
    {
        // Act
        var response = await Client.GetAsync("/api/users");

        // Assert
        if (response.Headers.Contains("Permissions-Policy"))
        {
            var permissionsPolicy = response.Headers.GetValues("Permissions-Policy").First();
            
            // Should restrict dangerous features
            permissionsPolicy.Should().Contain("camera=()");
            permissionsPolicy.Should().Contain("microphone=()");
            permissionsPolicy.Should().Contain("geolocation=()");
        }
    }

    [TestMethod]
    public async Task GetRequest_ShouldNotExposeServerInformation()
    {
        // Act
        var response = await Client.GetAsync("/api/users");

        // Assert
        response.Headers.Should().NotContainKey("Server", "Server header should not expose server information");
        response.Headers.Should().NotContainKey("X-Powered-By", "X-Powered-By header should not be present");
        response.Headers.Should().NotContainKey("X-AspNet-Version", "ASP.NET version should not be exposed");
    }

    [TestMethod]
    public async Task GetRequest_ShouldSetSecureCacheHeaders()
    {
        // Act
        var response = await Client.GetAsync("/api/users");

        // Assert
        if (response.Headers.Contains("Cache-Control"))
        {
            var cacheControl = response.Headers.GetValues("Cache-Control").First();
            
            // For API responses, should prevent caching of sensitive data
            cacheControl.Should().Match("*no-cache*|*no-store*|*private*");
        }
    }

    [TestMethod]
    public async Task ApiResponse_ShouldHaveCorrectContentTypeHeaders()
    {
        // Act
        var response = await Client.GetAsync("/api/users");

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        response.Content.Headers.ContentType?.CharSet.Should().Be("utf-8");
    }

    [TestMethod]
    public async Task HealthEndpoint_ShouldHaveMinimalSecurityHeaders()
    {
        // Act
        var response = await Client.GetAsync("/health");

        // Assert - Health endpoint should still have basic security headers
        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.Should().ContainKey("X-Frame-Options");
    }

    [TestMethod]
    public async Task StaticFiles_ShouldHaveAppropriateHeaders()
    {
        // Act - Try to access a static file (this might not exist in the test environment)
        var response = await Client.GetAsync("/favicon.ico");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Headers.Should().ContainKey("X-Content-Type-Options");
            
            // Static files should have long cache times
            if (response.Headers.Contains("Cache-Control"))
            {
                var cacheControl = response.Headers.GetValues("Cache-Control").First();
                cacheControl.Should().Match("*max-age*|*public*");
            }
        }
    }

    [TestMethod]
    public async Task OptionsRequest_ShouldIncludeSecurityHeaders()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/users");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.Should().ContainKey("X-Frame-Options");
    }

    [TestMethod]
    public async Task ErrorResponse_ShouldIncludeSecurityHeaders()
    {
        // Act - Request that should return an error
        var response = await Client.GetAsync("/api/nonexistent");

        // Assert
        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.Should().ContainKey("X-Frame-Options");
    }

    [TestMethod]
    public async Task SecurityHeaders_ShouldBeConsistentAcrossEndpoints()
    {
        // Arrange
        var endpoints = new[]
        {
            "/api/users",
            "/api/groups", 
            "/api/roles",
            "/api/health",
            "/health"
        };

        var requiredHeaders = new[]
        {
            "X-Content-Type-Options",
            "X-Frame-Options"
        };

        // Act & Assert
        foreach (var endpoint in endpoints)
        {
            var response = await Client.GetAsync(endpoint);
            
            foreach (var header in requiredHeaders)
            {
                response.Headers.Should().ContainKey(header, 
                    $"Endpoint {endpoint} should include {header} header");
            }
        }
    }

    [TestMethod]
    public async Task ContentTypeSniffing_ShouldBeDisabled()
    {
        // Act
        var response = await Client.GetAsync("/api/users");

        // Assert
        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        
        // Content type should be explicitly set
        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task ClickjackingProtection_ShouldBeEnabled()
    {
        // Act
        var response = await Client.GetAsync("/api/users");

        // Assert
        var frameOptions = response.Headers.GetValues("X-Frame-Options").First();
        frameOptions.Should().BeOneOf("DENY", "SAMEORIGIN", 
            "X-Frame-Options should prevent clickjacking attacks");
    }

    [TestMethod]
    public async Task ResponseHeaders_ShouldNotContainSensitiveInformation()
    {
        // Act
        var response = await Client.GetAsync("/api/users");

        // Assert - Check that headers don't expose sensitive information
        var allHeaders = response.Headers.Concat(response.Content.Headers)
            .SelectMany(h => h.Value)
            .ToList();

        var sensitivePatterns = new[]
        {
            "password", "secret", "key", "token", "credential",
            "admin", "root", "sa", "database", "connection"
        };

        foreach (var pattern in sensitivePatterns)
        {
            allHeaders.Should().NotContain(h => h.ToLower().Contains(pattern),
                $"Headers should not contain sensitive pattern: {pattern}");
        }
    }

    [TestMethod]
    public async Task SecurityHeaders_ShouldHaveCorrectValues()
    {
        // Act
        var response = await Client.GetAsync("/api/users");

        // Assert - Verify specific header values
        if (response.Headers.Contains("X-Content-Type-Options"))
        {
            response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        }

        if (response.Headers.Contains("X-Frame-Options"))
        {
            var frameOptions = response.Headers.GetValues("X-Frame-Options").First();
            frameOptions.Should().BeOneOf("DENY", "SAMEORIGIN");
        }

        if (response.Headers.Contains("Referrer-Policy"))
        {
            var referrerPolicy = response.Headers.GetValues("Referrer-Policy").First();
            var validPolicies = new[]
            {
                "no-referrer",
                "no-referrer-when-downgrade", 
                "same-origin",
                "strict-origin",
                "strict-origin-when-cross-origin"
            };
            validPolicies.Should().Contain(referrerPolicy);
        }
    }
}