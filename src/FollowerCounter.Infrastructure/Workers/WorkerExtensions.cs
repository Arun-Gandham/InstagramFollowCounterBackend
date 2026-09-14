using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FollowerCounter.Infrastructure.Workers;

public static class WorkerExtensions
{
    public static IServiceCollection AddBackgroundWorkers(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WorkerOptions>(configuration.GetSection(WorkerOptions.SectionName));

        services.AddHostedService<InstagramFollowerRefreshWorker>();
        services.AddHostedService<InstagramTokenRefreshWorker>();
        services.AddHostedService<DatabaseCleanupWorker>();

        return services;
    }
}
