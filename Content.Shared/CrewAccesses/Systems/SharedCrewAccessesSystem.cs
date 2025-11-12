using Content.Shared.CrewAccesses.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Content.Shared.Station;
namespace Content.Shared.CrewAccesses.Systems;

public abstract partial class SharedCrewAccessesystem : EntitySystem
{
    [Dependency] private readonly SharedStationSystem _station = default!;
    public override void Initialize()
    {
        base.Initialize();

    }

    public CrewAccessesComponent? GetCrewAccessesComponent(EntityUid stationId)
    {
        var target = _station.GetOwningStation(stationId);
        if (target == null) return null;

        if (!EntityManager.TryGetComponent<CrewAccessesComponent>(target, out var crewComp))
        {
            return null;
        }

        return crewComp;
    }


}

