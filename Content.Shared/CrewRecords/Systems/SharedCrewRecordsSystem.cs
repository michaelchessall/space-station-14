using Content.Shared.CrewRecords.Components;
using Content.Shared.Station;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
namespace Content.Shared.CrewRecords.Systems;

public abstract partial class SharedCrewRecordSystem : EntitySystem
{
    [Dependency] private readonly SharedStationSystem _station = default!;
    public override void Initialize()
    {
        base.Initialize();

    }

    public CrewRecordsComponent? GetCrewRecordsComponent(EntityUid stationId)
    {
        var target = _station.GetOwningStation(stationId);
        if (target == null) return null;

        if (!EntityManager.TryGetComponent<CrewRecordsComponent>(target, out var crewComp))
        {
            return null;
        }

        return crewComp;
    }

}

