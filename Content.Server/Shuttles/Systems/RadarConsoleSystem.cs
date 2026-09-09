using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using System.Numerics;
using Content.Server.Sectors.Events;
using Content.Server.Sectors.Systems;

namespace Content.Server.Shuttles.Systems;

public sealed class RadarConsoleSystem : SharedRadarConsoleSystem
{
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SectorWeatherSystem _sectorWeather = default!;

    private float _refreshTimer;
    private const float RefreshInterval = 1f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadarConsoleComponent, ComponentStartup>(OnRadarStartup);

        Subs.BuiEvents<RadarConsoleComponent>(RadarConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
        });

        SubscribeLocalEvent<SectorWeatherChangedEvent>(OnSectorWeatherChanged);
    }

    private void UpdateUserInterface(EntityUid uid, RadarConsoleComponent component, BoundUIOpenedEvent args)
    {
        UpdateState(uid, component);
    }

    private void OnRadarStartup(EntityUid uid, RadarConsoleComponent component, ComponentStartup args)
    {
        UpdateState(uid, component);
    }

        private void OnSectorWeatherChanged(SectorWeatherChangedEvent ev)
        {
            var query = EntityQueryEnumerator<RadarConsoleComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                UpdateState(uid, comp);
            }
        }

    protected override void UpdateState(EntityUid uid, RadarConsoleComponent component)
    {
        var xform = Transform(uid);
        var onGrid = xform.ParentUid == xform.GridUid;
        EntityCoordinates? coordinates = onGrid ? xform.Coordinates : null;
        Angle? angle = onGrid ? xform.LocalRotation : null;

        if (component.FollowEntity)
        {
            coordinates = new EntityCoordinates(uid, Vector2.Zero);
            angle = Angle.Zero;
        }

        if (_uiSystem.HasUi(uid, RadarConsoleUiKey.Key))
        {
            NavInterfaceState state;
            var docks = _console.GetAllDocks();

            if (coordinates != null && angle != null)
            {
                state = _console.GetNavState(uid, docks, coordinates.Value, angle.Value);
            }
            else
            {
                state = _console.GetNavState(uid, docks);
            }

            state.RotateWithEntity = !component.FollowEntity;

            _uiSystem.SetUiState(uid, RadarConsoleUiKey.Key, new NavBoundUserInterfaceState(state));
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _refreshTimer += frameTime;
        if (_refreshTimer < RefreshInterval)
            return;

        _refreshTimer -= RefreshInterval;

        var query = EntityQueryEnumerator<RadarConsoleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!_uiSystem.IsUiOpen(uid, RadarConsoleUiKey.Key))
                continue;

            UpdateState(uid, component);
        }
    }
}
