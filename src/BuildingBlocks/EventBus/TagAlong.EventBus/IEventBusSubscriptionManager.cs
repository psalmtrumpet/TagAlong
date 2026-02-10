namespace TagAlong.EventBus;

public interface IEventBusSubscriptionManager
{
    bool IsEmpty { get; }
    event EventHandler<string> OnEventRemoved;

    void AddSubscription<T, THandler>()
        where T : IntegrationEvent
        where THandler : IIntegrationEventHandler<T>;

    void RemoveSubscription<T, THandler>()
        where T : IntegrationEvent
        where THandler : IIntegrationEventHandler<T>;

    bool HasSubscriptionsForEvent<T>() where T : IntegrationEvent;
    bool HasSubscriptionsForEvent(string eventName);
    Type? GetEventTypeByName(string eventName);
    void Clear();
    IEnumerable<Type> GetHandlersForEvent<T>() where T : IntegrationEvent;
    IEnumerable<Type> GetHandlersForEvent(string eventName);
    string GetEventKey<T>() where T : IntegrationEvent;
}
