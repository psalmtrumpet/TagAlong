namespace TagAlong.Notification.Domain.Entities;

public class DeviceToken
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = null!;
    public DateTime UpdatedAt { get; private set; }

    private DeviceToken() { }

    public static DeviceToken Create(Guid userId, string token) => new()
    {
        UserId = userId,
        Token = token,
        UpdatedAt = DateTime.UtcNow
    };

    public void Update(string token)
    {
        Token = token;
        UpdatedAt = DateTime.UtcNow;
    }
}
