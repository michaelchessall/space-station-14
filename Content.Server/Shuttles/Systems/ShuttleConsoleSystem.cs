using System.Numerics;
using Content.Server.Power.EntitySystems;
using Content.Server.Sectors.Components;
using Content.Server.Sectors.Events;
using Content.Server.Sectors.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Station.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Alert;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Singularity.Components;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Events;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Shuttles.UI.MapObjects;
using Content.Shared.Tag;
using Content.Shared.Timing;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Collections;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleConsoleSystem : SharedShuttleConsoleSystem
{
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly AlertsSystem _alertsSystem = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedContentEyeSystem _eyeSystem = default!;
    [Dependency] private readonly SectorWeatherSystem _sectorWeather = default!;

    private EntityQuery<MetaDataComponent> _metaQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private readonly HashSet<Entity<ShuttleConsoleComponent>> _consoles = new();
    private float _navRefreshTimer;

    private const float NavRefreshInterval = 1f;

    private static readonly ProtoId<TagPrototype> CanPilotTag = "CanPilot";

    public override void Initialize()
    {
        base.Initialize();

        _metaQuery = GetEntityQuery<MetaDataComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<ShuttleConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
        SubscribeLocalEvent<ShuttleConsoleComponent, PowerChangedEvent>(OnConsolePowerChange);
        SubscribeLocalEvent<ShuttleConsoleComponent, AnchorStateChangedEvent>(OnConsoleAnchorChange);
        SubscribeLocalEvent<ShuttleConsoleComponent, AfterActivatableUIOpenEvent>(OnConsoleUIOpenAttempt);
        Subs.BuiEvents<ShuttleConsoleComponent>(ShuttleConsoleUiKey.Key, subs =>
        {
            subs.Event<ShuttleConsoleFTLBeaconMessage>(OnBeaconFTLMessage);
            subs.Event<ShuttleConsoleFTLPositionMessage>(OnPositionFTLMessage);
            subs.Event<ShuttleConsoleDampingMessage>(OnDampingMessage);
            subs.Event<ShuttleConsoleWaypointMessage>(OnWaypointMessage);
            subs.Event<BoundUIClosedEvent>(OnConsoleUIClose);
            subs.Event<BoundUIOpenedEvent>(OnConsoleUIOpen);
        });

        SubscribeLocalEvent<DroneConsoleComponent, ConsoleShuttleEvent>(OnCargoGetConsole);
        SubscribeLocalEvent<DroneConsoleComponent, AfterActivatableUIOpenEvent>(OnDronePilotConsoleOpen);
        Subs.BuiEvents<DroneConsoleComponent>(ShuttleConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIClosedEvent>(OnDronePilotConsoleClose);
        });

        SubscribeLocalEvent<DockEvent>(OnDock);
        SubscribeLocalEvent<UndockEvent>(OnUndock);
        SubscribeLocalEvent<SectorWeatherChangedEvent>(OnSectorWeatherChanged);

        SubscribeLocalEvent<PilotComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<PilotComponent, StopPilotingAlertEvent>(OnStopPilotingAlert);

        SubscribeLocalEvent<FTLDestinationComponent, ComponentStartup>(OnFtlDestStartup);
        SubscribeLocalEvent<FTLDestinationComponent, ComponentShutdown>(OnFtlDestShutdown);

        InitializeFTL();
    }

    private void OnFtlDestStartup(EntityUid uid, FTLDestinationComponent component, ComponentStartup args)
    {
        RefreshShuttleConsoles();
    }

    private void OnFtlDestShutdown(EntityUid uid, FTLDestinationComponent component, ComponentShutdown args)
    {
        RefreshShuttleConsoles();
    }

    private void OnDock(DockEvent ev)
    {
        RefreshShuttleConsoles();
    }

    private void OnUndock(UndockEvent ev)
    {
        RefreshShuttleConsoles();
    }

    private void OnSectorWeatherChanged(SectorWeatherChangedEvent ev)
    {
        RefreshShuttleConsoles();
    }

    /// <summary>
    /// Refreshes all the shuttle console data for a particular grid.
    /// </summary>
    public void RefreshShuttleConsoles(EntityUid gridUid)
    {
        var exclusions = new List<ShuttleExclusionObject>();
        GetExclusions(ref exclusions);
        _consoles.Clear();
        _lookup.GetChildEntities(gridUid, _consoles);
        DockingInterfaceState? dockState = null;

        foreach (var entity in _consoles)
        {
            UpdateState(entity, ref dockState);
        }
    }

    /// <summary>
    /// Refreshes all of the data for shuttle consoles.
    /// </summary>
    public void RefreshShuttleConsoles()
    {
        var exclusions = new List<ShuttleExclusionObject>();
        GetExclusions(ref exclusions);
        var query = AllEntityQuery<ShuttleConsoleComponent>();
        DockingInterfaceState? dockState = null;

        while (query.MoveNext(out var uid, out _))
        {
            UpdateState(uid, ref dockState);
        }
    }

    /// <summary>
    /// Stop piloting if the window is closed.
    /// </summary>
    private void OnConsoleUIClose(EntityUid uid, ShuttleConsoleComponent component, BoundUIClosedEvent args)
    {
        if ((ShuttleConsoleUiKey)args.UiKey != ShuttleConsoleUiKey.Key)
        {
            return;
        }

        RemovePilot(args.Actor);
    }

    private void OnConsoleUIOpen(EntityUid uid, ShuttleConsoleComponent component, BoundUIOpenedEvent args)
    {
        DockingInterfaceState? dock = null;
        UpdateState(uid, ref dock);
    }

    private void OnConsoleUIOpenAttempt(EntityUid uid, ShuttleConsoleComponent component,
        AfterActivatableUIOpenEvent args)
    {
        TryPilot(args.User, uid);
    }

    private void OnConsoleAnchorChange(EntityUid uid, ShuttleConsoleComponent component,
        ref AnchorStateChangedEvent args)
    {
        DockingInterfaceState? dockState = null;
        UpdateState(uid, ref dockState);
    }

    private void OnConsolePowerChange(EntityUid uid, ShuttleConsoleComponent component, ref PowerChangedEvent args)
    {
        DockingInterfaceState? dockState = null;
        UpdateState(uid, ref dockState);
    }

    private bool TryPilot(EntityUid user, EntityUid uid)
    {
        if (!_tags.HasTag(user, CanPilotTag) ||
            !TryComp<ShuttleConsoleComponent>(uid, out var component) ||
            !this.IsPowered(uid, EntityManager) ||
            !Transform(uid).Anchored ||
            !_blocker.CanInteract(user, uid))
        {
            return false;
        }

        var pilotComponent = EnsureComp<PilotComponent>(user);
        var console = pilotComponent.Console;

        if (console != null)
        {
            RemovePilot(user, pilotComponent);

            // This feels backwards; is this intended to be a toggle?
            if (console == uid)
                return false;
        }

        AddPilot(uid, user, component);
        return true;
    }

    private void OnGetState(EntityUid uid, PilotComponent component, ref ComponentGetState args)
    {
        args.State = new PilotComponentState(GetNetEntity(component.Console));
    }

    private void OnStopPilotingAlert(Entity<PilotComponent> ent, ref StopPilotingAlertEvent args)
    {
        if (ent.Comp.Console != null)
        {
            RemovePilot(ent, ent);
        }
    }

    /// <summary>
    /// Returns the position and angle of all dockingcomponents.
    /// </summary>
    public Dictionary<NetEntity, List<DockingPortState>> GetAllDocks()
    {
        // TODO: NEED TO MAKE SURE THIS UPDATES ON ANCHORING CHANGES!
        var result = new Dictionary<NetEntity, List<DockingPortState>>();
        var query = AllEntityQuery<DockingComponent, TransformComponent, MetaDataComponent>();

        while (query.MoveNext(out var uid, out var comp, out var xform, out var metadata))
        {
            if (xform.ParentUid != xform.GridUid)
                continue;

            var gridDocks = result.GetOrNew(GetNetEntity(xform.GridUid.Value));

            var state = new DockingPortState()
            {
                Name = metadata.EntityName,
                Coordinates = GetNetCoordinates(xform.Coordinates),
                Angle = xform.LocalRotation,
                Entity = GetNetEntity(uid),
                GridDockedWith =
                    _xformQuery.TryGetComponent(comp.DockedWith, out var otherDockXform) ?
                    GetNetEntity(otherDockXform.GridUid) :
                    null,
                Color = comp.RadarColor,
                HighlightedColor = comp.HighlightedRadarColor
            };

            gridDocks.Add(state);
        }

        return result;
    }

    private void UpdateState(EntityUid consoleUid, ref DockingInterfaceState? dockState)
    {
        EntityUid? entity = consoleUid;

        var getShuttleEv = new ConsoleShuttleEvent
        {
            Console = entity,
        };

        RaiseLocalEvent(entity.Value, ref getShuttleEv);
        entity = getShuttleEv.Console;

        TryComp(entity, out TransformComponent? consoleXform);
        var shuttleGridUid = consoleXform?.GridUid;

        NavInterfaceState navState;
        ShuttleMapInterfaceState mapState;
        dockState ??= GetDockState();

        if (shuttleGridUid != null && entity != null)
        {
            navState = GetNavState(entity.Value, dockState.Docks);
            mapState = GetMapState(shuttleGridUid.Value, entity.Value);
        }
        else
        {
            navState = new NavInterfaceState(
                0f,
                null,
                null,
                new Dictionary<NetEntity, List<DockingPortState>>(),
                ShuttleDampingMode.Normal,
                _sectorWeather.GetHazardWeatherSnapshot());
            mapState = new ShuttleMapInterfaceState(
                FTLState.Invalid,
                default,
                new List<ShuttleBeaconObject>(),
                new List<ShuttleExclusionObject>());
        }

        if (_ui.HasUi(consoleUid, ShuttleConsoleUiKey.Key))
        {
            _ui.SetUiState(consoleUid, ShuttleConsoleUiKey.Key, new ShuttleBoundUserInterfaceState(navState, mapState, dockState));
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _navRefreshTimer += frameTime;
        if (_navRefreshTimer >= NavRefreshInterval)
        {
            _navRefreshTimer -= NavRefreshInterval;

            DockingInterfaceState? dock = null;
            var consoleQuery = EntityQueryEnumerator<ShuttleConsoleComponent>();
            while (consoleQuery.MoveNext(out var consoleUid, out _))
            {
                if (!_ui.IsUiOpen(consoleUid, ShuttleConsoleUiKey.Key))
                    continue;

                UpdateState(consoleUid, ref dock);
            }
        }

        var toRemove = new ValueList<(EntityUid, PilotComponent)>();
        var query = EntityQueryEnumerator<PilotComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Console == null)
                continue;

            if (!_blocker.CanInteract(uid, comp.Console))
            {
                toRemove.Add((uid, comp));
            }
        }

        foreach (var (uid, comp) in toRemove)
        {
            RemovePilot(uid, comp);
        }
    }

    protected override void HandlePilotShutdown(EntityUid uid, PilotComponent component, ComponentShutdown args)
    {
        base.HandlePilotShutdown(uid, component, args);
        RemovePilot(uid, component);
    }

    private void OnConsoleShutdown(EntityUid uid, ShuttleConsoleComponent component, ComponentShutdown args)
    {
        ClearPilots(component);
    }

    public void AddPilot(EntityUid uid, EntityUid entity, ShuttleConsoleComponent component)
    {
        if (!TryComp(entity, out PilotComponent? pilotComponent)
        || component.SubscribedPilots.Contains(entity))
        {
            return;
        }

        _eyeSystem.SetZoom(entity, component.Zoom, ignoreLimits: true);

        component.SubscribedPilots.Add(entity);

        _alertsSystem.ShowAlert(entity, pilotComponent.PilotingAlert);

        pilotComponent.Console = uid;
        ActionBlockerSystem.UpdateCanMove(entity);
        pilotComponent.Position = Comp<TransformComponent>(entity).Coordinates;
        Dirty(entity, pilotComponent);
    }

    public void RemovePilot(EntityUid pilotUid, PilotComponent pilotComponent)
    {
        var console = pilotComponent.Console;

        if (!TryComp<ShuttleConsoleComponent>(console, out var helm))
            return;

        pilotComponent.Console = null;
        pilotComponent.Position = null;
        _eyeSystem.ResetZoom(pilotUid);

        if (!helm.SubscribedPilots.Remove(pilotUid))
            return;

        _alertsSystem.ClearAlert(pilotUid, pilotComponent.PilotingAlert);

        _popup.PopupEntity(Loc.GetString("shuttle-pilot-end"), pilotUid, pilotUid);

        if (pilotComponent.LifeStage < ComponentLifeStage.Stopping)
            RemComp<PilotComponent>(pilotUid);
    }

    public void RemovePilot(EntityUid entity)
    {
        if (!TryComp(entity, out PilotComponent? pilotComponent))
            return;

        RemovePilot(entity, pilotComponent);
    }

    public void ClearPilots(ShuttleConsoleComponent component)
    {
        var query = GetEntityQuery<PilotComponent>();
        while (component.SubscribedPilots.TryGetValue(0, out var pilot))
        {
            if (query.TryGetComponent(pilot, out var pilotComponent))
                RemovePilot(pilot, pilotComponent);
        }
    }

    /// <summary>
    /// Specific for a particular shuttle.
    /// </summary>
    public NavInterfaceState GetNavState(Entity<RadarConsoleComponent?, TransformComponent?> entity, Dictionary<NetEntity, List<DockingPortState>> docks)
    {
        var damping = ShuttleDampingMode.Normal;
        Vector2? waypoint = null;

        if (entity.Comp2?.GridUid is { } gridUid && TryComp<ShuttleComponent>(gridUid, out var shuttle))
        {
            damping = shuttle.DampingMode;
        }

        if (TryComp<ShuttleConsoleComponent>(entity, out var console))
        {
            waypoint = console.Waypoint;
        }

        if (!Resolve(entity, ref entity.Comp1, ref entity.Comp2))
            return new NavInterfaceState(entity.Comp1!.MaxRange, null, null, docks, damping, _sectorWeather.GetHazardWeatherSnapshot(), null, waypoint);

        return GetNavState(
            entity,
            docks,
            entity.Comp2.Coordinates,
            entity.Comp2.LocalRotation);
    }

    public NavInterfaceState GetNavState(
        Entity<RadarConsoleComponent?, TransformComponent?> entity,
        Dictionary<NetEntity, List<DockingPortState>> docks,
        EntityCoordinates coordinates,
        Angle angle)
    {
        var damping = ShuttleDampingMode.Normal;
        Vector2? waypoint = null;

        if (entity.Comp2?.GridUid is { } gridUid && TryComp<ShuttleComponent>(gridUid, out var shuttle))
        {
            damping = shuttle.DampingMode;
        }

        if (TryComp<ShuttleConsoleComponent>(entity, out var console))
        {
            waypoint = console.Waypoint;
        }

        if (!Resolve(entity, ref entity.Comp1, ref entity.Comp2))
            return new NavInterfaceState(entity.Comp1!.MaxRange, GetNetCoordinates(coordinates), angle, docks, damping, _sectorWeather.GetHazardWeatherSnapshot());

        var trackedEntities = GetTrackedEntities(coordinates);

        return new NavInterfaceState(
            entity.Comp1.MaxRange,
            GetNetCoordinates(coordinates),
            angle,
            docks,
            damping,
            _sectorWeather.GetHazardWeatherSnapshot(),
            trackedEntities,
            waypoint);
    }

    private List<NavTrackedEntityState> GetTrackedEntities(EntityCoordinates radarCoordinates)
    {
        var tracked = new List<NavTrackedEntityState>();
        var radarMapId = _transform.ToMapCoordinates(radarCoordinates).MapId;

        if (radarMapId == MapId.Nullspace)
            return tracked;

        var query = EntityQueryEnumerator<NavScreenTrackableComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var marker, out var xform, out var meta))
        {
            if (xform.MapID != radarMapId)
                continue;

            if (xform.MapID == MapId.Nullspace)
                continue;

            var label = string.IsNullOrWhiteSpace(marker.Label) ? meta.EntityName : marker.Label;

            var markerSize = marker.MarkerSize;
            if (TryComp<SingularityComponent>(uid, out var singularity))
            {
                markerSize += Math.Max(0, singularity.Level - 1) * marker.SingularityLevelSizeGrowth;
            }
            else if (TryComp<WhiteHoleComponent>(uid, out var whiteHole) &&
                     whiteHole.LinkedSingularity is { } linkedSingularityUid &&
                     TryComp<SingularityComponent>(linkedSingularityUid, out var linkedSingularity))
            {
                markerSize += Math.Max(0, linkedSingularity.Level - 1) * marker.SingularityLevelSizeGrowth;
            }

            NetCoordinates? spawnCoords = null;
            if (marker.TrackerType == NavScreenTrackerType.SpawnTracked &&
                TryComp<SectorWeatherSpawnedComponent>(uid, out var spawned))
            {
                var spawnMapCoordinates = new MapCoordinates(spawned.SpawnWorldPosition, radarMapId);
                var spawnCoordinates = _transform.ToCoordinates(_mapSystem.GetMap(radarMapId), spawnMapCoordinates);
                spawnCoords = GetNetCoordinates(spawnCoordinates);
            }

            tracked.Add(new NavTrackedEntityState(
                GetNetEntity(uid),
                GetNetCoordinates(xform.Coordinates),
                marker.Color,
                label,
                marker.ShowLabel,
                marker.TrackerType,
                spawnCoords,
                markerSize));
        }

        return tracked;
    }

    /// <summary>
    /// Global for all shuttles.
    /// </summary>
    /// <returns></returns>
    public DockingInterfaceState GetDockState()
    {
        var docks = GetAllDocks();
        return new DockingInterfaceState(docks);
    }

    /// <summary>
    /// Specific to a particular shuttle.
    /// </summary>
    public ShuttleMapInterfaceState GetMapState(Entity<FTLComponent?> shuttle, Entity<ShuttleConsoleComponent?> console)
    {
        FTLState ftlState = FTLState.Available;
        StartEndTime stateDuration = default;
        Vector2? waypoint = null;

        if (Resolve(shuttle, ref shuttle.Comp, false) && shuttle.Comp.LifeStage < ComponentLifeStage.Stopped)
        {
            ftlState = shuttle.Comp.State;
            stateDuration = _shuttle.GetStateTime(shuttle.Comp);
        }

        if (Resolve(console, ref console.Comp, false))
        {
            waypoint = console.Comp.Waypoint;
        }

        List<ShuttleBeaconObject>? beacons = null;
        List<ShuttleExclusionObject>? exclusions = null;
        GetBeacons(ref beacons);
        GetExclusions(ref exclusions);

        return new ShuttleMapInterfaceState(
            ftlState,
            stateDuration,
            beacons ?? new List<ShuttleBeaconObject>(),
            exclusions ?? new List<ShuttleExclusionObject>(),
            _sectorWeather.GetHazardWeatherSnapshot(),
            waypoint);
    }

    private void OnDampingMessage(Entity<ShuttleConsoleComponent> ent, ref ShuttleConsoleDampingMessage args)
    {
        var xform = _xformQuery.GetComponent(ent);
        if (xform.GridUid is not { } gridUid)
            return;

        _shuttle.SetDampingMode(gridUid, args.DampingMode);
        RefreshShuttleConsoles(gridUid);
    }

    private void OnWaypointMessage(Entity<ShuttleConsoleComponent> ent, ref ShuttleConsoleWaypointMessage args)
    {
        ent.Comp.Waypoint = args.Coords;

        DockingInterfaceState? dockState = null;
        UpdateState(ent, ref dockState);
    }
}
