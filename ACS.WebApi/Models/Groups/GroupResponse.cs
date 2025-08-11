using System.ComponentModel.DataAnnotations;
using ACS.WebResources.Groups;

namespace ACS.WebApi.Models.Groups;

public class GroupResponse
{
    [Required]
    public GroupResource Group { get; init; } = new();
}
