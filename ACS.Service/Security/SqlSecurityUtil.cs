using System.Text.RegularExpressions;

namespace ACS.Service.Security;

public static class SqlSecurityUtil
{
    private static readonly Regex ValidIdentifierPattern = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex ValidObjectNamePattern = new(@"^[a-zA-Z_][a-zA-Z0-9_]*(\.[a-zA-Z_][a-zA-Z0-9_]*)*$", RegexOptions.Compiled);
    private static readonly Regex ValidDatabaseNamePattern = new(@"^[a-zA-Z][a-zA-Z0-9_]*$", RegexOptions.Compiled);

    public static string ValidateAndEscapeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("Identifier cannot be null or empty", nameof(identifier));

        var cleanIdentifier = identifier.Trim('[', ']');
        
        if (!ValidIdentifierPattern.IsMatch(cleanIdentifier))
        {
            throw new ArgumentException($"Invalid SQL identifier: {identifier}. Only alphanumeric characters and underscores allowed, cannot start with number.", nameof(identifier));
        }

        return $"[{cleanIdentifier}]";
    }

    public static string ValidateAndEscapeObjectName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            throw new ArgumentException("Object name cannot be null or empty", nameof(objectName));

        var cleanObjectName = objectName.Trim('[', ']');
        
        if (!ValidObjectNamePattern.IsMatch(cleanObjectName))
        {
            throw new ArgumentException($"Invalid SQL object name: {objectName}. Only alphanumeric characters, underscores, and dots allowed.", nameof(objectName));
        }

        var parts = cleanObjectName.Split('.');
        var escapedParts = parts.Select(part => $"[{part}]");
        
        return string.Join(".", escapedParts);
    }

    public static string ValidateAndEscapeDatabaseName(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be null or empty", nameof(databaseName));

        var cleanName = databaseName.Trim('[', ']');
        
        if (!ValidDatabaseNamePattern.IsMatch(cleanName))
        {
            throw new ArgumentException($"Invalid database name: {databaseName}. Must start with letter, contain only alphanumeric and underscore characters.", nameof(databaseName));
        }

        if (cleanName.Length > 128)
            throw new ArgumentException("Database name cannot exceed 128 characters", nameof(databaseName));

        if (IsReservedDatabaseName(cleanName))
            throw new ArgumentException($"Database name {cleanName} is reserved and cannot be used", nameof(databaseName));

        return $"[{cleanName}]";
    }

    private static bool IsReservedDatabaseName(string name)
    {
        var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "master", "model", "msdb", "tempdb", "distribution", "resource",
            "ReportServer", "ReportServerTempDB"
        };

        return reservedNames.Contains(name);
    }

    public static bool IsSafeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return false;

        var cleanIdentifier = identifier.Trim('[', ']');
        return ValidIdentifierPattern.IsMatch(cleanIdentifier);
    }

    public static string CreateSecureAlterIndexCommand(string indexName, string tableName, string operation, Dictionary<string, object>? options = null)
    {
        var safeIndexName = ValidateAndEscapeIdentifier(indexName);
        var safeTableName = ValidateAndEscapeObjectName(tableName);

        var validOperations = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "REBUILD", "REORGANIZE" };
        if (!validOperations.Contains(operation))
            throw new ArgumentException($"Invalid operation: {operation}. Only REBUILD and REORGANIZE are allowed.", nameof(operation));

        var sql = $"ALTER INDEX {safeIndexName} ON {safeTableName} {operation.ToUpper()}";

        if (operation.Equals("REBUILD", StringComparison.OrdinalIgnoreCase) && options != null)
        {
            var optionParts = new List<string>();
            
            if (options.ContainsKey("ONLINE") && options["ONLINE"] is bool online)
                optionParts.Add($"ONLINE = {(online ? "ON" : "OFF")}");
                
            if (options.ContainsKey("FILLFACTOR") && options["FILLFACTOR"] is int fillFactor)
            {
                if (fillFactor < 1 || fillFactor > 100)
                    throw new ArgumentException("FILLFACTOR must be between 1 and 100", nameof(options));
                optionParts.Add($"FILLFACTOR = {fillFactor}");
            }

            if (optionParts.Any())
                sql += $" WITH ({string.Join(", ", optionParts)})";
        }

        return sql;
    }
}
