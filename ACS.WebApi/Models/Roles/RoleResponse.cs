using System.ComponentModel.DataAnnotations;
using ACS.WebResources.Roles;

namespace ACS.WebApi.Models.Roles;

public class RoleResponse
{
    [Required]
    public RoleResource Role { get; init; } = new();
}
