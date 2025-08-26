namespace ACS.Service.Delegates.Queries;

/// <summary>
/// Interface for all query operations
/// Provides a consistent contract for executing queries that return typed results
/// </summary>
/// <typeparam name="T">The type of result returned by the query</typeparam>
public interface IQuery<T>
{
    /// <summary>
    /// Executes the query and returns the result
    /// </summary>
    /// <returns>The query result of type T</returns>
    T Execute();
}