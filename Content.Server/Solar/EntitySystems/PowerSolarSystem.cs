using Content.Server.Power.Components;
using Content.Server.Solar.Components;
using Content.Shared.Physics;
using JetBrains.Annotations;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using System.Linq;

namespace Content.Server.Solar.EntitySystems
{
    /// <summary>
    ///     Responsible for maintaining the solar-panel sun angle and updating <see cref='SolarPanelComponent'/> coverage.
    /// </summary>
    [UsedImplicitly]
    internal sealed class PowerSolarSystem : EntitySystem
    {
        [Dependency] private readonly SharedPhysicsSystem _physicsSystem = default!;
        [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
        [Dependency] private readonly SolarPositioningSystem _solarPositioning = default!;

        /// <summary>
        /// Maximum panel angular velocity range - used to stop people rotating panels fast enough that the lag prevention becomes noticable
        /// </summary>
        public const float MaxPanelVelocityDegrees = 1f;


        /// <summary>
        /// The distance before the sun is considered to have been 'visible anyway'.
        /// This value, like the occlusion semantics, is borrowed from all the other SS13 stations with solars.
        /// </summary>
        public const float SunOcclusionCheckDistance = 20;

        public Dictionary<EntityUid, float> TotalPanelPowerByGrid = new();

        /// <summary>
        /// Used to lookup all panels on a single grid without constant lookup.
        /// This gets updated when solar panels update so it doesnt add any extra overhead.
        /// </summary>
        public Dictionary<EntityUid, HashSet<EntityUid>> PanelsByGrid = new();

        /// <summary>
        /// Queue of panels to update each cycle.
        /// </summary>
        private readonly Queue<Entity<SolarPanelComponent>> _updateQueue = new();

        public override void Initialize()
        {
            SubscribeLocalEvent<SolarPanelComponent, MapInitEvent>(OnMapInit);
            SubscribeLocalEvent<SolarPanelComponent, AnchorStateChangedEvent>(OnAnchorStateChange);
        }

        private void OnMapInit(EntityUid uid, SolarPanelComponent component, MapInitEvent args)
        {
            UpdateSupply(uid, component);
        }

        private void OnAnchorStateChange(Entity<SolarPanelComponent> ent, ref AnchorStateChangedEvent args)
        {
            if (args.Anchored)
            {
                var gridUid = _transformSystem.GetGrid(ent.Owner);
                if (gridUid.HasValue)
                {
                    SyncPanelToExisting(gridUid.Value, ent.Owner, ent.Comp);
                }
            }
        }

        public override void Update(float frameTime)
        {
            var processingUpdateQueue = _updateQueue.Count > 0;
            if (processingUpdateQueue)
            {
                var panel = _updateQueue.Dequeue();
                if (panel.Comp.Running)
                    UpdatePanelCoverage(panel);
            }

            if (!processingUpdateQueue)
            {
                TotalPanelPowerByGrid.Clear();
                PanelsByGrid.Clear();
            }

            var query = EntityQueryEnumerator<SolarPanelComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var panel, out var xform))
            {
                // Panels in space... How do you even wire them?
                if (xform.GridUid == null) continue;
                if (!xform.Anchored) continue;

                var gridUid = xform.GridUid.Value;

                panel.TargetPanelRotation += panel.TargetPanelVelocity * frameTime;
                panel.TargetPanelRotation = panel.TargetPanelRotation.Reduced();

                if (processingUpdateQueue) continue;

                if (!TotalPanelPowerByGrid.ContainsKey(gridUid))
                    TotalPanelPowerByGrid[gridUid] = 0;
                TotalPanelPowerByGrid[gridUid] += panel.MaxSupply * panel.Coverage;

                if (!PanelsByGrid.ContainsKey(gridUid))
                    PanelsByGrid[gridUid] = [];
                PanelsByGrid[gridUid].Add(uid);

                _transformSystem.SetWorldRotation(xform, panel.TargetPanelRotation);
                _updateQueue.Enqueue((uid, panel));
            }
        }

