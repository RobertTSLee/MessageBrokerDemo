using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using MessageBrokerDemo.Broker.Messaging;

namespace MessageBrokerDemo.Broker;

public static class DependencyInjection
{
    public static IServiceCollection AddRabbitMqBroker(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName));

        // Register the RabbitMqPublisher as a singleton service for publishing messages to RabbitMQ.
        // reuse connection and channel for performance, as recommended by RabbitMQ best practices.
        services.AddSingleton<IMessageBusPublisher, RabbitMqPublisher>();

        return services;
    }

    public static IServiceCollection AddRabbitMqConsumer<THandler>(this IServiceCollection services, string queueName, string routingKey)
        where THandler : class, IMessageHandler
    {
        services.AddScoped<THandler>();

        // Register the RabbitMqConsumer as a hosted service for consuming messages from RabbitMQ.
        services.AddHostedService(sp => new RabbitMqConsumer(
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RabbitMqOptions>>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RabbitMqConsumer>>())
        {
            QueueName = queueName,
            RoutingKey = routingKey,
            HandlerType = typeof(THandler)
        });

        return services;
    }
}
