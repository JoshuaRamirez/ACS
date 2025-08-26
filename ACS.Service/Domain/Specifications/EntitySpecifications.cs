using System.Linq.Expressions;

namespace ACS.Service.Domain.Specifications;

/// <summary>
/// Specification for entities by name
/// </summary>
public class EntityByNameSpecification : Specification<Entity>
{
    private readonly string _name;
    private readonly bool _exactMatch;

    public EntityByNameSpecification(string name, bool exactMatch = true)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _exactMatch = exactMatch;
    }

    public override Expression<Func<Entity, bool>> ToExpression()
    {
        if (_exactMatch)
        {
            return e => e.Name == _name;
        }
        else
        {
            return e => e.Name.Contains(_name);
        }
    }

    public override bool IsSatisfiedBy(Entity entity)
    {
        if (_exactMatch)
        {
            return entity.Name.Equals(_name, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            return entity.Name.Contains(_name, StringComparison.OrdinalIgnoreCase);
        }
    }
}

/// <summary>
/// Specification for entities by ID
/// </summary>
public class EntityByIdSpecification : Specification<Entity>
{
    private readonly int _id;

    public EntityByIdSpecification(int id)
    {
        _id = id;
    }

    public override Expression<Func<Entity, bool>> ToExpression()
    {
        return e => e.Id == _id;
    }
}

/// <summary>
/// Specification for entities by multiple IDs
/// </summary>
public class EntitiesByIdsSpecification : Specification<Entity>
{
    private readonly HashSet<int> _ids;

    public EntitiesByIdsSpecification(params int[] ids)
    {
        _ids = new HashSet<int>(ids ?? throw new ArgumentNullException(nameof(ids)));
    }

    public EntitiesByIdsSpecification(IEnumerable<int> ids)
    {
        _ids = new HashSet<int>(ids ?? throw new ArgumentNullException(nameof(ids)));
    }

    public override Expression<Func<Entity, bool>> ToExpression()
    {
        return e => _ids.Contains(e.Id);
    }

    public override bool IsSatisfiedBy(Entity entity)
    {
        return _ids.Contains(entity.Id);
    }
}

/// <summary>
/// Specification for entities with specific permissions
/// </summary>
public class EntityWithPermissionSpecification : Specification<Entity>
{
    private readonly ISpecification<Permission> _permissionSpecification;

    public EntityWithPermissionSpecification(ISpecification<Permission> permissionSpecification)
    {
        _permissionSpecification = permissionSpecification ?? throw new ArgumentNullException(nameof(permissionSpecification));
    }

    public override Expression<Func<Entity, bool>> ToExpression()
    {
        var permissionExpression = _permissionSpecification.ToExpression();
        var compiledPermissionPredicate = permissionExpression.Compile();
        return e => e.Permissions.Any(compiledPermissionPredicate);
    }

    public override bool IsSatisfiedBy(Entity entity)
    {
        return entity.Permissions.Any(_permissionSpecification.IsSatisfiedBy);
    }
}

/// <summary>
/// Specification for entities without any permissions
/// </summary>
public class EntityWithoutPermissionsSpecification : Specification<Entity>
{
    public override Expression<Func<Entity, bool>> ToExpression()
    {
        return e => !e.Permissions.Any();
    }
}

/// <summary>
/// Specification for entities with children
/// </summary>
public class EntityWithChildrenSpecification : Specification<Entity>
{
    public override Expression<Func<Entity, bool>> ToExpression()
    {
        return e => e.Children.Any();
    }
}

/// <summary>
/// Specification for entities without children (leaf entities)
/// </summary>
public class LeafEntitySpecification : Specification<Entity>
{
    public override Expression<Func<Entity, bool>> ToExpression()
    {
        return e => !e.Children.Any();
    }
}

/// <summary>
/// Specification for entities with parents
/// </summary>
public class EntityWithParentsSpecification : Specification<Entity>
{
    public override Expression<Func<Entity, bool>> ToExpression()
    {
        return e => e.Parents.Any();
    }
}

