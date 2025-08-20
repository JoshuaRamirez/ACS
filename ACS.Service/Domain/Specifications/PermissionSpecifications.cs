using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace ACS.Service.Domain.Specifications;

/// <summary>
/// Specification for permissions that grant access
/// </summary>
public class GrantPermissionSpecification : Specification<Permission>
{
    public override Expression<Func<Permission, bool>> ToExpression()
    {
        return p => p.Grant && !p.Deny;
    }
}

/// <summary>
/// Specification for permissions that deny access
/// </summary>
public class DenyPermissionSpecification : Specification<Permission>
{
    public override Expression<Func<Permission, bool>> ToExpression()
    {
        return p => p.Deny;
    }
}

/// <summary>
/// Specification for permissions matching a specific URI
/// </summary>
public class UriPermissionSpecification : Specification<Permission>
{
    private readonly string _uri;
    private readonly bool _exactMatch;

    public UriPermissionSpecification(string uri, bool exactMatch = true)
    {
        _uri = uri ?? throw new ArgumentNullException(nameof(uri));
        _exactMatch = exactMatch;
    }

    public override Expression<Func<Permission, bool>> ToExpression()
    {
        if (_exactMatch)
        {
            return p => p.Uri == _uri;
        }
        else
        {
            return p => p.Uri.Contains(_uri);
        }
    }

    public override bool IsSatisfiedBy(Permission entity)
    {
        if (_exactMatch)
        {
            return entity.Uri.Equals(_uri, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            return entity.Uri.Contains(_uri, StringComparison.OrdinalIgnoreCase);
        }
    }
}

/// <summary>
/// Specification for permissions matching a URI pattern (supports wildcards and parameters)
/// </summary>
public class UriPatternPermissionSpecification : Specification<Permission>
{
    private readonly string _pattern;
    private readonly Regex? _compiledRegex;

