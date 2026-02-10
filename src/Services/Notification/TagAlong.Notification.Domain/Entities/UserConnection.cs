using TagAlong.Common.Domain;

namespace TagAlong.Notification.Domain.Entities;

public class UserConnection : Entity
{
    public Guid UserId { get; private set; }
    public string ConnectionId { get; private set; } = null!;
    public string? DeviceType { get; private set; }
    public DateTime ConnectedAt { get; private set; }
    public DateTime? DisconnectedAt { get; private set; }
    public bool IsActive { get; private set; }

    private UserConnection() { }

    public static UserConnection Create(Guid userId, string connectionId, string? deviceType = null)
    {
        return new UserConnection
        {
            UserId = userId,
            ConnectionId = connectionId,
            DeviceType = deviceType,
            ConnectedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    public void Disconnect()
    {
        IsActive = false;
        DisconnectedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Reconnect(string newConnectionId)
    {
        ConnectionId = newConnectionId;
        IsActive = true;
        DisconnectedAt = null;
        SetUpdated();
    }
}
