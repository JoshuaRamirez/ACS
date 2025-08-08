using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ACS.WebResources.Users;

namespace ACS.WebApi.Models.Users;

public class UsersResponse
{
    [Required]
    public IReadOnlyCollection<UserResource> Users { get; init; } = Array.Empty<UserResource>();
}
