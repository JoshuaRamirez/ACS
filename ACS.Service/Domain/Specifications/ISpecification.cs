using System.Linq.Expressions;

namespace ACS.Service.Domain.Specifications;

/// <summary>
/// Base interface for specifications
/// </summary>
/// <typeparam name="T">The type being specified</typeparam>
public interface ISpecification<T>
{
    /// <summary>
    /// Gets the expression tree that represents the specification
    /// </summary>
    Expression<Func<T, bool>> ToExpression();
    
    /// <summary>
    /// Determines if the given entity satisfies the specification
    /// </summary>
    /// <param name="entity">The entity to test</param>
    /// <returns>True if the entity satisfies the specification</returns>
    bool IsSatisfiedBy(T entity);
    
    /// <summary>
    /// Combines this specification with another using AND logic
    /// </summary>
    /// <param name="specification">The specification to combine with</param>
    /// <returns>A new combined specification</returns>
    ISpecification<T> And(ISpecification<T> specification);
    
    /// <summary>
    /// Combines this specification with another using OR logic
    /// </summary>
    /// <param name="specification">The specification to combine with</param>
    /// <returns>A new combined specification</returns>
    ISpecification<T> Or(ISpecification<T> specification);
    
    /// <summary>
    /// Negates this specification
    /// </summary>
    /// <returns>A new negated specification</returns>
    ISpecification<T> Not();
}

/// <summary>
/// Base abstract class for specifications
/// </summary>
/// <typeparam name="T">The type being specified</typeparam>
public abstract class Specification<T> : ISpecification<T>
{
    private Func<T, bool>? _compiledExpression;
    
    /// <summary>
    /// Gets the expression tree that represents the specification
    /// </summary>
    public abstract Expression<Func<T, bool>> ToExpression();
    
    /// <summary>
    /// Determines if the given entity satisfies the specification
    /// </summary>
    public virtual bool IsSatisfiedBy(T entity)
    {
        _compiledExpression ??= ToExpression().Compile();
        return _compiledExpression(entity);
    }
    
    /// <summary>
    /// Combines this specification with another using AND logic
    /// </summary>
    public ISpecification<T> And(ISpecification<T> specification)
    {
        return new AndSpecification<T>(this, specification);
    }
    
    /// <summary>
    /// Combines this specification with another using OR logic
    /// </summary>
    public ISpecification<T> Or(ISpecification<T> specification)
    {
        return new OrSpecification<T>(this, specification);
    }
    
    /// <summary>
    /// Negates this specification
    /// </summary>
    public ISpecification<T> Not()
    {
        return new NotSpecification<T>(this);
    }
    
    /// <summary>
    /// Implicit conversion to expression
    /// </summary>
    public static implicit operator Expression<Func<T, bool>>(Specification<T> specification)
    {
        return specification.ToExpression();
    }
    
    /// <summary>
    /// Implicit conversion to func
    /// </summary>
    public static implicit operator Func<T, bool>(Specification<T> specification)
    {
        return specification.IsSatisfiedBy;
    }
}

/// <summary>
/// Always true specification
/// </summary>
/// <typeparam name="T">The type being specified</typeparam>
public class TrueSpecification<T> : Specification<T>
{
    public override Expression<Func<T, bool>> ToExpression()
    {
        return x => true;
    }
}

/// <summary>
/// Always false specification
/// </summary>
/// <typeparam name="T">The type being specified</typeparam>
public class FalseSpecification<T> : Specification<T>
{
    public override Expression<Func<T, bool>> ToExpression()
    {
        return x => false;
    }
}

/// <summary>
/// AND combination specification
/// </summary>
/// <typeparam name="T">The type being specified</typeparam>
internal class AndSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _left;
    private readonly ISpecification<T> _right;

    public AndSpecification(ISpecification<T> left, ISpecification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpression = _left.ToExpression();
        var rightExpression = _right.ToExpression();

        var parameter = Expression.Parameter(typeof(T));
        var leftVisitor = new ReplaceExpressionVisitor(leftExpression.Parameters[0], parameter);
        var left = leftVisitor.Visit(leftExpression.Body);

        var rightVisitor = new ReplaceExpressionVisitor(rightExpression.Parameters[0], parameter);
        var right = rightVisitor.Visit(rightExpression.Body);

        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(left!, right!), parameter);
    }
}

