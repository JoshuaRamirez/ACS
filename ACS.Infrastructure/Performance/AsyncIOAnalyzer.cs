using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace ACS.Infrastructure.Performance;

/// <summary>
/// Analyzer for detecting synchronous I/O operations that should be async
/// </summary>
public class AsyncIOAnalyzer
{
    private readonly ILogger<AsyncIOAnalyzer> _logger;
    
    private static readonly HashSet<string> SynchronousFileIOPatterns = new()
    {
        "File.ReadAllText",
        "File.WriteAllText",
        "File.ReadAllLines",
        "File.WriteAllLines",
        "File.ReadAllBytes",
        "File.WriteAllBytes",
        "File.AppendAllText",
        "File.AppendAllLines",
        "File.Open",
        "File.OpenRead",
        "File.OpenWrite",
        "File.Create",
        "StreamReader",
        "StreamWriter",
        "FileStream"
    };
    
    private static readonly HashSet<string> SynchronousDbPatterns = new()
    {
        "SaveChanges",
        "Find",
        "First",
        "FirstOrDefault",
        "Single",
        "SingleOrDefault",
        "ToList",
        "ToArray",
        "Count",
        "Any",
        "All",
        "Min",
        "Max",
        "Sum",
        "Average"
    };
    
    private static readonly HashSet<string> SynchronousHttpPatterns = new()
    {
        "GetString",
        "GetByteArray",
        "GetStream",
        "PostAsJson",
        "PutAsJson",
        "Send"
    };

    public AsyncIOAnalyzer(ILogger<AsyncIOAnalyzer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Analyzes a file for synchronous I/O operations
    /// </summary>
    public async Task<AnalysisResult> AnalyzeFileAsync(string filePath)
    {
        var result = new AnalysisResult { FilePath = filePath };
        
        try
        {
            var code = await File.ReadAllTextAsync(filePath);
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = await tree.GetRootAsync();
            
            // Check for file I/O operations
            var fileIONodes = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv => IsSynchronousFileIO(inv));
            
            foreach (var node in fileIONodes)
            {
                result.Issues.Add(new IOIssue
                {
                    Type = IOIssueType.SynchronousFileIO,
                    Location = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    Code = node.ToString(),
                    Suggestion = GetAsyncAlternative(node)
                });
            }
            
            // Check for database operations
            var dbNodes = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv => IsSynchronousDbOperation(inv));
            
            foreach (var node in dbNodes)
            {
                if (!IsInAsyncContext(node))
                {
                    result.Issues.Add(new IOIssue
                    {
                        Type = IOIssueType.SynchronousDatabase,
                        Location = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        Code = node.ToString(),
                        Suggestion = GetAsyncAlternative(node)
                    });
                }
            }
            
            // Check for HTTP operations
            var httpNodes = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv => IsSynchronousHttpOperation(inv));
            
            foreach (var node in httpNodes)
            {
                result.Issues.Add(new IOIssue
                {
                    Type = IOIssueType.SynchronousHttp,
                    Location = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    Code = node.ToString(),
                    Suggestion = GetAsyncAlternative(node)
                });
            }
            
            // Check for blocking calls
            var blockingCalls = root.DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>()
                .Where(ma => ma.Name.ToString() == "Result" || ma.Name.ToString() == "Wait");
            
            foreach (var node in blockingCalls)
            {
                var parent = node.Parent;
                if (parent is InvocationExpressionSyntax || 
                    (parent is MemberAccessExpressionSyntax && IsTaskType(node)))
                {
                    result.Issues.Add(new IOIssue
                    {
                        Type = IOIssueType.BlockingCall,
                        Location = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        Code = node.ToString(),
                        Suggestion = "Use 'await' instead of blocking calls"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing file {FilePath}", filePath);
        }
        
        return result;
    }

    private bool IsSynchronousFileIO(InvocationExpressionSyntax invocation)
    {
        var expression = invocation.Expression.ToString();
        return SynchronousFileIOPatterns.Any(pattern => expression.Contains(pattern)) &&
               !expression.Contains("Async");
    }

    private bool IsSynchronousDbOperation(InvocationExpressionSyntax invocation)
    {
        var methodName = GetMethodName(invocation);
        return SynchronousDbPatterns.Contains(methodName) &&
               !invocation.Expression.ToString().Contains("Async");
    }

    private bool IsSynchronousHttpOperation(InvocationExpressionSyntax invocation)
    {
        var expression = invocation.Expression.ToString();
        return SynchronousHttpPatterns.Any(pattern => expression.Contains(pattern)) &&
               !expression.Contains("Async");
    }

    private bool IsInAsyncContext(SyntaxNode node)
    {
        var method = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method == null) return false;
        
        return method.Modifiers.Any(m => m.ToString() == "async") ||
               method.ReturnType.ToString().Contains("Task");
    }

    private bool IsTaskType(SyntaxNode node)
    {
        // Simple heuristic - would need semantic model for accurate check
        var text = node.Parent?.ToString() ?? "";
        return text.Contains("Task") || text.Contains("ValueTask");
    }

    private string GetMethodName(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.ToString();
        }
        
        if (invocation.Expression is IdentifierNameSyntax identifier)
        {
            return identifier.ToString();
        }
        
        return "";
    }

    private string GetAsyncAlternative(InvocationExpressionSyntax invocation)
    {
        var expression = invocation.Expression.ToString();
        var methodName = GetMethodName(invocation);
        
        // File I/O alternatives
        if (expression.Contains("File.ReadAllText"))
            return "await File.ReadAllTextAsync(...)";
        if (expression.Contains("File.WriteAllText"))
            return "await File.WriteAllTextAsync(...)";
        if (expression.Contains("File.ReadAllLines"))
            return "await File.ReadAllLinesAsync(...)";
        if (expression.Contains("File.WriteAllLines"))
            return "await File.WriteAllLinesAsync(...)";
        
        // Database alternatives
        if (methodName == "SaveChanges")
            return "await SaveChangesAsync()";
        if (methodName == "ToList")
            return "await ToListAsync()";
        if (methodName == "FirstOrDefault")
            return "await FirstOrDefaultAsync()";
        if (methodName == "Count")
            return "await CountAsync()";
        if (methodName == "Any")
            return "await AnyAsync()";
        
        // HTTP alternatives
        if (expression.Contains("GetString"))
            return "await GetStringAsync(...)";
        if (expression.Contains("PostAsJson"))
            return "await PostAsJsonAsync(...)";
        
        return $"Use async version of {methodName}";
    }
}

/// <summary>
/// Analysis result for a file
/// </summary>
public class AnalysisResult
{
    public string FilePath { get; set; } = string.Empty;
    public List<IOIssue> Issues { get; set; } = new();
    public bool HasIssues => Issues.Any();
}

/// <summary>
/// Represents an I/O issue found in code
/// </summary>
public class IOIssue
{
    public IOIssueType Type { get; set; }
    public int Location { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Suggestion { get; set; } = string.Empty;
}

/// <summary>
/// Type of I/O issue
/// </summary>
public enum IOIssueType
{
    SynchronousFileIO,
    SynchronousDatabase,
    SynchronousHttp,
    BlockingCall,
    MissingConfigureAwait
}