        private void UpdatePanelCoverage(Entity<SolarPanelComponent> panel)
        {
            var entity = panel.Owner;
            var xform = Comp<TransformComponent>(entity);

            var solarLocation = _solarPositioning.GetSolarLocation(entity);
            var towardsSun = solarLocation?.TowardsSun ?? Angle.Zero;

            // So apparently, and yes, I *did* only find this out later,
            // this is just a really fancy way of saying "Lambert's law of cosines".
            // ...I still think this explaination makes more sense.

            // In the 'sunRelative' coordinate system:
            // the sun is considered to be an infinite distance directly up.
            // this is the rotation of the panel relative to that.
            // directly upwards (theta = 0) = coverage 1
            // left/right 90 degrees (abs(theta) = (pi / 2)) = coverage 0
            // directly downwards (abs(theta) = pi) = coverage -1
            // as TowardsSun + = CCW,
            // panelRelativeToSun should - = CW
            var panelRelativeToSun = _transformSystem.GetWorldRotation(xform) - towardsSun;
            // essentially, given cos = X & sin = Y & Y is 'downwards',
            // then for the first 90 degrees of rotation in either direction,
            // this plots the lower-right quadrant of a circle.
            // now basically assume a line going from the negated X/Y to there,
            // and that's the hypothetical solar panel.
            //
            // since, again, the sun is considered to be an infinite distance upwards,
            // this essentially means Cos(panelRelativeToSun) is half of the cross-section,
            // and since the full cross-section has a max of 2, effectively-halving it is fine.
            //
            // as for when it goes negative, it only does that when (abs(theta) > pi)
            // and that's expected behavior.
            float coverage = solarLocation != null ? (float)Math.Max(0, Math.Cos(panelRelativeToSun)) : 0;

            if (coverage > 0)
            {
                // Determine if the solar panel is occluded, and zero out coverage if so.
                var ray = new CollisionRay(_transformSystem.GetWorldPosition(xform), towardsSun.ToWorldVec(), (int)CollisionGroup.Opaque);
                var rayCastResults = _physicsSystem.IntersectRayWithPredicate(
                    xform.MapID,
                    ray,
                    SunOcclusionCheckDistance,
                    e => !xform.Anchored || e == entity);
                if (rayCastResults.Any())
                    coverage = 0;
            }

            // Total coverage calculated; apply it to the panel.
            panel.Comp.Coverage = coverage;
            UpdateSupply(panel, panel);
        }

        public void UpdateSupply(
            EntityUid uid,
            SolarPanelComponent? solar = null,
            PowerSupplierComponent? supplier = null)
        {
            if (!Resolve(uid, ref solar, ref supplier, false))
                return;

            supplier.MaxSupply = (int)(solar.MaxSupply * solar.Coverage);
        }

        private void SyncPanelToExisting(EntityUid gridUid, EntityUid owner, SolarPanelComponent comp)
        {
            var otherPanel = GetGridPanelEntities(gridUid)
                .Select(x =>
                {
                    if (TryComp<SolarPanelComponent>(x, out var otherPanel))
                        return otherPanel;
                    return null;
                }).FirstOrDefault();
            if (otherPanel != null)
            {
                comp.TargetPanelRotation = otherPanel.TargetPanelRotation;
                comp.TargetPanelVelocity = otherPanel.TargetPanelVelocity;
            }
        }

        public IEnumerable<EntityUid> GetGridPanelEntities(EntityUid gridUid)
        {
            if (PanelsByGrid.TryGetValue(gridUid, out var panelEntities))
                return panelEntities.AsEnumerable();
            return [];
        }

        public IEnumerable<SolarPanelComponent> GetGridPanels(EntityUid gridUid)
        {
            return GetGridPanelEntities(gridUid)
                .Select(x =>
                {
                    if (TryComp<SolarPanelComponent>(x, out var comp))
                        return comp;
                    return null;
                })
                .Where(x => x != null)
                .OfType<SolarPanelComponent>();
        }

        internal float GetGridTotalPower(EntityUid gridUid)
        {
            if (TotalPanelPowerByGrid.TryGetValue(gridUid, out var totalPanelPower))
                return totalPanelPower;
            return 0;
        }

        public void SetTargetPanelRotation(EntityUid gridUid, Angle angle)
        {
            foreach (var panel in GetGridPanels(gridUid))
                panel.TargetPanelRotation = angle;
        }

        public void SetTargetPanelVelocity(EntityUid gridUid, Angle angle)
        {
            foreach (var panel in GetGridPanels(gridUid))
                panel.TargetPanelVelocity = angle;
        }

        public void SetTargetPanelVelocityDegrees(EntityUid gridUid, double degrees)
        {
            degrees = Math.Clamp(degrees, -MaxPanelVelocityDegrees, MaxPanelVelocityDegrees);
            SetTargetPanelVelocity(gridUid, Angle.FromDegrees(degrees));
        }
    }
}
