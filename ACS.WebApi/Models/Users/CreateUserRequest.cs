using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ACS.WebResources.Groups;
using ACS.WebResources.Roles;

namespace ACS.WebApi.Models.Users;

public class CreateUserRequest
{
    [Required]
    public UserResource User { get; init; } = new();

    public class UserResource
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        public IReadOnlyCollection<GroupResource> GroupMemberships { get; init; } = Array.Empty<GroupResource>();
        public IReadOnlyCollection<RoleResource> RoleMemberships { get; init; } = Array.Empty<RoleResource>();
    }
}
