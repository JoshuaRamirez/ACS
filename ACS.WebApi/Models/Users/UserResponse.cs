using System.ComponentModel.DataAnnotations;
using ACS.WebResources.Users;

namespace ACS.WebApi.Models.Users;

public class UserResponse
{
    [Required]
    public UserResource User { get; init; } = new();
}
