using Content.Shared.CrewAssignments.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Content.Shared.Station;
using Content.Shared.Station.Components;
namespace Content.Shared.CrewAssignments.Systems;

public abstract partial class SharedCrewAssignmentSystem : EntitySystem
{
    [Dependency] private readonly SharedStationSystem _station = default!;
    public override void Initialize()
    {
        base.Initialize();

    }
    public CrewAssignmentsComponent? GetCrewAssignmentsComponent(EntityUid stationId)
    {
        var target = _station.GetOwningStation(stationId);
        if (target == null) return null;

        if (!EntityManager.TryGetComponent<CrewAssignmentsComponent>(target, out var crewComp))
        {
            return null;
        }

        return crewComp;
    }
    public void RemoveOwner(StationDataComponent comp, string owner)
    {
        comp.AddOwner(owner);
    }
}


[NetSerializable, Serializable]
public enum StationModUiKey : byte
{
    StationMod
}
