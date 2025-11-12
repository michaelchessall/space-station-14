using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.Events;

/// <summary>
///     Set order in database as approved.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationModificationRemoveOwner : BoundUserInterfaceMessage
{
    public string Owner;

    public StationModificationRemoveOwner(string owner)
    {
        Owner = owner;
    }
}