/// <summary>
/// OR combination specification
/// </summary>
/// <typeparam name="T">The type being specified</typeparam>
internal class OrSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _left;
    private readonly ISpecification<T> _right;

    public OrSpecification(ISpecification<T> left, ISpecification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpression = _left.ToExpression();
        var rightExpression = _right.ToExpression();

        var parameter = Expression.Parameter(typeof(T));
        var leftVisitor = new ReplaceExpressionVisitor(leftExpression.Parameters[0], parameter);
        var left = leftVisitor.Visit(leftExpression.Body);

        var rightVisitor = new ReplaceExpressionVisitor(rightExpression.Parameters[0], parameter);
        var right = rightVisitor.Visit(rightExpression.Body);

        return Expression.Lambda<Func<T, bool>>(Expression.OrElse(left!, right!), parameter);
    }
}

/// <summary>
/// NOT specification
/// </summary>
/// <typeparam name="T">The type being specified</typeparam>
internal class NotSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _specification;

    public NotSpecification(ISpecification<T> specification)
    {
        _specification = specification;
    }

    public override Expression<Func<T, bool>> ToExpression()
    {
        var expression = _specification.ToExpression();
        var parameter = expression.Parameters[0];
        var body = Expression.Not(expression.Body);

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}

/// <summary>
/// Expression visitor for replacing parameters in expression trees
/// </summary>
internal class ReplaceExpressionVisitor : ExpressionVisitor
{
    private readonly Expression _oldValue;
    private readonly Expression _newValue;

    public ReplaceExpressionVisitor(Expression oldValue, Expression newValue)
    {
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public override Expression? Visit(Expression? node)
    {
        return node == _oldValue ? _newValue : base.Visit(node);
    }
}

/// <summary>
/// Extension methods for specifications
/// </summary>
public static class SpecificationExtensions
{
    /// <summary>
    /// Combines specifications using AND logic
    /// </summary>
    public static ISpecification<T> And<T>(this ISpecification<T> left, ISpecification<T> right)
    {
        return left.And(right);
    }
    
    /// <summary>
    /// Combines specifications using OR logic
    /// </summary>
    public static ISpecification<T> Or<T>(this ISpecification<T> left, ISpecification<T> right)
    {
        return left.Or(right);
    }
    
    /// <summary>
    /// Negates a specification
    /// </summary>
    public static ISpecification<T> Not<T>(this ISpecification<T> specification)
    {
        return specification.Not();
    }
    
    /// <summary>
    /// Applies a specification to a queryable
    /// </summary>
    public static IQueryable<T> Where<T>(this IQueryable<T> query, ISpecification<T> specification)
    {
        return query.Where(specification.ToExpression());
    }
    
    /// <summary>
    /// Filters a collection using a specification
    /// </summary>
    public static IEnumerable<T> Where<T>(this IEnumerable<T> collection, ISpecification<T> specification)
    {
        return collection.Where(specification.IsSatisfiedBy);
    }
    
    /// <summary>
    /// Counts items that satisfy a specification
    /// </summary>
    public static int Count<T>(this IEnumerable<T> collection, ISpecification<T> specification)
    {
        return collection.Count(specification.IsSatisfiedBy);
    }
    
    /// <summary>
    /// Checks if any item satisfies a specification
    /// </summary>
    public static bool Any<T>(this IEnumerable<T> collection, ISpecification<T> specification)
    {
        return collection.Any(specification.IsSatisfiedBy);
    }
    
    /// <summary>
    /// Checks if all items satisfy a specification
    /// </summary>
    public static bool All<T>(this IEnumerable<T> collection, ISpecification<T> specification)
    {
        return collection.All(specification.IsSatisfiedBy);
    }
    
    /// <summary>
    /// Gets the first item that satisfies a specification
    /// </summary>
    public static T? FirstOrDefault<T>(this IEnumerable<T> collection, ISpecification<T> specification)
    {
        return collection.FirstOrDefault(specification.IsSatisfiedBy);
    }
}