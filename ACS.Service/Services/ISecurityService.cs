namespace ACS.Service.Services;

/// <summary>
/// Service interface for Security operations - minimal interface matching handler requirements
/// </summary>
public interface ISecurityService
{
    // Methods that handlers are calling
    Task<DateTime> BlockUserAsync(int userId, string severity, string violationId);
    Task QuarantineUserAsync(int userId, string reason, string quarantinedBy);
    Task GenerateSecurityAlertAsync(string alertType, string message, Dictionary<string, object> metadata, string createdBy);
}
