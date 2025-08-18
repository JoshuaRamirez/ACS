using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using System.Reflection;
using ACS.Core.Security;

namespace ACS.Infrastructure.Security;

/// <summary>
/// Entity Framework interceptor for automatic field encryption/decryption
/// </summary>
public class EncryptionInterceptor : SaveChangesInterceptor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EncryptionInterceptor> _logger;

    public EncryptionInterceptor(IServiceProvider serviceProvider, ILogger<EncryptionInterceptor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, 
        InterceptionResult<int> result, 
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context != null)
        {
            await EncryptEntityFieldsAsync(context);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        var context = eventData.Context;
        if (context != null)
        {
            EncryptEntityFields(context);
        }

        return base.SavingChanges(eventData, result);
    }

    private async Task EncryptEntityFieldsAsync(DbContext context)
    {
        using var scope = _serviceProvider.CreateScope();
        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        
        // Get tenant ID from context (assuming it's available)
        var tenantId = GetTenantIdFromContext(context);
        if (string.IsNullOrEmpty(tenantId))
        {
            _logger.LogWarning("No tenant ID found in context, skipping encryption");
            return;
        }

        var entriesToProcess = context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
            .Where(e => e.Entity.GetType().GetCustomAttribute<EncryptedEntityAttribute>() != null);

        foreach (var entry in entriesToProcess)
        {
            await ProcessEntityForEncryptionAsync(entry, encryptionService, tenantId);
        }
    }

    private void EncryptEntityFields(DbContext context)
    {
        using var scope = _serviceProvider.CreateScope();
        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        
        var tenantId = GetTenantIdFromContext(context);
        if (string.IsNullOrEmpty(tenantId))
        {
            _logger.LogWarning("No tenant ID found in context, skipping encryption");
            return;
        }

        var entriesToProcess = context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
            .Where(e => e.Entity.GetType().GetCustomAttribute<EncryptedEntityAttribute>() != null);

        foreach (var entry in entriesToProcess)
        {
            ProcessEntityForEncryption(entry, encryptionService, tenantId);
        }
    }

    private async Task ProcessEntityForEncryptionAsync(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, IEncryptionService encryptionService, string tenantId)
    {
        var entityType = entry.Entity.GetType();
        var properties = entityType.GetProperties()
            .Where(p => p.GetCustomAttribute<EncryptedFieldAttribute>() != null);

        foreach (var property in properties)
        {
            var encryptedAttr = property.GetCustomAttribute<EncryptedFieldAttribute>()!;
            var value = property.GetValue(entry.Entity);
            
            if (value != null && value is string plainText && !string.IsNullOrEmpty(plainText))
            {
                try
                {
                    var fieldName = !string.IsNullOrEmpty(encryptedAttr.FieldName) ? encryptedAttr.FieldName : property.Name;
                    var entityId = GetEntityId(entry.Entity);
                    
                    var encryptedField = await encryptionService.EncryptFieldAsync(plainText, fieldName, entityId, tenantId);
                    
                    // Store encrypted field as JSON in the property
                    var encryptedJson = System.Text.Json.JsonSerializer.Serialize(encryptedField);
                    property.SetValue(entry.Entity, encryptedJson);
                    
                    _logger.LogDebug("Encrypted field {FieldName} for entity {EntityId} in tenant {TenantId}", 
                        fieldName, entityId, tenantId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to encrypt field {PropertyName} for entity in tenant {TenantId}", 
                        property.Name, tenantId);
                    throw;
                }
            }
        }
    }

    private void ProcessEntityForEncryption(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, IEncryptionService encryptionService, string tenantId)
    {
        var entityType = entry.Entity.GetType();
        var properties = entityType.GetProperties()
            .Where(p => p.GetCustomAttribute<EncryptedFieldAttribute>() != null);

        foreach (var property in properties)
        {
            var encryptedAttr = property.GetCustomAttribute<EncryptedFieldAttribute>()!;
            var value = property.GetValue(entry.Entity);
            
            if (value != null && value is string plainText && !string.IsNullOrEmpty(plainText))
            {
                try
                {
                    var fieldName = !string.IsNullOrEmpty(encryptedAttr.FieldName) ? encryptedAttr.FieldName : property.Name;
                    var entityId = GetEntityId(entry.Entity);
                    
                    // For synchronous version, we need to call async method synchronously
                    // This is not ideal but necessary for EF Core synchronous operations
                    var encryptedField = encryptionService.EncryptFieldAsync(plainText, fieldName, entityId, tenantId).GetAwaiter().GetResult();
                    
                    var encryptedJson = System.Text.Json.JsonSerializer.Serialize(encryptedField);
                    property.SetValue(entry.Entity, encryptedJson);
                    
                    _logger.LogDebug("Encrypted field {FieldName} for entity {EntityId} in tenant {TenantId}", 
                        fieldName, entityId, tenantId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to encrypt field {PropertyName} for entity in tenant {TenantId}", 
                        property.Name, tenantId);
                    throw;
                }
            }
        }
    }

    private string GetTenantIdFromContext(DbContext context)
    {
        // Try to get tenant ID from various sources
        // This assumes you have a way to associate tenant ID with the context
        
        // Method 1: Check if context has a TenantId property
        var tenantIdProperty = context.GetType().GetProperty("TenantId");
        if (tenantIdProperty != null)
        {
            var tenantId = tenantIdProperty.GetValue(context)?.ToString();
            if (!string.IsNullOrEmpty(tenantId))
                return tenantId;
        }

        // Method 2: Check database name for tenant identifier
        var connectionString = context.Database.GetConnectionString();
        if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("Database=ACS_"))
        {
            var dbNameStart = connectionString.IndexOf("Database=ACS_") + "Database=ACS_".Length;
            var dbNameEnd = connectionString.IndexOf(';', dbNameStart);
            if (dbNameEnd == -1) dbNameEnd = connectionString.Length;
            
            return connectionString.Substring(dbNameStart, dbNameEnd - dbNameStart);
        }

        // Method 3: Check for tenant context service in DI (if available)
        try
        {
            using var scope = _serviceProvider.CreateScope();
            // Try to get tenant context service without hard dependency
            var serviceType = Type.GetType("ACS.WebApi.Services.ITenantContextService, ACS.WebApi");
            if (serviceType != null)
            {
                var tenantContext = scope.ServiceProvider.GetService(serviceType);
                if (tenantContext != null)
                {
                    var method = serviceType.GetMethod("GetTenantId");
                    var tenantId = method?.Invoke(tenantContext, null)?.ToString();
                    if (!string.IsNullOrEmpty(tenantId))
                        return tenantId;
                }
            }
        }
        catch
        {
            // Ignore if service not available
        }

        return string.Empty;
    }

    private string GetEntityId(object entity)
    {
        // Try to get entity ID from common property names
        var idProperties = new[] { "Id", "EntityId", "Uid" };
        
        foreach (var propName in idProperties)
        {
            var property = entity.GetType().GetProperty(propName);
            if (property != null)
            {
                var value = property.GetValue(entity);
                return value?.ToString() ?? string.Empty;
            }
        }
        
        return string.Empty;
    }
}

/// <summary>
/// Interceptor for decrypting data when reading from database
/// Note: This is a simplified implementation. For full decryption on read,
/// you would need to implement a custom DbDataReader wrapper.
/// </summary>
public class DecryptionInterceptor : DbCommandInterceptor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DecryptionInterceptor> _logger;

    public DecryptionInterceptor(IServiceProvider serviceProvider, ILogger<DecryptionInterceptor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, 
        CommandExecutedEventData eventData, 
        DbDataReader result, 
        CancellationToken cancellationToken = default)
    {
        // For complex scenarios, you might need to implement custom result processing
        // This is a simplified example - real implementation would need custom reader
        return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }
}