/// <summary>
/// Specification for root entities (without parents)
/// </summary>
public class RootEntitySpecification : Specification<Entity>
{
    public override Expression<Func<Entity, bool>> ToExpression()
    {
        return e => !e.Parents.Any();
    }
}

/// <summary>
/// Specification for entities that are children of a specific entity
/// </summary>
public class ChildOfEntitySpecification : Specification<Entity>
{
    private readonly int _parentId;

    public ChildOfEntitySpecification(int parentId)
    {
        _parentId = parentId;
    }

    public override Expression<Func<Entity, bool>> ToExpression()
    {
        return e => e.Parents.Any(p => p.Id == _parentId);
    }

    public override bool IsSatisfiedBy(Entity entity)
    {
        return entity.Parents.Any(p => p.Id == _parentId);
    }
}

/// <summary>
/// Specification for entities that are parents of a specific entity
/// </summary>
public class ParentOfEntitySpecification : Specification<Entity>
{
    private readonly int _childId;

    public ParentOfEntitySpecification(int childId)
    {
        _childId = childId;
    }

    public override Expression<Func<Entity, bool>> ToExpression()
    {
        return e => e.Children.Any(c => c.Id == _childId);
    }

    public override bool IsSatisfiedBy(Entity entity)
    {
        return entity.Children.Any(c => c.Id == _childId);
    }
}

/// <summary>
/// Specification for entities at a specific depth in the hierarchy
/// </summary>
public class EntityAtDepthSpecification : Specification<Entity>
{
    private readonly int _depth;

    public EntityAtDepthSpecification(int depth)
    {
        _depth = depth;
    }

    public override Expression<Func<Entity, bool>> ToExpression()
    {
        // This is complex to express in LINQ to Entities, so we'll use in-memory evaluation
        return e => true; // Will be filtered in IsSatisfiedBy
    }

    public override bool IsSatisfiedBy(Entity entity)
    {
        return CalculateDepth(entity) == _depth;
    }

    private static int CalculateDepth(Entity entity, HashSet<int>? visited = null)
    {
        visited ??= new HashSet<int>();
        
        if (visited.Contains(entity.Id))
            return -1; // Circular reference detected
        
        visited.Add(entity.Id);
        
        if (!entity.Parents.Any())
            return 0;
        
        var maxParentDepth = entity.Parents.Max(p => CalculateDepth(p, new HashSet<int>(visited)));
        return maxParentDepth == -1 ? -1 : maxParentDepth + 1;
    }
}

/// <summary>
/// Specification for entities with a minimum number of permissions
/// </summary>
public class EntityWithMinimumPermissionsSpecification : Specification<Entity>
{
    private readonly int _minimumCount;

    public EntityWithMinimumPermissionsSpecification(int minimumCount)
    {
        _minimumCount = minimumCount;
    }

    public override Expression<Func<Entity, bool>> ToExpression()
    {
        return e => e.Permissions.Count() >= _minimumCount;
    }

    public override bool IsSatisfiedBy(Entity entity)
    {
        return entity.Permissions.Count >= _minimumCount;
    }
}

/// <summary>
/// Specification for entities with a maximum number of permissions
/// </summary>
public class EntityWithMaximumPermissionsSpecification : Specification<Entity>
{
    private readonly int _maximumCount;

    public EntityWithMaximumPermissionsSpecification(int maximumCount)
    {
        _maximumCount = maximumCount;
    }

    public override Expression<Func<Entity, bool>> ToExpression()
    {
        return e => e.Permissions.Count() <= _maximumCount;
    }

    public override bool IsSatisfiedBy(Entity entity)
    {
        return entity.Permissions.Count <= _maximumCount;
    }
}

/// <summary>
/// Specification for entities with effective permissions for a specific resource
/// </summary>
public class EntityWithEffectiveAccessSpecification : Specification<Entity>
{
    private readonly string _uri;
    private readonly HttpVerb _httpVerb;

