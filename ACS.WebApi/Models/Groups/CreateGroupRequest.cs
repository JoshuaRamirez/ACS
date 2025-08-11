using System.ComponentModel.DataAnnotations;

namespace ACS.WebApi.Models.Groups;

public class CreateGroupRequest
{
    [Required]
    public GroupResource Group { get; init; } = new();

    public class GroupResource
    {
        [Required]
        public string Name { get; set; } = string.Empty;
    }
}