    public UriPatternPermissionSpecification(string pattern)
    {
        _pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        
        // Pre-compile regex for performance if pattern contains wildcards or parameters
        if (pattern.Contains('*') || pattern.Contains('{'))
        {
            var regexPattern = ConvertToRegexPattern(pattern);
            _compiledRegex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
    }

    public override Expression<Func<Permission, bool>> ToExpression()
    {
        if (_compiledRegex != null)
        {
            // For EF Core queries, we need to use simpler pattern matching
            if (_pattern.Contains('*') && !_pattern.Contains('{'))
            {
                var likePattern = _pattern.Replace("*", "%");
                return p => EF.Functions.Like(p.Uri, likePattern);
            }
        }
        
        return p => p.Uri == _pattern;
    }

    public override bool IsSatisfiedBy(Permission entity)
    {
        if (_compiledRegex != null)
        {
            return _compiledRegex.IsMatch(entity.Uri);
        }
        
        return entity.Uri.Equals(_pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static string ConvertToRegexPattern(string pattern)
    {
        // Escape special regex characters except * and {}
        var escaped = Regex.Escape(pattern);
        
        // Convert wildcards to regex
        escaped = escaped.Replace("\\*", ".*");
        
        // Convert parameters to regex groups
        escaped = Regex.Replace(escaped, @"\\{[^}]+\\}", @"([^/]+)");
        
        return $"^{escaped}$";
    }
}

/// <summary>
/// Specification for permissions matching specific HTTP verbs
/// </summary>
public class HttpVerbPermissionSpecification : Specification<Permission>
{
    private readonly HttpVerb _httpVerb;

    public HttpVerbPermissionSpecification(HttpVerb httpVerb)
    {
        _httpVerb = httpVerb;
    }

    public override Expression<Func<Permission, bool>> ToExpression()
    {
        return p => p.HttpVerb == _httpVerb;
    }
}

/// <summary>
/// Specification for permissions matching multiple HTTP verbs
/// </summary>
public class HttpVerbsPermissionSpecification : Specification<Permission>
{
    private readonly HashSet<HttpVerb> _httpVerbs;

    public HttpVerbsPermissionSpecification(params HttpVerb[] httpVerbs)
    {
        _httpVerbs = new HashSet<HttpVerb>(httpVerbs ?? throw new ArgumentNullException(nameof(httpVerbs)));
    }

    public HttpVerbsPermissionSpecification(IEnumerable<HttpVerb> httpVerbs)
    {
        _httpVerbs = new HashSet<HttpVerb>(httpVerbs ?? throw new ArgumentNullException(nameof(httpVerbs)));
    }

    public override Expression<Func<Permission, bool>> ToExpression()
    {
        return p => _httpVerbs.Contains(p.HttpVerb);
    }

    public override bool IsSatisfiedBy(Permission entity)
    {
        return _httpVerbs.Contains(entity.HttpVerb);
    }
}

/// <summary>
/// Specification for permissions with specific scheme
/// </summary>
public class SchemePermissionSpecification : Specification<Permission>
{
    private readonly Scheme _scheme;

    public SchemePermissionSpecification(Scheme scheme)
    {
        _scheme = scheme;
    }

    public override Expression<Func<Permission, bool>> ToExpression()
    {
        return p => p.Scheme == _scheme;
    }
}

/// <summary>
/// Specification for resource-based permissions (combining URI and HTTP verb)
/// </summary>
public class ResourceAccessSpecification : Specification<Permission>
{
    private readonly string _uri;
    private readonly HttpVerb _httpVerb;
    private readonly bool _usePatternMatching;

    public ResourceAccessSpecification(string uri, HttpVerb httpVerb, bool usePatternMatching = false)
    {
        _uri = uri ?? throw new ArgumentNullException(nameof(uri));
        _httpVerb = httpVerb;
        _usePatternMatching = usePatternMatching;
    }

    public override Expression<Func<Permission, bool>> ToExpression()
    {
        if (_usePatternMatching)
        {
            if (_uri.Contains('*'))
            {
                var likePattern = _uri.Replace("*", "%");
                return p => p.HttpVerb == _httpVerb && EF.Functions.Like(p.Uri, likePattern);
            }
        }
        
        return p => p.Uri == _uri && p.HttpVerb == _httpVerb;
    }

    public override bool IsSatisfiedBy(Permission entity)
    {
        if (entity.HttpVerb != _httpVerb)
            return false;

        if (_usePatternMatching)
        {
            return MatchesUriPattern(entity.Uri, _uri);
        }

        return entity.Uri.Equals(_uri, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesUriPattern(string uri, string pattern)
    {
        if (pattern.Contains('*'))
        {
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(uri, regexPattern, RegexOptions.IgnoreCase);
        }

        if (pattern.Contains('{') && pattern.Contains('}'))
        {
            var regexPattern = "^" + Regex.Replace(Regex.Escape(pattern), @"\\{[^}]+\\}", "([^/]+)") + "$";
            return Regex.IsMatch(uri, regexPattern, RegexOptions.IgnoreCase);
        }

        return uri.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Specification for effective permissions (grant and not deny)
/// </summary>
public class EffectivePermissionSpecification : Specification<Permission>
{
    public override Expression<Func<Permission, bool>> ToExpression()
    {
        return p => p.Grant && !p.Deny;
    }
}

/// <summary>
/// Specification for high-risk permissions
/// </summary>
public class HighRiskPermissionSpecification : Specification<Permission>
{
    private static readonly string[] HighRiskPatterns = 
    {
        "/admin/*", "/system/*", "/config/*", "/security/*", 
        "*/delete", "*/admin", "*/config", "*/secrets"
    };

    public override Expression<Func<Permission, bool>> ToExpression()
    {
        // For database queries, we use simple string operations
        return p => p.Uri.Contains("/admin") || 
                   p.Uri.Contains("/system") || 
                   p.Uri.Contains("/config") || 
                   p.Uri.Contains("/security") ||
                   p.HttpVerb == HttpVerb.DELETE;
    }

    public override bool IsSatisfiedBy(Permission entity)
    {
        // Check high-risk URI patterns
        foreach (var pattern in HighRiskPatterns)
        {
            if (pattern.EndsWith("*"))
            {
                var prefix = pattern[..^1];
                if (entity.Uri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (pattern.StartsWith("*"))
            {
                var suffix = pattern[1..];
                if (entity.Uri.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (entity.Uri.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Check high-risk HTTP verbs
        return entity.HttpVerb == HttpVerb.DELETE || entity.HttpVerb == HttpVerb.PUT;
    }
}

/// <summary>
/// Specification for administrative permissions
/// </summary>
public class AdministrativePermissionSpecification : Specification<Permission>
{
    private static readonly string[] AdminPatterns = 
    {
        "/admin", "/administration", "/manage", "/config", "/system"
    };

    public override Expression<Func<Permission, bool>> ToExpression()
    {
        return p => p.Uri.Contains("/admin") || 
                   p.Uri.Contains("/administration") || 
                   p.Uri.Contains("/manage") ||
                   p.Uri.Contains("/config") ||
                   p.Uri.Contains("/system");
    }

    public override bool IsSatisfiedBy(Permission entity)
    {
        return AdminPatterns.Any(pattern => 
            entity.Uri.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Specification for read-only permissions
/// </summary>
public class ReadOnlyPermissionSpecification : Specification<Permission>
{
    private static readonly HttpVerb[] ReadOnlyVerbs = { HttpVerb.GET, HttpVerb.HEAD, HttpVerb.OPTIONS };

    public override Expression<Func<Permission, bool>> ToExpression()
    {
        return p => ReadOnlyVerbs.Contains(p.HttpVerb);
    }

    public override bool IsSatisfiedBy(Permission entity)
    {
        return ReadOnlyVerbs.Contains(entity.HttpVerb);
    }
}

/// <summary>
/// Specification for write permissions
/// </summary>
public class WritePermissionSpecification : Specification<Permission>
{
    private static readonly HttpVerb[] WriteVerbs = { HttpVerb.POST, HttpVerb.PUT, HttpVerb.PATCH, HttpVerb.DELETE };

    public override Expression<Func<Permission, bool>> ToExpression()
    {
        return p => WriteVerbs.Contains(p.HttpVerb);
    }

    public override bool IsSatisfiedBy(Permission entity)
    {
        return WriteVerbs.Contains(entity.HttpVerb);
    }
}

/// <summary>
/// Specification for wildcard permissions
/// </summary>
public class WildcardPermissionSpecification : Specification<Permission>
{
    public override Expression<Func<Permission, bool>> ToExpression()
    {
        return p => p.Uri.Contains("*");
    }
}

/// <summary>
/// Specification for parameterized permissions
/// </summary>
public class ParameterizedPermissionSpecification : Specification<Permission>
{
    public override Expression<Func<Permission, bool>> ToExpression()
    {
        return p => p.Uri.Contains("{") && p.Uri.Contains("}");
    }
}

/// <summary>
/// Specification for permissions granted within a time range
/// </summary>
public class TimeBasedPermissionSpecification : Specification<Permission>
{
    private readonly DateTime _startTime;
    private readonly DateTime _endTime;

    public TimeBasedPermissionSpecification(DateTime startTime, DateTime endTime)
    {
        _startTime = startTime;
        _endTime = endTime;
    }

    public override Expression<Func<Permission, bool>> ToExpression()
    {
        // This would require additional timestamp fields on Permission entity
        // For now, return a true expression as base permissions don't have timestamps
        return p => true;
    }

    public override bool IsSatisfiedBy(Permission entity)
    {
        // In a real implementation, this would check permission grant/creation timestamps
        // For now, return true as base permissions don't have timestamps
        return true;
    }
}

/// <summary>
/// Builder for creating complex permission specifications
/// </summary>
public class PermissionSpecificationBuilder
{
    private ISpecification<Permission>? _specification;

    public PermissionSpecificationBuilder()
    {
        _specification = new TrueSpecification<Permission>();
    }

    public PermissionSpecificationBuilder ForUri(string uri, bool exactMatch = true)
    {
        var uriSpec = new UriPermissionSpecification(uri, exactMatch);
        _specification = _specification?.And(uriSpec) ?? uriSpec;
        return this;
    }

    public PermissionSpecificationBuilder ForUriPattern(string pattern)
    {
        var patternSpec = new UriPatternPermissionSpecification(pattern);
        _specification = _specification?.And(patternSpec) ?? patternSpec;
        return this;
    }

    public PermissionSpecificationBuilder ForHttpVerb(HttpVerb httpVerb)
    {
        var verbSpec = new HttpVerbPermissionSpecification(httpVerb);
        _specification = _specification?.And(verbSpec) ?? verbSpec;
        return this;
    }

    public PermissionSpecificationBuilder ForHttpVerbs(params HttpVerb[] httpVerbs)
    {
        var verbsSpec = new HttpVerbsPermissionSpecification(httpVerbs);
        _specification = _specification?.And(verbsSpec) ?? verbsSpec;
        return this;
    }

    public PermissionSpecificationBuilder ForScheme(Scheme scheme)
    {
        var schemeSpec = new SchemePermissionSpecification(scheme);
        _specification = _specification?.And(schemeSpec) ?? schemeSpec;
        return this;
    }

    public PermissionSpecificationBuilder ThatGrant()
    {
        var grantSpec = new GrantPermissionSpecification();
        _specification = _specification?.And(grantSpec) ?? grantSpec;
        return this;
    }

    public PermissionSpecificationBuilder ThatDeny()
    {
        var denySpec = new DenyPermissionSpecification();
        _specification = _specification?.And(denySpec) ?? denySpec;
        return this;
    }

    public PermissionSpecificationBuilder ThatAreEffective()
    {
        var effectiveSpec = new EffectivePermissionSpecification();
        _specification = _specification?.And(effectiveSpec) ?? effectiveSpec;
        return this;
    }

    public PermissionSpecificationBuilder ThatAreHighRisk()
    {
        var highRiskSpec = new HighRiskPermissionSpecification();
        _specification = _specification?.And(highRiskSpec) ?? highRiskSpec;
        return this;
    }

    public PermissionSpecificationBuilder ThatAreAdministrative()
    {
        var adminSpec = new AdministrativePermissionSpecification();
        _specification = _specification?.And(adminSpec) ?? adminSpec;
        return this;
    }

    public PermissionSpecificationBuilder ThatAreReadOnly()
    {
        var readOnlySpec = new ReadOnlyPermissionSpecification();
        _specification = _specification?.And(readOnlySpec) ?? readOnlySpec;
        return this;
    }

    public PermissionSpecificationBuilder ThatAreWrite()
    {
        var writeSpec = new WritePermissionSpecification();
        _specification = _specification?.And(writeSpec) ?? writeSpec;
        return this;
    }

    public PermissionSpecificationBuilder ThatUseWildcards()
    {
        var wildcardSpec = new WildcardPermissionSpecification();
        _specification = _specification?.And(wildcardSpec) ?? wildcardSpec;
        return this;
    }

    public PermissionSpecificationBuilder ThatAreParameterized()
    {
        var paramSpec = new ParameterizedPermissionSpecification();
        _specification = _specification?.And(paramSpec) ?? paramSpec;
        return this;
    }

    public PermissionSpecificationBuilder Or()
    {
        // This creates a new builder context for OR operations
        return new OrPermissionSpecificationBuilder(_specification);
    }

    public PermissionSpecificationBuilder And(ISpecification<Permission> otherSpec)
    {
        _specification = _specification?.And(otherSpec) ?? otherSpec;
        return this;
    }

    public PermissionSpecificationBuilder OrSpec(ISpecification<Permission> otherSpec)
    {
        _specification = _specification?.Or(otherSpec) ?? otherSpec;
        return this;
    }

    public ISpecification<Permission> Build()
    {
        return _specification ?? new TrueSpecification<Permission>();
    }

    public static implicit operator Specification<Permission>(PermissionSpecificationBuilder builder)
    {
        return (Specification<Permission>)builder.Build();
    }
}

/// <summary>
/// Builder for OR operations in permission specifications
/// </summary>
public class OrPermissionSpecificationBuilder : PermissionSpecificationBuilder
{
    private readonly ISpecification<Permission>? _leftSpecification;

    internal OrPermissionSpecificationBuilder(ISpecification<Permission>? leftSpecification)
    {
        _leftSpecification = leftSpecification;
    }

    public new ISpecification<Permission> Build()
    {
        var rightSpecification = base.Build();
        return _leftSpecification?.Or(rightSpecification) ?? rightSpecification;
    }
}