using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace TagAlong.EventBus.RabbitMQ;

public class RabbitMQEventBus : IEventBus, IDisposable
{
    private const string BROKER_NAME = "tagalong_event_bus";

    private readonly IRabbitMQConnection _connection;
    private readonly ILogger<RabbitMQEventBus> _logger;
    private readonly IEventBusSubscriptionManager _subscriptionManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _queueName;
    private readonly int _retryCount;

    private IModel? _consumerChannel;

    public RabbitMQEventBus(
        IRabbitMQConnection connection,
        ILogger<RabbitMQEventBus> logger,
        IServiceProvider serviceProvider,
        IEventBusSubscriptionManager subscriptionManager,
        string queueName,
        int retryCount = 5)
    {
        _connection = connection;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _subscriptionManager = subscriptionManager;
        _queueName = queueName;
        _retryCount = retryCount;
        _consumerChannel = CreateConsumerChannel();
        _subscriptionManager.OnEventRemoved += OnEventRemoved;
    }

    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IntegrationEvent
    {
        if (!_connection.IsConnected)
        {
            _connection.TryConnect();
        }

        var policy = Policy.Handle<BrokerUnreachableException>()
            .Or<SocketException>()
            .WaitAndRetryAsync(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (ex, time) =>
                {
                    _logger.LogWarning(ex, "Could not publish event: {EventId} after {Timeout}s", @event.Id, time.TotalSeconds);
                });

        var eventName = @event.GetType().Name;

        _logger.LogTrace("Creating RabbitMQ channel to publish event: {EventId} ({EventName})", @event.Id, eventName);

        using var channel = _connection.CreateModel();
        _logger.LogTrace("Declaring RabbitMQ exchange to publish event: {EventId}", @event.Id);

        channel.ExchangeDeclare(exchange: BROKER_NAME, type: "direct");

        var body = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await policy.ExecuteAsync(async () =>
        {
            var properties = channel.CreateBasicProperties();
            properties.DeliveryMode = 2;

            _logger.LogTrace("Publishing event to RabbitMQ: {EventId}", @event.Id);

            channel.BasicPublish(
                exchange: BROKER_NAME,
                routingKey: eventName,
                mandatory: true,
                basicProperties: properties,
                body: body);

            await Task.CompletedTask;
        });
    }

    public void Subscribe<T, THandler>()
        where T : IntegrationEvent
        where THandler : IIntegrationEventHandler<T>
    {
        var eventName = _subscriptionManager.GetEventKey<T>();
        DoInternalSubscription(eventName);

        _logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(THandler).Name);

        _subscriptionManager.AddSubscription<T, THandler>();
        StartBasicConsume();
    }

    private void DoInternalSubscription(string eventName)
    {
        var containsKey = _subscriptionManager.HasSubscriptionsForEvent(eventName);
        if (!containsKey)
        {
            if (!_connection.IsConnected)
            {
                _connection.TryConnect();
            }

            _consumerChannel?.QueueBind(queue: _queueName, exchange: BROKER_NAME, routingKey: eventName);
        }
    }

    private IModel CreateConsumerChannel()
    {
        if (!_connection.IsConnected)
        {
            _connection.TryConnect();
        }

        _logger.LogTrace("Creating RabbitMQ consumer channel");

        var channel = _connection.CreateModel();

        channel.ExchangeDeclare(exchange: BROKER_NAME, type: "direct");

        channel.QueueDeclare(queue: _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        channel.CallbackException += (sender, ea) =>
        {
            _logger.LogWarning(ea.Exception, "Recreating RabbitMQ consumer channel");
            _consumerChannel?.Dispose();
            _consumerChannel = CreateConsumerChannel();
            StartBasicConsume();
        };

        return channel;
    }

    private void StartBasicConsume()
    {
        _logger.LogTrace("Starting RabbitMQ basic consume");

        if (_consumerChannel != null)
        {
            var consumer = new AsyncEventingBasicConsumer(_consumerChannel);

            consumer.Received += Consumer_Received;

            _consumerChannel.BasicConsume(
                queue: _queueName,
                autoAck: false,
                consumer: consumer);
        }
        else
        {
            _logger.LogError("StartBasicConsume can't call on _consumerChannel == null");
        }
    }

    private async Task Consumer_Received(object sender, BasicDeliverEventArgs eventArgs)
    {
        var eventName = eventArgs.RoutingKey;
        var message = Encoding.UTF8.GetString(eventArgs.Body.Span);

        try
        {
            await ProcessEvent(eventName, message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error Processing message \"{Message}\"", message);
        }

        _consumerChannel?.BasicAck(eventArgs.DeliveryTag, multiple: false);
    }

    private async Task ProcessEvent(string eventName, string message)
    {
        _logger.LogTrace("Processing RabbitMQ event: {EventName}", eventName);

        if (_subscriptionManager.HasSubscriptionsForEvent(eventName))
        {
            using var scope = _serviceProvider.CreateScope();
            var handlers = _subscriptionManager.GetHandlersForEvent(eventName);

            foreach (var handlerType in handlers)
            {
                var handler = scope.ServiceProvider.GetService(handlerType);
                if (handler == null) continue;

                var eventType = _subscriptionManager.GetEventTypeByName(eventName);
                if (eventType == null) continue;

                var integrationEvent = JsonSerializer.Deserialize(message, eventType, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                await Task.Yield();

                await (Task)concreteType
                    .GetMethod("HandleAsync")!
                    .Invoke(handler, new object[] { integrationEvent!, CancellationToken.None })!;
            }
        }
        else
        {
            _logger.LogWarning("No subscription for RabbitMQ event: {EventName}", eventName);
        }
    }

    private void OnEventRemoved(object? sender, string eventName)
    {
        if (!_connection.IsConnected)
        {
            _connection.TryConnect();
        }

        using var channel = _connection.CreateModel();
        channel.QueueUnbind(queue: _queueName, exchange: BROKER_NAME, routingKey: eventName);

        if (_subscriptionManager.IsEmpty)
        {
            _queueName.GetType();
            _consumerChannel?.Close();
        }
    }

    public void Dispose()
    {
        _consumerChannel?.Dispose();
        _subscriptionManager.Clear();
    }
}
