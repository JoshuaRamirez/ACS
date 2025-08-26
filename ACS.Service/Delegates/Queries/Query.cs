namespace ACS.Service.Delegates.Queries;

/// <summary>
/// Abstract base class for all query operations
/// Implements the IQuery interface and provides polymorphic inheritance
/// with validation and execution patterns
/// </summary>
/// <typeparam name="T">The type of result returned by the query</typeparam>
public abstract class Query<T> : IQuery<T>
{
    /// <summary>
    /// Public entry point that validates parameters and executes the query
    /// </summary>
    /// <returns>The query result of type T</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails</exception>
    public T Execute()
    {
        // Validate query parameters before execution
        Validate();
        
        // Execute the concrete query implementation
        return ExecuteQuery();
    }

    /// <summary>
    /// Abstract method that concrete query classes must implement
    /// to validate all query parameters and ensure proper initialization
    /// </summary>
    /// <exception cref="ArgumentException">Should be thrown when validation fails</exception>
    protected abstract void Validate();

    /// <summary>
    /// Abstract method that concrete query classes must implement
    /// to perform the actual query execution
    /// </summary>
    /// <returns>The query result of type T</returns>
    protected abstract T ExecuteQuery();
}