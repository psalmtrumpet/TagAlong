namespace TagAlong.EventBus;

public class InMemoryEventBusSubscriptionManager : IEventBusSubscriptionManager
{
    private readonly Dictionary<string, List<Type>> _handlers = new();
    private readonly List<Type> _eventTypes = new();

    public bool IsEmpty => !_handlers.Any();
    public event EventHandler<string>? OnEventRemoved;

    public void AddSubscription<T, THandler>()
        where T : IntegrationEvent
        where THandler : IIntegrationEventHandler<T>
    {
        var eventName = GetEventKey<T>();
        DoAddSubscription(typeof(THandler), eventName);

        if (!_eventTypes.Contains(typeof(T)))
        {
            _eventTypes.Add(typeof(T));
        }
    }

    private void DoAddSubscription(Type handlerType, string eventName)
    {
        if (!HasSubscriptionsForEvent(eventName))
        {
            _handlers.Add(eventName, new List<Type>());
        }

        if (_handlers[eventName].Any(h => h == handlerType))
        {
            throw new ArgumentException($"Handler Type {handlerType.Name} already registered for '{eventName}'");
        }

        _handlers[eventName].Add(handlerType);
    }

    public void RemoveSubscription<T, THandler>()
        where T : IntegrationEvent
        where THandler : IIntegrationEventHandler<T>
    {
        var eventName = GetEventKey<T>();
        var handlerToRemove = _handlers[eventName].SingleOrDefault(h => h == typeof(THandler));

        if (handlerToRemove != null)
        {
            _handlers[eventName].Remove(handlerToRemove);
            if (!_handlers[eventName].Any())
            {
                _handlers.Remove(eventName);
                var eventType = _eventTypes.SingleOrDefault(e => e.Name == eventName);
                if (eventType != null)
                {
                    _eventTypes.Remove(eventType);
                }
                RaiseOnEventRemoved(eventName);
            }
        }
    }

    public bool HasSubscriptionsForEvent<T>() where T : IntegrationEvent
    {
        var key = GetEventKey<T>();
        return HasSubscriptionsForEvent(key);
    }

    public bool HasSubscriptionsForEvent(string eventName) => _handlers.ContainsKey(eventName);

    public Type? GetEventTypeByName(string eventName) => _eventTypes.SingleOrDefault(t => t.Name == eventName);

    public void Clear() => _handlers.Clear();

    public IEnumerable<Type> GetHandlersForEvent<T>() where T : IntegrationEvent
    {
        var key = GetEventKey<T>();
        return GetHandlersForEvent(key);
    }

    public IEnumerable<Type> GetHandlersForEvent(string eventName) => _handlers[eventName];

    public string GetEventKey<T>() where T : IntegrationEvent => typeof(T).Name;

    private void RaiseOnEventRemoved(string eventName)
    {
        OnEventRemoved?.Invoke(this, eventName);
    }
}
