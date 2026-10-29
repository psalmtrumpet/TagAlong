using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Services;

public static class PickupPins
{
    /// <summary>Every passenger waiting on one trip gets a different PIN.</summary>
    public static async Task MakeUniqueOnTripAsync(IConversationRepository repo, Conversation conversation, CancellationToken ct)
    {
        if (conversation.TripId is not { } tripId || conversation.PickupPin is null) return;
        var taken = (await repo.GetUnstartedByTripIdAsync(tripId, ct))
            .Where(c => c.Id != conversation.Id && c.PickupPin != null)
            .Select(c => c.PickupPin!)
            .ToHashSet();
        for (var i = 0; i < 50 && taken.Contains(conversation.PickupPin!); i++)
            conversation.IssuePickupPin();
    }

    /// <summary>What the driver is told when the PIN they typed doesn't start the ride.</summary>
    public static string Problem(PinCheck check, Conversation c, DateTime now) => check switch
    {
        PinCheck.Locked =>
            $"Too many wrong PINs. Try again in {Math.Max(1, (int)Math.Ceiling(((c.PinLockedUntil ?? now) - now).TotalMinutes))} min.",
        _ =>
            $"That PIN doesn't match. Ask your passenger for the PIN on their screen ({Conversation.MaxPinFailures - c.PinFailures} tries left).",
    };
}
