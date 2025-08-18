namespace ACS.Service.Domain;

public class Permission
{
    public int Id { get; set; }
    public string Uri { get; set; } = string.Empty;
    public HttpVerb HttpVerb { get; set; }
    public bool Grant { get; set; }
    public bool Deny { get; set; }
    public Scheme Scheme { get; set; }
}