    public EntityWithEffectiveAccessSpecification(string uri, HttpVerb httpVerb)
    {
        _uri = uri ?? throw new ArgumentNullException(nameof(uri));
        _httpVerb = httpVerb;
    }

    public override Expression<Func<Entity, bool>> ToExpression()
    {
        return e => e.Permissions.Any(p => p.Uri == _uri && p.HttpVerb == _httpVerb && p.Grant && !p.Deny);
    }

    public override bool IsSatisfiedBy(Entity entity)
    {
        return entity.Permissions.Any(p => 
            p.Uri.Equals(_uri, StringComparison.OrdinalIgnoreCase) && 
            p.HttpVerb == _httpVerb && 
            p.Grant && 
            !p.Deny);
    }
}

/// <summary>
/// Base specification for type-specific entities
/// </summary>
public abstract class TypedEntitySpecification<T> : Specification<Entity> where T : Entity
{
    public override Expression<Func<Entity, bool>> ToExpression()
    {
        return e => e is T;
    }

    public override bool IsSatisfiedBy(Entity entity)
    {
        return entity is T;
    }
    
    protected virtual Expression<Func<T, bool>> GetTypedExpression()
    {
        return t => true;
    }
    
    protected virtual bool IsSatisfiedByTyped(T entity)
    {
        return true;
    }
}

/// <summary>
/// Concrete implementation of TypedEntitySpecification for basic type filtering
/// </summary>
public class ConcreteTypedEntitySpecification<T> : TypedEntitySpecification<T> where T : Entity
{
    // Inherits all behavior from the abstract base class
    // No additional implementation needed for basic type filtering
}

/// <summary>
/// Specification builder for entities
/// </summary>
public class EntitySpecificationBuilder
{
    private ISpecification<Entity>? _specification;

    public EntitySpecificationBuilder()
    {
        _specification = new TrueSpecification<Entity>();
    }

    public EntitySpecificationBuilder WithId(int id)
    {
        var idSpec = new EntityByIdSpecification(id);
        _specification = _specification?.And(idSpec) ?? idSpec;
        return this;
    }

    public EntitySpecificationBuilder WithIds(params int[] ids)
    {
        var idsSpec = new EntitiesByIdsSpecification(ids);
        _specification = _specification?.And(idsSpec) ?? idsSpec;
        return this;
    }

    public EntitySpecificationBuilder WithName(string name, bool exactMatch = true)
    {
        var nameSpec = new EntityByNameSpecification(name, exactMatch);
        _specification = _specification?.And(nameSpec) ?? nameSpec;
        return this;
    }

    public EntitySpecificationBuilder WithPermission(ISpecification<Permission> permissionSpec)
    {
        var entityPermSpec = new EntityWithPermissionSpecification(permissionSpec);
        _specification = _specification?.And(entityPermSpec) ?? entityPermSpec;
        return this;
    }

    public EntitySpecificationBuilder WithoutPermissions()
    {
        var noPermSpec = new EntityWithoutPermissionsSpecification();
        _specification = _specification?.And(noPermSpec) ?? noPermSpec;
        return this;
    }

    public EntitySpecificationBuilder ThatAreRoots()
    {
        var rootSpec = new RootEntitySpecification();
        _specification = _specification?.And(rootSpec) ?? rootSpec;
        return this;
    }

    public EntitySpecificationBuilder ThatAreLeaves()
    {
        var leafSpec = new LeafEntitySpecification();
        _specification = _specification?.And(leafSpec) ?? leafSpec;
        return this;
    }

    public EntitySpecificationBuilder ThatAreChildrenOf(int parentId)
    {
        var childSpec = new ChildOfEntitySpecification(parentId);
        _specification = _specification?.And(childSpec) ?? childSpec;
        return this;
    }

