namespace ACS.Service.Domain;

/// <summary>
/// Represents a compliance check item
/// </summary>
public class ComplianceItem
{
    public string Category { get; set; } = string.Empty;
    public string Requirement { get; set; } = string.Empty;
    public bool IsMet { get; set; }
    public string Evidence { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; }
    public string? Notes { get; set; }
}