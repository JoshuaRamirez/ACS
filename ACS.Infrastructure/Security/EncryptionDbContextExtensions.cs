using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ACS.Infrastructure.Security;

/// <summary>
/// Extension methods for configuring DbContext with encryption interceptors
/// </summary>
public static class EncryptionDbContextExtensions
{
    /// <summary>
    /// Configure a DbContext with encryption interceptors
    /// </summary>
    public static void ConfigureEncryptionInterceptors(this DbContextOptionsBuilder optionsBuilder, IServiceProvider serviceProvider)
    {
        // Add encryption interceptors if available
        var encryptionInterceptor = serviceProvider.GetService<EncryptionInterceptor>();
        if (encryptionInterceptor != null)
        {
            optionsBuilder.AddInterceptors(encryptionInterceptor);
        }
        
        var decryptionInterceptor = serviceProvider.GetService<DecryptionInterceptor>();
        if (decryptionInterceptor != null)
        {
            optionsBuilder.AddInterceptors(decryptionInterceptor);
        }
    }
}