    public EntitySpecificationBuilder ThatAreParentsOf(int childId)
    {
        var parentSpec = new ParentOfEntitySpecification(childId);
        _specification = _specification?.And(parentSpec) ?? parentSpec;
        return this;
    }

    public EntitySpecificationBuilder WithMinimumPermissions(int count)
    {
        var minPermSpec = new EntityWithMinimumPermissionsSpecification(count);
        _specification = _specification?.And(minPermSpec) ?? minPermSpec;
        return this;
    }

    public EntitySpecificationBuilder WithMaximumPermissions(int count)
    {
        var maxPermSpec = new EntityWithMaximumPermissionsSpecification(count);
        _specification = _specification?.And(maxPermSpec) ?? maxPermSpec;
        return this;
    }

    public EntitySpecificationBuilder WithEffectiveAccess(string uri, HttpVerb httpVerb)
    {
        var accessSpec = new EntityWithEffectiveAccessSpecification(uri, httpVerb);
        _specification = _specification?.And(accessSpec) ?? accessSpec;
        return this;
    }

    public EntitySpecificationBuilder AtDepth(int depth)
    {
        var depthSpec = new EntityAtDepthSpecification(depth);
        _specification = _specification?.And(depthSpec) ?? depthSpec;
        return this;
    }

    public EntitySpecificationBuilder And(ISpecification<Entity> otherSpec)
    {
        _specification = _specification?.And(otherSpec) ?? otherSpec;
        return this;
    }

    public EntitySpecificationBuilder Or(ISpecification<Entity> otherSpec)
    {
        _specification = _specification?.Or(otherSpec) ?? otherSpec;
        return this;
    }

    public ISpecification<Entity> Build()
    {
        return _specification ?? new TrueSpecification<Entity>();
    }

    public static implicit operator Specification<Entity>(EntitySpecificationBuilder builder)
    {
        return (Specification<Entity>)builder.Build();
    }
}

/// <summary>
/// Extensions for entity specifications
/// </summary>
public static class EntitySpecificationExtensions
{
    /// <summary>
    /// Creates a specification builder for entities
    /// </summary>
    public static EntitySpecificationBuilder Specify(this IQueryable<Entity> query)
    {
        return new EntitySpecificationBuilder();
    }

    /// <summary>
    /// Filters entities that have access to a specific resource
    /// </summary>
    public static ISpecification<Entity> WithAccessTo(this EntitySpecificationBuilder builder, string uri, HttpVerb httpVerb)
    {
        return builder.WithEffectiveAccess(uri, httpVerb).Build();
    }

    /// <summary>
    /// Filters entities by type
    /// </summary>
    public static ISpecification<Entity> OfType<T>() where T : Entity
    {
        return new ConcreteTypedEntitySpecification<T>();
    }

    /// <summary>
    /// Combines entity specifications for complex queries
    /// </summary>
    public static ISpecification<Entity> HasAnyPermission(params ISpecification<Permission>[] permissionSpecs)
    {
        if (permissionSpecs == null || permissionSpecs.Length == 0)
            return new FalseSpecification<Entity>();

        var combined = permissionSpecs[0];
        for (int i = 1; i < permissionSpecs.Length; i++)
        {
            combined = combined.Or(permissionSpecs[i]);
        }

        return new EntityWithPermissionSpecification(combined);
    }

    /// <summary>
    /// Combines entity specifications for entities that have all specified permissions
    /// </summary>
    public static ISpecification<Entity> HasAllPermissions(params ISpecification<Permission>[] permissionSpecs)
    {
        if (permissionSpecs == null || permissionSpecs.Length == 0)
            return new TrueSpecification<Entity>();

        var entitySpec = new EntityWithPermissionSpecification(permissionSpecs[0]);
        for (int i = 1; i < permissionSpecs.Length; i++)
        {
            var nextSpec = new EntityWithPermissionSpecification(permissionSpecs[i]);
            entitySpec = (EntityWithPermissionSpecification)entitySpec.And(nextSpec);
        }

        return entitySpec;
    }
}