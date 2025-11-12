using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.Events;

/// <summary>
///     Set order in database as approved.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationModificationToggleAssignmentAccess : BoundUserInterfaceMessage
{
    public int AccessID;
    public bool ToggleState;
    public string Access;

    public StationModificationToggleAssignmentAccess(int id, bool state, string access)
    {
        AccessID = id;
        ToggleState = state;
        Access = access;
    }
}
