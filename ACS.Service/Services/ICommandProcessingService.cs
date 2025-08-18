using ACS.Service.Infrastructure;

namespace ACS.Service.Services;

public interface ICommandProcessingService
{
    Task<TResult> ExecuteCommandAsync<TResult>(Infrastructure.WebRequestCommand command);
    Task ExecuteCommandAsync(Infrastructure.WebRequestCommand command);
}