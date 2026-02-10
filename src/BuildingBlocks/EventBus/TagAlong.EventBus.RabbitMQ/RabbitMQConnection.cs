using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Net.Sockets;

namespace TagAlong.EventBus.RabbitMQ;

public interface IRabbitMQConnection : IDisposable
{
    bool IsConnected { get; }
    bool TryConnect();
    IModel CreateModel();
}

public class RabbitMQConnection : IRabbitMQConnection
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILogger<RabbitMQConnection> _logger;
    private readonly int _retryCount;
    private IConnection? _connection;
    private bool _disposed;
    private readonly object _syncRoot = new();

    public RabbitMQConnection(
        IConnectionFactory connectionFactory,
        ILogger<RabbitMQConnection> logger,
        int retryCount = 5)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _retryCount = retryCount;
    }

    public bool IsConnected => _connection is { IsOpen: true } && !_disposed;

    public bool TryConnect()
    {
        _logger.LogInformation("RabbitMQ Client is trying to connect");

        lock (_syncRoot)
        {
            var policy = Policy.Handle<SocketException>()
                .Or<BrokerUnreachableException>()
                .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (ex, time) =>
                    {
                        _logger.LogWarning(ex, "RabbitMQ Client could not connect after {TimeOut}s", time.TotalSeconds);
                    });

            policy.Execute(() =>
            {
                _connection = _connectionFactory.CreateConnection();
            });

            if (IsConnected)
            {
                _connection!.ConnectionShutdown += OnConnectionShutdown;
                _connection.CallbackException += OnCallbackException;
                _connection.ConnectionBlocked += OnConnectionBlocked;

                _logger.LogInformation("RabbitMQ Client acquired a persistent connection to '{HostName}'",
                    _connection.Endpoint.HostName);

                return true;
            }

            _logger.LogCritical("FATAL ERROR: RabbitMQ connections could not be created and opened");
            return false;
        }
    }

    public IModel CreateModel()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("No RabbitMQ connections are available to perform this action");
        }

        return _connection!.CreateModel();
    }

    private void OnConnectionBlocked(object? sender, ConnectionBlockedEventArgs e)
    {
        if (_disposed) return;
        _logger.LogWarning("A RabbitMQ connection is shutdown. Trying to re-connect...");
        TryConnect();
    }

    private void OnCallbackException(object? sender, CallbackExceptionEventArgs e)
    {
        if (_disposed) return;
        _logger.LogWarning("A RabbitMQ connection throw exception. Trying to re-connect...");
        TryConnect();
    }

    private void OnConnectionShutdown(object? sender, ShutdownEventArgs reason)
    {
        if (_disposed) return;
        _logger.LogWarning("A RabbitMQ connection is on shutdown. Trying to re-connect...");
        TryConnect();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _connection?.Dispose();
        }
        catch (IOException ex)
        {
            _logger.LogCritical(ex, "Error disposing RabbitMQ connection");
        }
    }
}
