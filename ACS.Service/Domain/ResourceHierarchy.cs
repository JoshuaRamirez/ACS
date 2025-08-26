namespace ACS.Service.Domain;

/// <summary>
/// Represents a hierarchical structure of resources with parent-child relationships
/// </summary>
public class ResourceHierarchy
{
    /// <summary>
    /// The root resource entity
    /// </summary>
    public Entity? Root { get; set; }

    /// <summary>
    /// All resources in the hierarchy flattened as a collection
    /// </summary>
    public ICollection<Entity> Entities { get; set; } = new List<Entity>();

    /// <summary>
    /// Maximum depth of the hierarchy
    /// </summary>
    public int MaxDepth { get; set; }

    /// <summary>
    /// Total number of resources in the hierarchy
    /// </summary>
    public int TotalCount => Entities.Count;

    /// <summary>
    /// Builds a hierarchy tree structure from the flat entity collection
    /// </summary>
    public ResourceHierarchyNode BuildTree()
    {
        if (Root == null)
            throw new InvalidOperationException("Cannot build tree without a root entity");

        var hierarchyNodes = new Dictionary<int, ResourceHierarchyNode>();
        
        // Create nodes for all entities
        foreach (var entity in Entities)
        {
            hierarchyNodes[entity.Id] = new ResourceHierarchyNode
            {
                Entity = entity,
                Children = new List<ResourceHierarchyNode>()
            };
        }

        // Build parent-child relationships using existing Parents/Children collections
        ResourceHierarchyNode? rootNode = null;
        foreach (var entity in Entities)
        {
            var node = hierarchyNodes[entity.Id];
            
            // Add children based on existing Children collection
            foreach (var child in entity.Children)
            {
                if (hierarchyNodes.ContainsKey(child.Id))
                {
                    var childNode = hierarchyNodes[child.Id];
                    node.Children.Add(childNode);
                    childNode.Parent = node;
                }
            }
            
            // Identify root nodes (nodes with no parents)
            if (!entity.Parents.Any() && rootNode == null)
            {
                rootNode = node;
            }
        }

        return rootNode ?? hierarchyNodes.Values.First();
    }

    /// <summary>
    /// Gets all leaf nodes (nodes without children) in the hierarchy
    /// </summary>
    public ICollection<Entity> GetLeafNodes()
    {
        return Entities.Where(e => !e.Children.Any()).ToList();
    }

    /// <summary>
    /// Gets the path from root to a specific entity
    /// </summary>
    public ICollection<Entity> GetPathToEntity(int entityId)
    {
        var path = new List<Entity>();
        var entity = Entities.FirstOrDefault(e => e.Id == entityId);
        
        if (entity == null)
            return path;

        // Build path by traversing up through parents
        var visited = new HashSet<int>();
        var current = entity;
        
        while (current != null && !visited.Contains(current.Id))
        {
            path.Insert(0, current);
            visited.Add(current.Id);
            
            // Get the first parent (assuming single-parent hierarchy for path)
            current = current.Parents.FirstOrDefault();
        }

        return path;
    }
}

/// <summary>
/// Represents a node in the resource hierarchy tree
/// </summary>
public class ResourceHierarchyNode
{
    /// <summary>
    /// The entity at this node
    /// </summary>
    public Entity Entity { get; set; } = null!;

    /// <summary>
    /// Parent node reference
    /// </summary>
    public ResourceHierarchyNode? Parent { get; set; }

    /// <summary>
    /// Child nodes
    /// </summary>
    public ICollection<ResourceHierarchyNode> Children { get; set; } = new List<ResourceHierarchyNode>();

    /// <summary>
    /// Depth level in the hierarchy (0 for root)
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Whether this node is a leaf (has no children)
    /// </summary>
    public bool IsLeaf => Children.Count == 0;

    /// <summary>
    /// Whether this node is the root (has no parent)
    /// </summary>
    public bool IsRoot => Parent == null;

    /// <summary>
    /// Gets all descendant nodes recursively
    /// </summary>
    public ICollection<ResourceHierarchyNode> GetDescendants()
    {
        var descendants = new List<ResourceHierarchyNode>();
        
        foreach (var child in Children)
        {
            descendants.Add(child);
            descendants.AddRange(child.GetDescendants());
        }

        return descendants;
    }

    /// <summary>
    /// Gets all ancestor nodes up to the root
    /// </summary>
    public ICollection<ResourceHierarchyNode> GetAncestors()
    {
        var ancestors = new List<ResourceHierarchyNode>();
        var current = Parent;

        while (current != null)
        {
            ancestors.Add(current);
            current = current.Parent;
        }

        return ancestors;
    }
}