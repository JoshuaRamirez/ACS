using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace ACS.Infrastructure.Performance;

/// <summary>
/// Enforces proper async/await patterns and best practices
/// </summary>
public static class AsyncPatternEnforcer
{
    /// <summary>
    /// Ensures a task is properly awaited with timeout
    /// </summary>
    public static async Task<T> WithTimeoutAsync<T>(
        this Task<T> task,
        TimeSpan timeout,
        [CallerMemberName] string? caller = null)
    {
        using var cts = new CancellationTokenSource(timeout);
        
        try
        {
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, cts.Token));
            
            if (completedTask == task)
            {
                cts.Cancel();
                return await task;
            }
            
            throw new TimeoutException($"Operation '{caller}' timed out after {timeout.TotalSeconds} seconds");
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Operation '{caller}' was cancelled");
        }
    }

    /// <summary>
    /// Ensures a task is properly awaited with timeout
    /// </summary>
    public static async Task WithTimeoutAsync(
        this Task task,
        TimeSpan timeout,
        [CallerMemberName] string? caller = null)
    {
        using var cts = new CancellationTokenSource(timeout);
        
        try
        {
            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, cts.Token));
            
            if (completedTask == task)
            {
                cts.Cancel();
                await task;
                return;
            }
            
            throw new TimeoutException($"Operation '{caller}' timed out after {timeout.TotalSeconds} seconds");
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Operation '{caller}' was cancelled");
        }
    }

    /// <summary>
    /// Safely executes async operation with retry logic
    /// </summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        TimeSpan? retryDelay = null,
        ILogger? logger = null,
        [CallerMemberName] string? caller = null)
    {
        var delay = retryDelay ?? TimeSpan.FromSeconds(1);
        var lastException = default(Exception);

        for (int i = 0; i <= maxRetries; i++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (Exception ex) when (i < maxRetries)
            {
                lastException = ex;
                logger?.LogWarning(ex, "Retry {Attempt}/{Max} for {Operation} failed",
                    i + 1, maxRetries, caller);
                
                await Task.Delay(delay).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
            }
        }

        throw new InvalidOperationException(
            $"Operation '{caller}' failed after {maxRetries} retries",
            lastException);
    }

    /// <summary>
    /// Executes multiple async operations in parallel with proper error handling
    /// </summary>
    public static async Task<IEnumerable<T>> ExecuteInParallelAsync<T>(
        IEnumerable<Func<Task<T>>> operations,
        int maxDegreeOfParallelism = 10,
        CancellationToken cancellationToken = default)
    {
        var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
        var tasks = new List<Task<T>>();

        foreach (var operation in operations)
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            
            var task = Task.Run(async () =>
            {
                try
                {
                    return await operation().ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);
            
            tasks.Add(task);
        }

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Ensures async enumerable is properly consumed
    /// </summary>
    public static async Task<List<T>> ToListAsync<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken cancellationToken = default)
    {
        var list = new List<T>();
        
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            list.Add(item);
        }
        
        return list;
    }

    /// <summary>
    /// Safely disposes async resources
    /// </summary>
    public static async ValueTask SafeDisposeAsync(this IAsyncDisposable? resource)
    {
        if (resource != null)
        {
            try
            {
                await resource.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Log but don't throw - disposal should not fail operations
                Console.WriteLine($"Error disposing resource: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Converts synchronous operation to async
    /// </summary>
    public static Task<T> ToAsync<T>(Func<T> syncOperation)
    {
        return Task.Run(syncOperation);
    }

    /// <summary>
    /// Ensures proper ConfigureAwait usage
    /// </summary>
    public static ConfiguredTaskAwaitable<T> ConfigureAwaitCorrectly<T>(
        this Task<T> task,
        bool continueOnCapturedContext = false)
    {
        return task.ConfigureAwait(continueOnCapturedContext);
    }

    /// <summary>
    /// Ensures proper ConfigureAwait usage
    /// </summary>
    public static ConfiguredTaskAwaitable ConfigureAwaitCorrectly(
        this Task task,
        bool continueOnCapturedContext = false)
    {
        return task.ConfigureAwait(continueOnCapturedContext);
    }
}

/// <summary>
/// Async lock for thread-safe async operations
/// </summary>
public class AsyncLock
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<IDisposable> LockAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new AsyncLockReleaser(_semaphore);
    }

    private class AsyncLockReleaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public AsyncLockReleaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            _semaphore.Release();
        }
    }
}

/// <summary>
/// Async lazy initialization
/// </summary>
public class AsyncLazy<T>
{
    private readonly Lazy<Task<T>> _lazy;

    public AsyncLazy(Func<Task<T>> taskFactory)
    {
        _lazy = new Lazy<Task<T>>(() => Task.Run(taskFactory));
    }

    public TaskAwaiter<T> GetAwaiter()
    {
        return _lazy.Value.GetAwaiter();
    }

    public Task<T> Value => _lazy.Value;
}

/// <summary>
/// Async event handler
/// </summary>
public class AsyncEventHandler<TEventArgs> where TEventArgs : EventArgs
{
    private readonly List<Func<object?, TEventArgs, Task>> _handlers = new();
    private readonly AsyncLock _lock = new();

    public async Task AddHandler(Func<object?, TEventArgs, Task> handler)
    {
        using (await _lock.LockAsync())
        {
            _handlers.Add(handler);
        }
    }

    public async Task RemoveHandler(Func<object?, TEventArgs, Task> handler)
    {
        using (await _lock.LockAsync())
        {
            _handlers.Remove(handler);
        }
    }

    public async Task InvokeAsync(object? sender, TEventArgs args)
    {
        List<Func<object?, TEventArgs, Task>> handlers;
        
        using (await _lock.LockAsync())
        {
            handlers = _handlers.ToList();
        }

        var tasks = handlers.Select(h => h(sender, args));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}