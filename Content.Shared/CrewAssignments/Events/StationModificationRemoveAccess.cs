using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.Events;

/// <summary>
///     Set order in database as approved.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationModificationRemoveAccess : BoundUserInterfaceMessage
{
    public string Owner;

    public StationModificationRemoveAccess(string owner)
    {
        Owner = owner;
    }
}
