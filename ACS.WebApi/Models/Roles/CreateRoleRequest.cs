using System.ComponentModel.DataAnnotations;

namespace ACS.WebApi.Models.Roles;

public class CreateRoleRequest
{
    [Required]
    public RoleResource Role { get; init; } = new();

    public class RoleResource
    {
        [Required]
        public string Name { get; set; } = string.Empty;
    }
}
