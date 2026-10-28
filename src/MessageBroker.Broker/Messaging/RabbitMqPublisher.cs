using System.Text;

using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace MessageBrokerDemo.Broker.Messaging;

public sealed class RabbitMqPublisher(IOptions<RabbitMqOptions> options) : IMessageBusPublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;
    private volatile string? _returnReplyText;

    public async Task PublishAsync(string routingKey, string payload, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);

            await channel.ExchangeDeclareAsync(_options.Exchange, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);

            _returnReplyText = null;
            var properties = new BasicProperties { Persistent = true, ContentType = "application/json" };
            await channel.BasicPublishAsync(
                exchange: _options.Exchange,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(payload),
                cancellationToken: cancellationToken);

            if (_returnReplyText is { } replyText)
            {
                throw new InvalidOperationException($"Message for routing key '{routingKey}' was not routed: {replyText}");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        if (_connection is null || !_connection.IsOpen)
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost
            };
            _connection = await factory.CreateConnectionAsync(cancellationToken);
        }

        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken: cancellationToken);

        _channel.BasicReturnAsync += (_, args) =>
        {
            _returnReplyText = args.ReplyText;
            return Task.CompletedTask;
        };

        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}
