using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ACS.WebResources.Groups;

namespace ACS.WebApi.Models.Groups;

public class GroupsResponse
{
    [Required]
    public IReadOnlyCollection<GroupResource> Groups { get; init; } = Array.Empty<GroupResource>();
}
