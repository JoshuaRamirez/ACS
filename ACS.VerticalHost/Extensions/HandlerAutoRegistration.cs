using System.Reflection;
using ACS.VerticalHost.Services;

namespace ACS.VerticalHost.Extensions;

/// <summary>
/// Extension methods for automatic handler registration using conventions
/// </summary>
public static class HandlerAutoRegistration
{
    /// <summary>
    /// Automatically registers all command and query handlers using reflection
    /// Replaces 70+ lines of manual handler registration
    /// </summary>
    public static IServiceCollection AddHandlersAutoRegistration(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var handlerTypes = assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => type.Namespace == "ACS.VerticalHost.Handlers")
            .ToList();

        var registeredCount = 0;

        foreach (var handlerType in handlerTypes)
        {
            var interfaces = handlerType.GetInterfaces();
            
            foreach (var interfaceType in interfaces)
            {
                if (IsCommandHandlerInterface(interfaceType) || IsQueryHandlerInterface(interfaceType))
                {
                    services.AddTransient(interfaceType, handlerType);
                    registeredCount++;
                    
                    // Log registration for debugging
                    Console.WriteLine($"✅ Auto-registered: {interfaceType.Name} -> {handlerType.Name}");
                }
            }
        }

        Console.WriteLine($"🎯 Total handlers auto-registered: {registeredCount}");
        return services;
    }

    private static bool IsCommandHandlerInterface(Type interfaceType)
    {
        if (!interfaceType.IsGenericType) return false;
        
        var genericTypeDefinition = interfaceType.GetGenericTypeDefinition();
        
        // Check for ICommandHandler<TCommand> or ICommandHandler<TCommand, TResponse>
        return genericTypeDefinition == typeof(ICommandHandler<>) ||
               genericTypeDefinition == typeof(ICommandHandler<,>);
    }

    private static bool IsQueryHandlerInterface(Type interfaceType)
    {
        if (!interfaceType.IsGenericType) return false;
        
        var genericTypeDefinition = interfaceType.GetGenericTypeDefinition();
        
        // Check for IQueryHandler<TQuery, TResponse>
        return genericTypeDefinition == typeof(IQueryHandler<,>);
    }
}