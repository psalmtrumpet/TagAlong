using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace TagAlong.EventBus.RabbitMQ;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMQEventBus(
        this IServiceCollection services,
        string connectionString,
        string queueName,
        int retryCount = 5)
    {
        services.AddSingleton<IEventBusSubscriptionManager, InMemoryEventBusSubscriptionManager>();

        services.AddSingleton<IRabbitMQConnection>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RabbitMQConnection>>();
            var factory = new ConnectionFactory
            {
                Uri = new Uri(connectionString),
                DispatchConsumersAsync = true
            };

            return new RabbitMQConnection(factory, logger, retryCount);
        });

        services.AddSingleton<IEventBus, RabbitMQEventBus>(sp =>
        {
            var connection = sp.GetRequiredService<IRabbitMQConnection>();
            var logger = sp.GetRequiredService<ILogger<RabbitMQEventBus>>();
            var subscriptionManager = sp.GetRequiredService<IEventBusSubscriptionManager>();

            return new RabbitMQEventBus(connection, logger, sp, subscriptionManager, queueName, retryCount);
        });

        return services;
    }
}
