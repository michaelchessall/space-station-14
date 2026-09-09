using Content.Server.Solar.Components;
using Content.Shared.Solar;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server.Solar.EntitySystems
{
    /// <summary>
    /// Responsible for updating solar control consoles.
    /// </summary>
    [UsedImplicitly]
    internal sealed class PowerSolarControlConsoleSystem : EntitySystem
    {
        [Dependency] private readonly PowerSolarSystem _powerSolarSystem = default!;
        [Dependency] private readonly PowerSolarTrackerSystem _powerSolarTracker = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;

        /// <summary>
        /// Timer used to avoid updating the UI state every frame (which would be overkill)
        /// </summary>
        private float _updateTimer;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SolarControlConsoleComponent, SolarControlConsoleAdjustMessage>(OnUIMessage);
        }

        public override void Update(float frameTime)
        {
            _updateTimer += frameTime;
            if (_updateTimer >= 1)
            {
                _updateTimer -= 1;
                var query = EntityQueryEnumerator<SolarControlConsoleComponent, UserInterfaceComponent>();
                while (query.MoveNext(out var uid, out _, out var uiComp))
                {
                    Angle towardsSun = Angle.Zero;
                    float totalPanelPower = 0;
                    bool hasTracker = false;
                    Angle targetPanelRotation = Angle.Zero;
                    Angle targetPanelVelocity = Angle.Zero;

                    var mapUid = _transform.GetMap(uid);
                    if (mapUid.HasValue && TryComp<SolarLocationComponent>(mapUid, out var solarLocation) && solarLocation != null)
                        towardsSun = solarLocation.TowardsSun;

                    var gridUid = _transform.GetGrid(uid);
                    if (gridUid.HasValue)
                    {
                        totalPanelPower = _powerSolarSystem.GetGridTotalPower(gridUid.Value);
                        var trackerUid = _powerSolarTracker.GetGridTrackerEntity(gridUid.Value);
                        if (trackerUid != null && TryComp<SolarTrackerComponent>(trackerUid, out var trackerComp) && trackerComp != null)
                            hasTracker = true;
                        var panel = _powerSolarSystem.GetGridPanels(gridUid.Value)
                            .FirstOrDefault();
                        if (panel != null)
                        {
                            targetPanelRotation = panel.TargetPanelRotation;
                            targetPanelVelocity = panel.TargetPanelVelocity;
                        }
                    }
                    var state = new SolarControlConsoleBoundInterfaceState(targetPanelRotation, targetPanelVelocity, totalPanelPower, towardsSun, hasTracker);
                    _uiSystem.SetUiState((uid, uiComp), SolarControlConsoleUiKey.Key, state);
                }
            }
        }

        private void OnUIMessage(EntityUid uid, SolarControlConsoleComponent component, SolarControlConsoleAdjustMessage msg)
        {
            var gridUid = _transform.GetGrid(uid).GetValueOrDefault();
            DebugTools.Assert(gridUid != default);

            if (double.IsFinite(msg.Rotation))
                _powerSolarSystem.SetTargetPanelRotation(gridUid, msg.Rotation.Reduced());
            if (double.IsFinite(msg.AngularVelocity))
                _powerSolarSystem.SetTargetPanelVelocityDegrees(gridUid, msg.AngularVelocity.Degrees);
        }

    }
}
