using ACS.Service.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Domain.Specifications;

/// <summary>
/// Service for executing specifications against the database
/// </summary>
public interface ISpecificationService
{
    /// <summary>
    /// Executes a specification query against entities
    /// </summary>
    Task<IEnumerable<T>> QueryAsync<T>(ISpecification<T> specification) where T : class;
    
    /// <summary>
    /// Counts entities matching a specification
    /// </summary>
    Task<int> CountAsync<T>(ISpecification<T> specification) where T : class;
    
    /// <summary>
    /// Checks if any entities match a specification
    /// </summary>
    Task<bool> AnyAsync<T>(ISpecification<T> specification) where T : class;
    
    /// <summary>
    /// Gets the first entity matching a specification
    /// </summary>
    Task<T?> FirstOrDefaultAsync<T>(ISpecification<T> specification) where T : class;
    
    /// <summary>
    /// Executes a paged query using a specification
    /// </summary>
    Task<PagedResult<T>> QueryPagedAsync<T>(ISpecification<T> specification, int page, int pageSize) where T : class;
    
    /// <summary>
    /// Executes multiple specifications and returns combined results
    /// </summary>
    Task<SpecificationQueryResult<T>> ExecuteMultipleSpecificationsAsync<T>(params ISpecification<T>[] specifications) where T : class;
    
    /// <summary>
    /// Executes a security analysis using specifications
    /// </summary>
    Task<SecurityAnalysisResult> ExecuteSecurityAnalysisAsync<T>(ISpecification<T> specification, string analysisType) where T : class;
    
    /// <summary>
    /// Validates a specification without executing it
    /// </summary>
    Task<SpecificationValidationResult> ValidateSpecificationAsync<T>(ISpecification<T> specification) where T : class;
}

