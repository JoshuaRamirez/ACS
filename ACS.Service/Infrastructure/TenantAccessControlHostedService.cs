using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ACS.Service.Services;

namespace ACS.Service.Infrastructure;

public class TenantAccessControlHostedService : BackgroundService
{
    private readonly string _tenantId;
    private readonly TenantRingBuffer _ringBuffer;
    private readonly InMemoryEntityGraph _entityGraph;
    private readonly AccessControlDomainService _domainService;
    private readonly CommandTranslationService _commandTranslator;
    private readonly ILogger<TenantAccessControlHostedService> _logger;

    public TenantAccessControlHostedService(
        TenantConfiguration tenantConfig,
        TenantRingBuffer ringBuffer,
        InMemoryEntityGraph entityGraph,
        AccessControlDomainService domainService,
        CommandTranslationService commandTranslator,
        ILogger<TenantAccessControlHostedService> logger)
    {
        _tenantId = tenantConfig.TenantId;
        _ringBuffer = ringBuffer;
        _entityGraph = entityGraph;
        _domainService = domainService;
        _commandTranslator = commandTranslator;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Access Control Hosted Service for tenant {TenantId}", _tenantId);
        
        // Load entire tenant entity graph into memory using domain service
        await _domainService.LoadEntityGraphAsync(cancellationToken);
        
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
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogDebug("Processing command {CommandType} with ID {RequestId} for tenant {TenantId}", 
                command.GetType().Name, command.RequestId, _tenantId);
            
            var commandDescription = _commandTranslator.GetCommandDescription(command);
            _logger.LogInformation("Executing: {CommandDescription} (RequestId: {RequestId})", 
                commandDescription, command.RequestId);
            
            // Translate web command to domain command
            var domainCommand = _commandTranslator.TranslateCommand(command);
            
            // Execute the domain command through the domain service
            if (_commandTranslator.IsQueryCommand(command))
            {
                // Handle query commands
                if (domainCommand is CheckPermissionCommand checkCmd)
                {
                    var result = await _domainService.ExecuteCommandAsync(checkCmd);
                    _logger.LogInformation("Permission check result: {Result} for {CommandDescription}", 
                        result, commandDescription);
                }
            }
            else if (_commandTranslator.IsMutationCommand(command))
            {
                // Handle mutation commands
                await _domainService.ExecuteCommandAsync(domainCommand);
                _logger.LogInformation("Successfully executed mutation: {CommandDescription}", commandDescription);
            }
            else
            {
                _logger.LogWarning("Unsupported command type for processing: {CommandType}", command.GetType().Name);
            }
            
            var duration = DateTime.UtcNow - startTime;
            _logger.LogDebug("Successfully processed command {RequestId} for tenant {TenantId} in {Duration}ms", 
                command.RequestId, _tenantId, duration.TotalMilliseconds);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning("Unsupported command {CommandType} for tenant {TenantId}: {Message}", 
                command.GetType().Name, _tenantId, ex.Message);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Error processing command {RequestId} for tenant {TenantId} after {Duration}ms: {Error}", 
                command.RequestId, _tenantId, duration.TotalMilliseconds, ex.Message);
            
            // In a production system, you might want to:
            // 1. Send error response back to waiting client
            // 2. Dead letter the command for retry
            // 3. Trigger monitoring alerts
            // 4. Apply circuit breaker patterns
            throw;
        }
    }
}