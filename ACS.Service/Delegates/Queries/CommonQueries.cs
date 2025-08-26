using ACS.Service.Domain;
using ACS.Service.Infrastructure;

namespace ACS.Service.Delegates.Queries;

/// <summary>
/// Query to retrieve any entity by ID and type
/// </summary>
public class GetEntityByIdQuery : Query<Entity?>
{
    public int EntityId { get; set; }
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (EntityId <= 0)
            throw new ArgumentException("Entity ID must be greater than zero", nameof(EntityId));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override Entity? ExecuteQuery()
    {
        // Try to find the entity in any of the collections
        if (EntityGraph.Users.TryGetValue(EntityId, out var user))
            return user;
        
        if (EntityGraph.Groups.TryGetValue(EntityId, out var group))
            return group;
        
        if (EntityGraph.Roles.TryGetValue(EntityId, out var role))
            return role;
        
        return null;
    }
}