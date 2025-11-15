using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CrewAssignments;

[Serializable, NetSerializable]
public enum JobNetUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class JobNetUpdateState : BoundUserInterfaceState
{
    public Dictionary<int, string>? Stations { get; set; }
    public string? AssignmentName;
    public int? Wage;
    public int SelectedStation;
    public TimeSpan? RemainingMinutes;

    public JobNetUpdateState( Dictionary<int, string>? stations, string? assignmentName, int? wage, int selectedstation, TimeSpan? remainingminutes)
    {
        Stations = stations;
        AssignmentName = assignmentName;
        Wage = wage;
        SelectedStation = selectedstation;
        RemainingMinutes = remainingminutes;
    }
}

[Serializable, NetSerializable]
public sealed class JobNetRequestUpdateInterfaceMessage : BoundUserInterfaceMessage
{

}

[Serializable, NetSerializable]
public sealed class JobNetSelectMessage : BoundUserInterfaceMessage
{
    public int ID;
    public JobNetSelectMessage(int id)
    {
        ID = id;
    }
}


