using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.Events;

/// <summary>
///     Set order in database as approved.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationModificationCreateAssignment : BoundUserInterfaceMessage
{
    public string Owner;

    public StationModificationCreateAssignment(string owner)
    {
        Owner = owner;
    }
}
