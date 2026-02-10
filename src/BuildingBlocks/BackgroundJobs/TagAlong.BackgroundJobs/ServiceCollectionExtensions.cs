using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using TagAlong.BackgroundJobs.Jobs;

namespace TagAlong.BackgroundJobs;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHangfireBackgroundJobs(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddHangfire(configuration => configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));

        services.AddHangfireServer();

        return services;
    }

    public static void RegisterRecurringJobs(this IServiceProvider serviceProvider)
    {
        var recurringJobManager = serviceProvider.GetRequiredService<IRecurringJobManager>();

        // Register all IRecurringJob implementations
        using var scope = serviceProvider.CreateScope();
        var jobs = scope.ServiceProvider.GetServices<IRecurringJob>();

        foreach (var job in jobs)
        {
            recurringJobManager.AddOrUpdate(
                job.JobId,
                () => job.ExecuteAsync(CancellationToken.None),
                job.CronExpression);
        }
    }
}
