using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.Infrastructure;

public static class HangfireSetup
{
    public static IServiceCollection AddInfrastructureHangfire(this IServiceCollection services)
    {
        // Add Hangfire services.
        services.AddHangfire(configuration => configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseMemoryStorage());

        // Add the processing server as IHostedService
        services.AddHangfireServer();

        return services;
    }
}
