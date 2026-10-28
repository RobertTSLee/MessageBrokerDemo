using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MessageBrokerDemo.Broker.Messaging;

public sealed class RabbitMqConsumer(
    IOptions<RabbitMqOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqConsumer> logger) : BackgroundService
{
    private readonly RabbitMqOptions _options = options.Value;
    private IConnection? _connection;
    private IChannel? _channel;

    public required string QueueName { get; init; }
    public required string RoutingKey { get; init; }
    public required Type HandlerType { get; init; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        var deadLetterExchange = $"{_options.Exchange}.dlx";
        var deadLetterQueue = $"{QueueName}.dlq";

        await _channel.ExchangeDeclareAsync(_options.Exchange, ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);
        await _channel.ExchangeDeclareAsync(deadLetterExchange, ExchangeType.Direct, durable: true, cancellationToken: stoppingToken);
        await _channel.QueueDeclareAsync(deadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(deadLetterQueue, deadLetterExchange, QueueName, cancellationToken: stoppingToken);

        var queueArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = deadLetterExchange,
            ["x-dead-letter-routing-key"] = QueueName
        };
        await _channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false, arguments: queueArguments, cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(QueueName, _options.Exchange, RoutingKey, cancellationToken: stoppingToken);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var payload = Encoding.UTF8.GetString(ea.Body.Span);
                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = (IMessageHandler)scope.ServiceProvider.GetRequiredService(HandlerType);
                await handler.HandleAsync(ea.RoutingKey, payload, stoppingToken);
                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to handle message on queue {Queue}.", QueueName);
                await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        logger.LogInformation("Subscribed to {RoutingKey} on queue {Queue}.", RoutingKey, QueueName);

        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
