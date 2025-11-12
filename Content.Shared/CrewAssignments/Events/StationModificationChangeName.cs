using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.Events;

/// <summary>
///     Set order in database as approved.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationModificationAddAccess : BoundUserInterfaceMessage
{
    public string Owner;

    public StationModificationAddAccess(string owner)
    {
        Owner = owner;
    }
}
