using ACS.IntegrationValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ACS.IntegrationValidation;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🚀 ACS System Integration Validation");
        Console.WriteLine("=====================================");
        Console.WriteLine();

        try
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            // Build service provider
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            services.AddSingleton<IConfiguration>(configuration);
            services.AddScoped<FocusedIntegrationTests>();

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();

            var integrationTests = scope.ServiceProvider.GetRequiredService<FocusedIntegrationTests>();
            var report = await integrationTests.RunAllTestsAsync();

            // Generate and display report
            Console.WriteLine();
            Console.WriteLine("===============================================");
            Console.WriteLine("ACS INTEGRATION VALIDATION REPORT");
            Console.WriteLine("===============================================");
            Console.WriteLine();
            Console.WriteLine(report.GenerateReport());

            // Save report to file
            var reportPath = Path.Combine(Environment.CurrentDirectory, $"integration-validation-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md");
            await File.WriteAllTextAsync(reportPath, report.GenerateReport());
            Console.WriteLine($"📄 Report saved to: {reportPath}");

            return report.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Critical error during validation: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return 1;
        }
    }
}