public class SpecificationService : ISpecificationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<SpecificationService> _logger;

    public SpecificationService(ApplicationDbContext dbContext, ILogger<SpecificationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(ISpecification<T> specification) where T : class
    {
        try
        {
            _logger.LogDebug("Executing specification query for type {EntityType}", typeof(T).Name);
            
            var query = GetQueryable<T>();
            return await query.Where(specification).ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing specification query for type {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public async Task<int> CountAsync<T>(ISpecification<T> specification) where T : class
    {
        try
        {
            var query = GetQueryable<T>();
            return await query.Where(specification).CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting entities with specification for type {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public async Task<bool> AnyAsync<T>(ISpecification<T> specification) where T : class
    {
        try
        {
            var query = GetQueryable<T>();
            return await query.Where(specification).AnyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence with specification for type {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public async Task<T?> FirstOrDefaultAsync<T>(ISpecification<T> specification) where T : class
    {
        try
        {
            var query = GetQueryable<T>();
            return await query.Where(specification).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting first entity with specification for type {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public async Task<PagedResult<T>> QueryPagedAsync<T>(ISpecification<T> specification, int page, int pageSize) where T : class
    {
        try
        {
            var query = GetQueryable<T>();
            var filteredQuery = query.Where(specification);
            
            var totalCount = await filteredQuery.CountAsync();
            var items = await filteredQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<T>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing paged query with specification for type {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public async Task<SpecificationQueryResult<T>> ExecuteMultipleSpecificationsAsync<T>(params ISpecification<T>[] specifications) where T : class
    {
        var results = new Dictionary<ISpecification<T>, List<T>>();
        var errors = new Dictionary<ISpecification<T>, string>();
        var executionTimes = new Dictionary<ISpecification<T>, TimeSpan>();

        foreach (var specification in specifications)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var entities = await QueryAsync(specification);
                results[specification] = entities.ToList();
            }
            catch (Exception ex)
            {
                errors[specification] = ex.Message;
                _logger.LogError(ex, "Error executing specification {SpecType}", specification.GetType().Name);
            }
            finally
            {
                stopwatch.Stop();
                executionTimes[specification] = stopwatch.Elapsed;
            }
        }

        return new SpecificationQueryResult<T>
        {
            Results = results,
            Errors = errors,
            ExecutionTimes = executionTimes,
            ExecutedAt = DateTime.UtcNow
        };
    }

    public async Task<SecurityAnalysisResult> ExecuteSecurityAnalysisAsync<T>(ISpecification<T> specification, string analysisType) where T : class
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var query = GetQueryable<T>();
            var totalCount = await query.CountAsync();
            var matchingEntities = await query.Where(specification).ToListAsync();
            
            stopwatch.Stop();

            var result = new SecurityAnalysisResult
            {
                EntityType = typeof(T).Name,
                TotalEntities = totalCount,
                MatchingEntities = matchingEntities.Count,
                RiskLevel = DetermineRiskLevel(matchingEntities.Count, totalCount),
                Entities = matchingEntities.Cast<object>().ToList(),
                AnalysisDate = DateTime.UtcNow,
                QueryDescription = analysisType
            };

            result.AdditionalMetrics["ExecutionTimeMs"] = stopwatch.ElapsedMilliseconds;
            result.AdditionalMetrics["MatchPercentage"] = totalCount > 0 ? (double)matchingEntities.Count / totalCount * 100 : 0;

            _logger.LogInformation("Security analysis completed: {AnalysisType} found {MatchingCount}/{TotalCount} entities with {RiskLevel} risk level",
                analysisType, matchingEntities.Count, totalCount, result.RiskLevel);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error executing security analysis {AnalysisType} for type {EntityType}", analysisType, typeof(T).Name);
            throw;
        }
    }

    public Task<SpecificationValidationResult> ValidateSpecificationAsync<T>(ISpecification<T> specification) where T : class
    {
        var result = new SpecificationValidationResult
        {
            IsValid = true,
            ValidatedAt = DateTime.UtcNow,
            EntityType = typeof(T).Name,
            SpecificationType = specification.GetType().Name
        };

        try
        {
            // Try to get the expression
            var expression = specification.ToExpression();
            result.ExpressionInfo = $"Expression: {expression}";
            
            // Try to compile the expression
            var compiledExpression = expression.Compile();
            
            // Test with a default instance if possible
            if (typeof(T).GetConstructor(Type.EmptyTypes) != null)
            {
                var testInstance = Activator.CreateInstance<T>();
                var testResult = compiledExpression(testInstance);
                result.TestExecutionResult = $"Test execution returned: {testResult}";
            }
            
            // Try to build SQL query
            try
            {
                var query = GetQueryable<T>().Where(specification.ToExpression());
                var sql = query.ToQueryString();
                result.GeneratedSql = sql;
            }
            catch (Exception sqlEx)
            {
                result.SqlGenerationError = sqlEx.Message;
                result.Warnings.Add("Unable to generate SQL query - specification may not be database-compatible");
            }

            _logger.LogDebug("Specification validation successful for {SpecType} on {EntityType}", 
                specification.GetType().Name, typeof(T).Name);
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.ValidationError = ex.Message;
            result.Errors.Add($"Specification validation failed: {ex.Message}");
            
            _logger.LogError(ex, "Specification validation failed for {SpecType} on {EntityType}", 
                specification.GetType().Name, typeof(T).Name);
        }

        return Task.FromResult(result);
    }

    private IQueryable<T> GetQueryable<T>() where T : class
    {
        return typeof(T).Name switch
        {
            nameof(Entity) => _dbContext.Entities.Cast<T>(),
            nameof(User) => _dbContext.Users.Cast<T>(),
            nameof(Group) => _dbContext.Groups.Cast<T>(),
            nameof(Role) => _dbContext.Roles.Cast<T>(),
            nameof(Permission) => _dbContext.EntityPermissions.Cast<T>(),
            nameof(Resource) => _dbContext.Resources.Cast<T>(),
            _ => _dbContext.Set<T>()
        };
    }

    private static string DetermineRiskLevel(int matchingCount, int totalCount)
    {
        if (totalCount == 0) return "Low";
        
        var percentage = (double)matchingCount / totalCount * 100;
        
        return percentage switch
        {
            0 => "Low",
            <= 5 => "Low",
            <= 15 => "Medium", 
            <= 30 => "High",
            _ => "Critical"
        };
    }
}

/// <summary>
/// Result of paged specification query
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// Result of multiple specification execution
/// </summary>
public class SpecificationQueryResult<T>
{
    public Dictionary<ISpecification<T>, List<T>> Results { get; set; } = new();
    public Dictionary<ISpecification<T>, string> Errors { get; set; } = new();
    public Dictionary<ISpecification<T>, TimeSpan> ExecutionTimes { get; set; } = new();
    public DateTime ExecutedAt { get; set; }
    
    public bool HasErrors => Errors.Any();
    public TimeSpan TotalExecutionTime => ExecutionTimes.Values.Aggregate(TimeSpan.Zero, (sum, time) => sum.Add(time));
    public int TotalResults => Results.Values.Sum(list => list.Count);
}

/// <summary>
/// Result of specification validation
/// </summary>
public class SpecificationValidationResult
{
    public bool IsValid { get; set; }
    public DateTime ValidatedAt { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string SpecificationType { get; set; } = string.Empty;
    public string? ExpressionInfo { get; set; }
    public string? GeneratedSql { get; set; }
    public string? SqlGenerationError { get; set; }
    public string? TestExecutionResult { get; set; }
    public string? ValidationError { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Extension methods for specification service
/// </summary>
public static class SpecificationServiceExtensions
{
    /// <summary>
    /// Executes common security queries
    /// </summary>
    public static async Task<Dictionary<string, SecurityAnalysisResult>> ExecuteSecurityAuditAsync(this ISpecificationService specificationService)
    {
        var results = new Dictionary<string, SecurityAnalysisResult>();

        // Users with excessive roles
        var excessiveRolesSpec = new UserWithMinimumRolesSpecification(5);
        results["ExcessiveRoles"] = await specificationService.ExecuteSecurityAnalysisAsync(excessiveRolesSpec, "Users with excessive roles");

        // Admin users
        var adminUsersSpec = new AdminUserSpecification();
        results["AdminUsers"] = await specificationService.ExecuteSecurityAnalysisAsync(adminUsersSpec, "Administrative users");

        // Users with high-risk access
        var highRiskSpec = new UserWithHighRiskAccessSpecification();
        results["HighRiskAccess"] = await specificationService.ExecuteSecurityAnalysisAsync(highRiskSpec, "Users with high-risk access");

        // Orphaned users
        var orphanedSpec = new OrphanedUserSpecification();
        results["OrphanedUsers"] = await specificationService.ExecuteSecurityAnalysisAsync(orphanedSpec, "Orphaned users");

        // High-risk permissions
        var highRiskPermissionsSpec = new HighRiskPermissionSpecification();
        results["HighRiskPermissions"] = await specificationService.ExecuteSecurityAnalysisAsync(highRiskPermissionsSpec, "High-risk permissions");

        return results;
    }

    /// <summary>
    /// Executes compliance queries
    /// </summary>
    public static async Task<Dictionary<string, SecurityAnalysisResult>> ExecuteComplianceAuditAsync(this ISpecificationService specificationService)
    {
        var results = new Dictionary<string, SecurityAnalysisResult>();

        // Least privilege violations
        var leastPrivilegeSpec = ComplexPermissionQueries.ComplianceQueries.UsersViolatingLeastPrivilege();
        results["LeastPrivilegeViolations"] = await specificationService.ExecuteSecurityAnalysisAsync(leastPrivilegeSpec, "Least privilege violations");

        // Segregation of duties violations
        var sodSpec = ComplexPermissionQueries.ComplianceQueries.UsersWithSegregationOfDutiesViolation();
        results["SegregationOfDutiesViolations"] = await specificationService.ExecuteSecurityAnalysisAsync(sodSpec, "Segregation of duties violations");

        // Permissions requiring approval
        var approvalPermissionsSpec = ComplexPermissionQueries.ComplianceQueries.PermissionsRequiringApproval();
        results["PermissionsRequiringApproval"] = await specificationService.ExecuteSecurityAnalysisAsync(approvalPermissionsSpec, "Permissions requiring approval");

        return results;
    }
}