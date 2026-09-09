using Robust.Shared.Serialization;

namespace Content.Shared._Persistence14.Research.RecipeRelay;

/// <summary>
/// Called before a link attempt to allow other systems to cancel the unlink.
/// </summary>
[ByRefEvent]
public sealed partial class RecipeRelayLinkAttemptEvent : CancellableEntityEventArgs
{
    public required Entity<RecipeRelaySourceComponent> Source;
    public required Entity<RecipeRelayReceiverComponent> Receiver;
    public EntityUid? User = null;
    public string CancelMessage = "default-link-fail";
    public bool AllowDisplay = true;
    public string? BlockedTooltip = null;
}

/// <summary>
/// Called before an unlink attempt to allow other systems to cancel the unlink.
/// </summary>
[ByRefEvent]
public sealed partial class RecipeRelayUnlinkAttemptEvent : CancellableEntityEventArgs
{
    public required Entity<RecipeRelaySourceComponent> Source;
    public required Entity<RecipeRelayReceiverComponent> Receiver;
    public EntityUid? User = null;
    public string CancelMessage = "default-unlink-fail";
    public bool AllowDisplay = true;
    public string? BlockedTooltip = null;
}

/// <summary>
/// Called after successfully linking a source to a receiver.
/// </summary>
public sealed partial class RecipeRelayLinkSuccessEvent : EntityEventArgs
{
    public required Entity<RecipeRelaySourceComponent> Source;
    public required Entity<RecipeRelayReceiverComponent> Receiver;
    public EntityUid? User = null;
}

/// <summary>
/// Called after successfully unlinking a source from a receiver.
/// </summary>
public sealed partial class RecipeRelayUnlinkSuccessEvent : EntityEventArgs
{
    public required Entity<RecipeRelaySourceComponent> Source;
    public required Entity<RecipeRelayReceiverComponent> Receiver;
    public EntityUid? User = null;
}

[Serializable, NetSerializable]
public sealed partial class RecipeRelaySourceBoundUserInterfaceState : BoundUserInterfaceState
{
    public List<RecipeRelayReceiverState> Receivers = new();

    public RecipeRelaySourceBoundUserInterfaceState(List<RecipeRelayReceiverState> receivers)
    {
        Receivers = [.. receivers];
    }
}

[Serializable, NetSerializable]
public readonly record struct RecipeRelayReceiverState(NetEntity Entity, string Name, bool Connected, bool CanChangeState, string? BlockedTooltip);

[Serializable, NetSerializable]
public sealed class RecipeRelayToggleReceieverMessage : BoundUserInterfaceMessage
{
    public NetEntity Receiver { get; set; }

    public RecipeRelayToggleReceieverMessage(NetEntity receiver)
    {
        Receiver = receiver;
    }
}

[Serializable, NetSerializable]
public enum RecipeRelaySourceUIKey : byte
{
    Main,
}