using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.Events;

/// <summary>
///     Set order in database as approved.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationModificationChangeAssignmentName : BoundUserInterfaceMessage
{
    public int AccessID;
    public string Owner;

    public StationModificationChangeAssignmentName(int id, string name)
    {
        AccessID = id;
        Owner = name;
    }
}
