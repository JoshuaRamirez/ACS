using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Infrastructure;

public class TenantAccessControlHostedService : BackgroundService
{
    private readonly string _tenantId;
    private readonly TenantRingBuffer _ringBuffer;
    private readonly InMemoryEntityGraph _entityGraph;
    private readonly ILogger<TenantAccessControlHostedService> _logger;

    public TenantAccessControlHostedService(
        TenantConfiguration tenantConfig,
        TenantRingBuffer ringBuffer,
        InMemoryEntityGraph entityGraph,
        ILogger<TenantAccessControlHostedService> logger)
    {
        _tenantId = tenantConfig.TenantId;
        _ringBuffer = ringBuffer;
        _entityGraph = entityGraph;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Access Control Hosted Service for tenant {TenantId}", _tenantId);
        
        // Load entire tenant entity graph into memory
        await _entityGraph.LoadFromDatabaseAsync(cancellationToken);
        
        // Hydrate normalizer collections to reference domain objects
        _entityGraph.HydrateNormalizerReferences();
        
        _logger.LogInformation("Entity graph loaded and normalizers hydrated for tenant {TenantId}", _tenantId);
        
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Access Control event processor started for tenant {TenantId}", _tenantId);
        
        try
        {
            await foreach (var command in _ringBuffer.ReadAllAsync(stoppingToken))
            {
                await ProcessCommandAsync(command);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Access Control event processor stopping for tenant {TenantId}", _tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Access Control event processor for tenant {TenantId}", _tenantId);
            throw;
        }
    }

    private async Task ProcessCommandAsync(WebRequestCommand command)
    {
        try
        {
            _logger.LogDebug("Processing command {CommandType} with ID {RequestId} for tenant {TenantId}", 
                command.GetType().Name, command.RequestId, _tenantId);
            
            // Command processing will be implemented in Phase 2
            // For now, just log the command
            _logger.LogInformation("Command processing implementation pending for Phase 2: {CommandType}", 
                command.GetType().Name);
            
            await Task.CompletedTask;
            
            _logger.LogDebug("Successfully processed command {RequestId} for tenant {TenantId}", 
                command.RequestId, _tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command {RequestId} for tenant {TenantId}: {Error}", 
                command.RequestId, _tenantId, ex.Message);
            
            // In a production system, you might want to:
            // 1. Send error response back to waiting client
            // 2. Dead letter the command
            // 3. Trigger alerts
            throw;
        }
    }
}