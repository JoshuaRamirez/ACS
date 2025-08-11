namespace ACS.WebResources.Permissions;

public class PermissionResource
{
    public int Id { get; set; }
    public string Uri { get; set; } = string.Empty;
    public string HttpVerb { get; set; } = string.Empty;
    public bool Grant { get; set; }
    public bool Deny { get; set; }
}
