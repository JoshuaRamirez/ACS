using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Diagnostics;
using System.Transactions;

namespace ACS.Infrastructure.Performance;

/// <summary>
/// Implementation of batch processing for database operations
/// </summary>
public class BatchProcessor : IBatchProcessor
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<BatchProcessor> _logger;
    private readonly IConnectionPoolMonitor _connectionPoolMonitor;

    public BatchProcessor(
        IConfiguration configuration,
        ILogger<BatchProcessor> logger,
        IConnectionPoolMonitor connectionPoolMonitor)
    {
        _configuration = configuration;
        _logger = logger;
        _connectionPoolMonitor = connectionPoolMonitor;
    }

    public async Task<BatchResult<TResult>> ProcessBatchAsync<TItem, TResult>(
        IEnumerable<TItem> items,
        Func<IEnumerable<TItem>, Task<IEnumerable<TResult>>> batchOperation,
        BatchOptions? options = null)
    {
        options ??= new BatchOptions();
        var result = new BatchResult<TResult>();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var itemList = items.ToList();
            var batches = itemList.Chunk(options.BatchSize);
            
            var semaphore = new SemaphoreSlim(options.MaxDegreeOfParallelism);
            var tasks = new List<Task>();

            foreach (var batch in batches)
            {
                await semaphore.WaitAsync();
                
                var task = ProcessSingleBatchAsync(batch, batchOperation, options, result)
                    .ContinueWith(_ => semaphore.Release());
                
                tasks.Add(task);
                
                if (options.StopOnFirstError && result.Errors.Any())
                {
                    break;
                }
            }

            await Task.WhenAll(tasks);
            
            result.Success = result.Errors.Count == 0;
            result.ElapsedTime = stopwatch.Elapsed;
            
            _logger.LogInformation(
                "Batch processing completed: {Processed}/{Total} items in {Elapsed}ms",
                result.ProcessedCount,
                itemList.Count,
                result.ElapsedTime.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch processing failed");
            result.Success = false;
            result.Errors.Add(new BatchError
            {
                Message = ex.Message,
                Exception = ex,
                Timestamp = DateTime.UtcNow
            });
            result.ElapsedTime = stopwatch.Elapsed;
            return result;
        }
    }

    private async Task ProcessSingleBatchAsync<TItem, TResult>(
        TItem[] batch,
        Func<IEnumerable<TItem>, Task<IEnumerable<TResult>>> batchOperation,
        BatchOptions options,
        BatchResult<TResult> result)
    {
        var retryCount = 0;
        while (retryCount <= options.RetryCount)
        {
            try
            {
                var batchResults = await batchOperation(batch);
                
                lock (result)
                {
                    result.Results.AddRange(batchResults);
                    result.ProcessedCount += batch.Length;
                }
                
                return;
            }
            catch (Exception ex)
            {
                retryCount++;
                
                if (retryCount > options.RetryCount)
                {
                    lock (result)
                    {
                        result.FailedCount += batch.Length;
                        result.Errors.Add(new BatchError
                        {
                            BatchIndex = result.ProcessedCount + result.FailedCount,
                            Message = $"Batch failed after {options.RetryCount} retries: {ex.Message}",
                            Exception = ex,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                    
                    if (options.StopOnFirstError)
                    {
                        throw;
                    }
                }
                else
                {
                    await Task.Delay(options.RetryDelay);
                }
            }
        }
    }

    public async Task<BatchExecutionResult> ExecuteBatchCommandsAsync(
        IEnumerable<BatchCommand> commands,
        BatchOptions? options = null)
    {
        options ??= new BatchOptions();
        var result = new BatchExecutionResult();
        var stopwatch = Stopwatch.StartNew();

        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Connection string not configured");
        }

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        SqlTransaction? transaction = null;
        if (options.EnableTransaction)
        {
            transaction = connection.BeginTransaction();
        }

        try
        {
            var commandList = commands.ToList();
            var batches = commandList.Chunk(options.BatchSize);

            foreach (var batch in batches)
            {
                var batchCommand = BuildBatchCommand(connection, batch, transaction);
                batchCommand.CommandTimeout = (int)options.Timeout.TotalSeconds;

                var affectedRows = await batchCommand.ExecuteNonQueryAsync();
                result.AffectedRows += affectedRows;
                result.ExecutedCommands += batch.Length;

                if (options.StopOnFirstError && result.Errors.Any())
                {
                    break;
                }
            }

            transaction?.Commit();
            result.Success = true;
        }
        catch (Exception ex)
        {
            transaction?.Rollback();
            
            _logger.LogError(ex, "Batch command execution failed");
            result.Success = false;
            result.Errors.Add(new BatchError
            {
                Message = ex.Message,
                Exception = ex,
                Timestamp = DateTime.UtcNow
            });
        }
        finally
        {
            transaction?.Dispose();
            result.ElapsedTime = stopwatch.Elapsed;
        }

        return result;
    }

    private SqlCommand BuildBatchCommand(SqlConnection connection, BatchCommand[] batch, SqlTransaction? transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;

        var commandTexts = new List<string>();
        var parameterIndex = 0;

        foreach (var batchCommand in batch)
        {
            var parameterizedText = batchCommand.CommandText;
            
            foreach (var parameter in batchCommand.Parameters)
            {
                var paramName = $"@p{parameterIndex++}";
                parameterizedText = parameterizedText.Replace($"@{parameter.Key}", paramName);
                command.Parameters.AddWithValue(paramName, parameter.Value ?? DBNull.Value);
            }
            
            commandTexts.Add(parameterizedText);
        }

        command.CommandText = string.Join(";\n", commandTexts);
        return command;
    }

    public async Task<int> BulkInsertAsync<T>(
        IEnumerable<T> entities,
        BulkInsertOptions? options = null) where T : class
    {
        options ??= new BulkInsertOptions();
        
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Connection string not configured");
        }

        var entityList = entities.ToList();
        if (!entityList.Any())
        {
            return 0;
        }

        try
        {
            using var bulkCopy = new SqlBulkCopy(connectionString)
            {
                BatchSize = options.BatchSize,
                BulkCopyTimeout = (int)options.Timeout.TotalSeconds,
                DestinationTableName = options.TableName ?? typeof(T).Name
            };

            // Configure bulk copy options
            var bulkOptions = SqlBulkCopyOptions.Default;
            if (options.CheckConstraints) bulkOptions |= SqlBulkCopyOptions.CheckConstraints;
            if (options.FireTriggers) bulkOptions |= SqlBulkCopyOptions.FireTriggers;
            if (options.KeepIdentity) bulkOptions |= SqlBulkCopyOptions.KeepIdentity;
            if (options.KeepNulls) bulkOptions |= SqlBulkCopyOptions.KeepNulls;
            
            bulkCopy.SqlRowsCopied += (sender, e) =>
            {
                _logger.LogDebug("Bulk insert progress: {RowsCopied} rows copied", e.RowsCopied);
            };

            // Convert entities to DataTable
            var dataTable = ConvertToDataTable(entityList);
            
            await bulkCopy.WriteToServerAsync(dataTable);
            
            _logger.LogInformation("Bulk inserted {Count} {Type} entities", entityList.Count, typeof(T).Name);
            return entityList.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk insert failed for {Type}", typeof(T).Name);
            throw;
        }
    }

    public async Task<int> BulkUpdateAsync<T>(
        IEnumerable<T> entities,
        BulkUpdateOptions? options = null) where T : class
    {
        options ??= new BulkUpdateOptions();
        
        var entityList = entities.ToList();
        if (!entityList.Any())
        {
            return 0;
        }

        // For bulk update, we'll use a MERGE statement approach
        var tableName = typeof(T).Name;
        var updateColumns = options.UpdateColumns.Any() 
            ? options.UpdateColumns 
            : GetEntityProperties<T>().Where(p => !options.KeyColumns.Contains(p)).ToList();
        
        var keyColumns = options.KeyColumns.Any() 
            ? options.KeyColumns 
            : new List<string> { "Id" }; // Default to Id

        var mergeStatement = BuildMergeStatement(tableName, keyColumns, updateColumns);
        
        var batchCommands = entityList.Select(entity =>
        {
            var parameters = new Dictionary<string, object?>();
            foreach (var prop in typeof(T).GetProperties())
            {
                parameters[prop.Name] = prop.GetValue(entity);
            }
            
            return new BatchCommand
            {
                CommandText = mergeStatement,
                Parameters = parameters
            };
        });

        var result = await ExecuteBatchCommandsAsync(batchCommands, options);
        return result.AffectedRows;
    }

    public async Task<int> BulkDeleteAsync<T>(IEnumerable<T> entities) where T : class
    {
        var entityList = entities.ToList();
        if (!entityList.Any())
        {
            return 0;
        }

        var tableName = typeof(T).Name;
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty == null)
        {
            throw new InvalidOperationException($"Entity {typeof(T).Name} does not have an Id property");
        }

        var ids = entityList.Select(e => idProperty.GetValue(e)).ToList();
        var idParameters = string.Join(",", ids.Select((_, i) => $"@id{i}"));
        
        var deleteCommand = new BatchCommand
        {
            CommandText = $"DELETE FROM {tableName} WHERE Id IN ({idParameters})",
            Parameters = ids.Select((id, i) => new { Key = $"id{i}", Value = id })
                           .ToDictionary(x => x.Key, x => x.Value)
        };

        var result = await ExecuteBatchCommandsAsync(new[] { deleteCommand });
        return result.AffectedRows;
    }

    private DataTable ConvertToDataTable<T>(List<T> entities)
    {
        var dataTable = new DataTable(typeof(T).Name);
        var properties = typeof(T).GetProperties();

        foreach (var prop in properties)
        {
            var columnType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            dataTable.Columns.Add(prop.Name, columnType);
        }

        foreach (var entity in entities)
        {
            var row = dataTable.NewRow();
            foreach (var prop in properties)
            {
                row[prop.Name] = prop.GetValue(entity) ?? DBNull.Value;
            }
            dataTable.Rows.Add(row);
        }

        return dataTable;
    }

    private string BuildMergeStatement(string tableName, List<string> keyColumns, List<string> updateColumns)
    {
        var keyJoinCondition = string.Join(" AND ", keyColumns.Select(k => $"target.{k} = source.{k}"));
        var updateSetClause = string.Join(", ", updateColumns.Select(c => $"target.{c} = source.{c}"));
        
        return $@"
            MERGE {tableName} AS target
            USING (SELECT {string.Join(", ", keyColumns.Concat(updateColumns).Select(c => $"@{c} AS {c}"))}) AS source
            ON {keyJoinCondition}
            WHEN MATCHED THEN
                UPDATE SET {updateSetClause};";
    }

    private List<string> GetEntityProperties<T>()
    {
        return typeof(T).GetProperties()
            .Where(p => p.CanRead && p.CanWrite)
            .Select(p => p.Name)
            .ToList();
    }
}