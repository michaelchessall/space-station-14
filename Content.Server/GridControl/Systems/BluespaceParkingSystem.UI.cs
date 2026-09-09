using Content.Server.Cargo.Components;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.CCVar;
using Content.Shared.GridControl.Components;
using Content.Shared.GridControl.Systems;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server.GridControl.Systems;

public sealed partial class BluespaceParkingSystem : SharedBluespaceParkingSystem
{
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;

    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly GridConfigSystem _gridConfigSystem = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    private void InitializeUI()
    {
        Subs.BuiEvents<BSPAnchorKeyComponent>(BSPAnchorKeyUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<BSPAnchorKeyStartPark>(OnStartParkRequest);
            subs.Event<BSPAnchorKeyStartUnpark>(OnStartUnparkRequest);
            subs.Event<BSPAnchorKeyCancel>(OnCancelRequest);
            subs.Event<BSPAnchorKeyToggleClearOwnership>(OnToggleClearOwnership);
        });
    }

    private void OnStartParkRequest(EntityUid uid, BSPAnchorKeyComponent component, BSPAnchorKeyStartPark args)
    {
        StartPark((uid, component));
    }

    private void OnStartUnparkRequest(EntityUid uid, BSPAnchorKeyComponent component, BSPAnchorKeyStartUnpark args)
    {
        StartUnpark((uid, component));
    }

    private void OnCancelRequest(EntityUid uid, BSPAnchorKeyComponent component, BSPAnchorKeyCancel args)
    {
        CancelRoutine((uid, component), "User request.");
    }

    private void OnToggleClearOwnership(EntityUid uid, BSPAnchorKeyComponent component, BSPAnchorKeyToggleClearOwnership args)
    {
        ToggleClearOwnership((uid, component));
    }

    private void ToggleClearOwnership(Entity<BSPAnchorKeyComponent> entity)
    {
        if (TerminatingOrDeleted(entity)) return;
        if (entity.Comp.State != BSPState.Parked) return;
        entity.Comp.SavedClearOwnership = !entity.Comp.SavedClearOwnership;
        UpdateUserInterface(entity.Owner, entity.Comp);
    }

    private void UpdateUserInterface(EntityUid uid, BSPAnchorKeyComponent component, EntityEventArgs args)
    {
        UpdateUserInterface(uid, component);
    }

    private BSPAnchorKeyBoundUserInterfaceState? GetUIState(EntityUid uid, BSPAnchorKeyComponent component)
    {
        if (!component.Initialized || TerminatingOrDeleted(uid))
            return null;

        var inFilledState = component.State == BSPState.Parked || component.State == BSPState.Unparking;
        var tileLimit = _cfg.GetCVar(CCVars.BluespaceParkingMaxTiles);

        var gridOwnerTotalTiles = 0;
        EntityUid? station = null;
        if (component.SavedOwnerFaction != null)
        {
            station = _station.GetStationByID(component.SavedOwnerFaction.Value);
            if (station != null)
            {
                gridOwnerTotalTiles = _station.GetStationTileCount(station.Value);
                if (TryComp<StationDataComponent>(station, out var stationData) && stationData != null)
                {
                    _protoMan.Resolve(stationData.Level, out var prototype);
                    if (prototype != null)
                    {
                        tileLimit = prototype.TileLimit;
                    }
                }
            }
        }
        else if (component.SavedOwnerPersonal != null)
        {
            gridOwnerTotalTiles = _station.GetPersonalTileCount(component.SavedOwnerPersonal);
        }


        var privilegedIdName = string.Empty;
        var privilegedName = string.Empty;
        if (component.PrivilegedIdSlot.Item is { Valid: true } idCard)
        {
            privilegedIdName = Name(idCard);
            if (TryComp<IdCardComponent>(idCard, out var id) && id.FullName != null)
            {
                privilegedName = id.FullName;
            }

        }

        var gridName = string.Empty;
        var grid = component.CurrentTarget.HasValue ? component.CurrentTarget : _transform.GetGrid(uid);
        if (inFilledState)
        {
            grid = null;
            gridName = component.SavedGridName;
        }
        else if (grid != null)
        {
            gridName = Name(grid.Value);
        }

        EntityUid? owningStation = null;
        string? ownerName;

        if (!inFilledState)
        {
            _station.GetOwning(grid.GetValueOrDefault(uid), out owningStation, out ownerName);
        }
        else
        {
            if (component.SavedOwnerFaction.HasValue)
                owningStation = _station.GetStationByID(component.SavedOwnerFaction.Value);
            ownerName = component.SavedOwnerPersonal;
        }

        var isAuth = _station.GetGridAccess(grid, privilegedName, owningStation, ownerName, ignoreGrid: inFilledState);

        string? errMsg = null;
        var controlled = false;
        if (grid != null)
        {
            controlled = _gridConfigSystem.IsGridControlled(grid.Value);
        }
        var gridTileCount = 0;
        if (TryComp<MapGridComponent>(grid, out var targetGridComp))
            gridTileCount = _mapSystem.GetAllTiles(grid.Value, targetGridComp).Count();





        if (HasComp<TradeStationComponent>(grid))
            errMsg = "Trade stations cannot be parked.";

        return new BSPAnchorKeyBoundUserInterfaceState(
            component.State,
            isAuth, controlled,
            ownerName, gridName, privilegedIdName,
            gridTileCount, gridOwnerTotalTiles, tileLimit, errMsg,
            component.RoutineStartTime,
            _cfg.GetCVar(CCVars.BluespaceParkingParkDelay), _cfg.GetCVar(CCVars.BluespaceParkingUnparkDelay),
            component.SavedParkedPosition, _cfg.GetCVar(CCVars.BluespaceUnparkMaxDistance), component.SavedClearOwnership
        );
    }

    private void UpdateUserInterface(EntityUid uid, BSPAnchorKeyComponent component)
    {
        UpdateUserInterface(uid, GetUIState(uid, component));
    }
    private void UpdateUserInterface(EntityUid uid, BSPAnchorKeyBoundUserInterfaceState? state)
    {
        if (state != null)
            _userInterface.SetUiState(uid, BSPAnchorKeyUiKey.Key, state);
    }
}
