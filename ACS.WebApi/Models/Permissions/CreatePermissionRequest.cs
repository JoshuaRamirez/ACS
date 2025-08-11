using System.ComponentModel.DataAnnotations;

namespace ACS.WebApi.Models.Permissions;

public class CreatePermissionRequest
{
    [Required]
    public PermissionResource Permission { get; init; } = new();

    public class PermissionResource
    {
        [Required]
        public string Uri { get; set; } = string.Empty;

        [Required]
        public string HttpVerb { get; set; } = string.Empty;

        public bool Grant { get; set; }
        public bool Deny { get; set; }
    }
}
