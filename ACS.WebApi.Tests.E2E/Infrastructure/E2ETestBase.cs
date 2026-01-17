using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ACS.WebApi.Tests.E2E.Infrastructure;

/// <summary>
/// Base class for end-to-end tests with common utilities and scenarios
/// </summary>
public abstract class E2ETestBase
{
    protected E2ETestWebApplicationFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    // Test user credentials
    protected readonly Dictionary<string, UserCredential> TestUsers = new()
    {
        ["admin"] = new("admin@company.com", "Admin123!", "Administrator"),
        ["manager"] = new("john.smith@company.com", "Manager123!", "Manager"),
        ["employee"] = new("alice.johnson@company.com", "Employee123!", "Employee"),
        ["employee2"] = new("bob.wilson@company.com", "Employee123!", "Employee"),
        ["employee3"] = new("carol.davis@company.com", "Employee123!", "Employee"),
        ["inactive"] = new("david.brown@company.com", "Employee123!", "Employee")
    };

    protected virtual async Task InitializeAsync(bool useRealDatabase = false)
    {
        Factory = new E2ETestWebApplicationFactory(useRealDatabase);
        Client = Factory.CreateClient();
        await Factory.SeedE2ETestDataAsync();
    }

    protected virtual async Task CleanupAsync()
    {
        Client?.Dispose();
        if (Factory != null)
        {
            await Factory.DisposeAsync();
        }
    }

    protected async Task<string> LoginAsync(string userKey)
    {
        if (!TestUsers.TryGetValue(userKey, out var credential))
        {
            throw new ArgumentException($"Unknown test user: {userKey}");
        }

        var loginRequest = new
        {
            Email = credential.Email,
            Password = credential.Password
        };

        var content = new StringContent(
            JsonSerializer.Serialize(loginRequest),
            Encoding.UTF8,
            "application/json");

        var response = await Client.PostAsync("/api/auth/login", content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var loginResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

        if (loginResponse.TryGetProperty("token", out var tokenElement))
        {
            var token = tokenElement.GetString();
            if (!string.IsNullOrEmpty(token))
            {
                // Set authorization header for subsequent requests
                Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return token;
            }
        }

        throw new InvalidOperationException($"Failed to get token for user {userKey}");
    }

    protected void SetAuthorizationHeader(string token)
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    protected void ClearAuthorizationHeader()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }

    protected async Task<T?> GetJsonAsync<T>(string requestUri)
    {
        var response = await Client.GetAsync(requestUri);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    protected async Task<HttpResponseMessage> PostJsonAsync<T>(string requestUri, T data)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(data),
            Encoding.UTF8,
            "application/json");

        return await Client.PostAsync(requestUri, content);
    }

    protected async Task<HttpResponseMessage> PutJsonAsync<T>(string requestUri, T data)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(data),
            Encoding.UTF8,
            "application/json");

        return await Client.PutAsync(requestUri, content);
    }

    protected async Task<bool> VerifyUserCanAccessEndpoint(string userKey, string endpoint, HttpMethod method, object? data = null)
    {
        await LoginAsync(userKey);

        try
        {
            HttpResponseMessage response;

            switch (method.Method)
            {
                case "GET":
                    response = await Client.GetAsync(endpoint);
                    break;
                case "POST":
                    response = await PostJsonAsync(endpoint, data ?? new { });
                    break;
                case "PUT":
                    response = await PutJsonAsync(endpoint, data ?? new { });
                    break;
                case "DELETE":
                    response = await Client.DeleteAsync(endpoint);
                    break;
                default:
                    throw new NotSupportedException($"HTTP method {method.Method} not supported");
            }

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    protected async Task<bool> VerifyUserCannotAccessEndpoint(string userKey, string endpoint, HttpMethod method, object? data = null)
    {
        return !await VerifyUserCanAccessEndpoint(userKey, endpoint, method, data);
    }

    protected async Task WaitForAsync(Func<Task<bool>> condition, TimeSpan timeout, string? errorMessage = null)
    {
        var start = DateTime.UtcNow;
        
        while (DateTime.UtcNow - start < timeout)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        throw new TimeoutException(errorMessage ?? "Condition was not met within the specified timeout");
    }

    protected async Task<List<T>> GetPagedResultsAsync<T>(string baseEndpoint, int? maxPages = null)
    {
        var allResults = new List<T>();
        var page = 1;
        var pageSize = 20;
        var pagesChecked = 0;

        while (maxPages == null || pagesChecked < maxPages)
        {
            var endpoint = $"{baseEndpoint}?page={page}&size={pageSize}";
            var response = await Client.GetAsync(endpoint);
            
            if (!response.IsSuccessStatusCode)
            {
                break;
            }

            var content = await response.Content.ReadAsStringAsync();
            var pageResult = JsonSerializer.Deserialize<PagedResult<T>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (pageResult?.Items == null || !pageResult.Items.Any())
            {
                break;
            }

            allResults.AddRange(pageResult.Items);
            
            if (pageResult.Items.Count < pageSize)
            {
                break; // Last page
            }

            page++;
            pagesChecked++;
        }

        return allResults;
    }

    protected record UserCredential(string Email, string Password, string Role);

    protected class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasPrevious { get; set; }
        public bool HasNext { get; set; }
    }

    protected class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    protected class UserDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<string> Roles { get; set; } = new();
        public List<string> Groups { get; set; } = new();
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }
    }

    protected class GroupDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int? ParentGroupId { get; set; }
        public List<UserDto> Users { get; set; } = new();
        public List<GroupDto> ChildGroups { get; set; } = new();
    }

    protected class RoleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
    }

    protected class AuditLogDto
    {
        public int Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Details { get; set; } = string.Empty;
    }
}