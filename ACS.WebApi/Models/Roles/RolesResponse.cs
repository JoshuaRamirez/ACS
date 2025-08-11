using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ACS.WebResources.Roles;

namespace ACS.WebApi.Models.Roles;

public class RolesResponse
{
    [Required]
    public IReadOnlyCollection<RoleResource> Roles { get; init; } = Array.Empty<RoleResource>();
}
