using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.Events;

/// <summary>
///     Set order in database as approved.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationModificationChangeAssignmentCLevel : BoundUserInterfaceMessage
{
    public int AccessID;
    public int Level;

    public StationModificationChangeAssignmentCLevel(int id, int clevel)
    {
        AccessID = id;
        Level = clevel;
    }
}
