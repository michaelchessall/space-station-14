using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.Events;

/// <summary>
///     Set order in database as approved.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationModificationChangeAssignmentWage : BoundUserInterfaceMessage
{
    public int AccessID;
    public int Wage;

    public StationModificationChangeAssignmentWage(int id, int clevel)
    {
        AccessID = id;
        Wage = clevel;
    }
}
