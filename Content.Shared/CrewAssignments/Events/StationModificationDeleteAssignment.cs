using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.Events;

/// <summary>
///     Set order in database as approved.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationModificationDeleteAssignment : BoundUserInterfaceMessage
{
    public int AccessID;

    public StationModificationDeleteAssignment(int id)
    {
        AccessID = id;
    }
}
