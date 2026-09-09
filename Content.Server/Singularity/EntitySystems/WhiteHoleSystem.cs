using Content.Server.Singularity.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mind.Components;
using Content.Shared.Physics;
using Content.Shared.Radiation.Components;
using Content.Shared.Singularity;
using Content.Shared.Singularity.Components;
using Content.Shared.Singularity.Events;
using Content.Shared.Singularity.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server.Singularity.EntitySystems;

/// <summary>
/// Manages white hole entities that are linked to singularities.
/// White holes eject everything the singularity consumes at the opposite spatial coordinates.
/// </summary>
public sealed class WhiteHoleSystem : EntitySystem
{
    private const float MindTransitBluntDamage = 100f;
    private const float WhiteHoleRadiationScale = 0.75f;
    private static readonly TimeSpan LinkValidationInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextLinkValidation;

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WhiteHoleComponent, ComponentShutdown>(OnWhiteHoleShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Keep white holes opposite their linked singularities and mirror their visual level.
        var wholeQuery = EntityQueryEnumerator<WhiteHoleComponent, TransformComponent>();
        while (wholeQuery.MoveNext(out var wholeUid, out var hole, out var wholeXform))
        {
            if (hole.LinkedSingularity is not { Valid: true })
            {
                QueueDel(wholeUid);
                continue;
            }

            if (!_entityManager.TryGetComponent<TransformComponent>(hole.LinkedSingularity.Value, out _))
            {
                QueueDel(wholeUid);
                continue;
            }

            var singularityUid = hole.LinkedSingularity.Value;
            var singMapCoords = _transformSystem.GetMapCoordinates(singularityUid);
            if (singMapCoords.MapId == MapId.Nullspace)
                continue;

            if (hole.FollowSingularity)
            {
                var oppositeCoords = new MapCoordinates(-singMapCoords.Position, singMapCoords.MapId);
                _transformSystem.SetMapCoordinates(wholeUid, oppositeCoords);
            }

            if (TryComp<SingularityComponent>(singularityUid, out var singularity))
            {
                if (TryComp<AppearanceComponent>(wholeUid, out var appearance))
                    _appearance.SetData(wholeUid, SingularityAppearanceKeys.Singularity, singularity.Level, appearance);

                if (TryComp<RadiationSourceComponent>(wholeUid, out var radiationSource))
                    radiationSource.Intensity = singularity.Level * singularity.RadsPerLevel * WhiteHoleRadiationScale;

                if (TryComp<GravityWellComponent>(wholeUid, out var gravityWell))
                {
                    gravityWell.MaxRange = SharedSingularitySystem.BaseGravityWellRadius * (singularity.Level + 1);
                    gravityWell.BaseRadialAcceleration = -SharedSingularitySystem.BaseGravityWellAcceleration * singularity.Level;
                    gravityWell.BaseTangentialAcceleration = 0f;
                }

                if (HasComp<SingularityDistortionComponent>(wholeUid))
                    RaiseLocalEvent(wholeUid, new SingularityLevelChangedEvent(singularity.Level, singularity.Level, singularity));
            }
        }

        if (_timing.CurTime < _nextLinkValidation)
            return;

        _nextLinkValidation = _timing.CurTime + LinkValidationInterval;
        EnsureAllSingularityPairs();
    }

    private void EnsureAllSingularityPairs()
    {
        var singularityQuery = EntityQueryEnumerator<SingularityComponent>();
        while (singularityQuery.MoveNext(out var singularityUid, out _))
        {
            EnsureWhiteHoleForSingularity(singularityUid);
        }
    }

    /// <summary>
    /// Ensures a white hole exists for this singularity.
    /// </summary>
    public void EnsureWhiteHoleForSingularity(EntityUid uid)
    {
        var singMapCoords = _transformSystem.GetMapCoordinates(uid);
        if (singMapCoords.MapId == MapId.Nullspace)
            return;

        var linkedWhiteHoles = new List<EntityUid>();
        var wholeQuery = EntityQueryEnumerator<WhiteHoleComponent>();
        while (wholeQuery.MoveNext(out var wholeUid, out var hole))
        {
            if (hole.LinkedSingularity == uid)
                linkedWhiteHoles.Add(wholeUid);
        }

        var oppositeCoords = new MapCoordinates(-singMapCoords.Position, singMapCoords.MapId);

        EntityUid whiteHole;
        if (linkedWhiteHoles.Count == 0)
        {
            // Spawn the white hole at the opposite coordinates
            whiteHole = Spawn("WhiteHole", oppositeCoords);

            // Link them together
            if (TryComp<WhiteHoleComponent>(whiteHole, out var hole))
                hole.LinkedSingularity = uid;
        }
        else
        {
            whiteHole = linkedWhiteHoles[0];

            if (TryComp<WhiteHoleComponent>(whiteHole, out var hole))
                hole.LinkedSingularity = uid;

            for (var i = 1; i < linkedWhiteHoles.Count; i++)
            {
                QueueDel(linkedWhiteHoles[i]);
            }
        }

        if (TryComp<WhiteHoleComponent>(whiteHole, out var link) && link.FollowSingularity)
            _transformSystem.SetMapCoordinates(whiteHole, oppositeCoords);

        if (TryComp<SingularityComponent>(uid, out var singularity))
        {
            if (TryComp<AppearanceComponent>(whiteHole, out var appearance))
                _appearance.SetData(whiteHole, SingularityAppearanceKeys.Singularity, singularity.Level, appearance);

            if (TryComp<RadiationSourceComponent>(whiteHole, out var radiationSource))
                radiationSource.Intensity = singularity.Level * singularity.RadsPerLevel * WhiteHoleRadiationScale;

            if (TryComp<GravityWellComponent>(whiteHole, out var gravityWell))
            {
                gravityWell.MaxRange = SharedSingularitySystem.BaseGravityWellRadius * (singularity.Level + 1);
                gravityWell.BaseRadialAcceleration = -SharedSingularitySystem.BaseGravityWellAcceleration * singularity.Level;
                gravityWell.BaseTangentialAcceleration = 0f;
            }

            if (HasComp<SingularityDistortionComponent>(whiteHole))
                RaiseLocalEvent(whiteHole, new SingularityLevelChangedEvent(singularity.Level, singularity.Level, singularity));
        }
    }

    /// <summary>
    /// Cleans up the white hole when it's deleted.
    /// </summary>
    private void OnWhiteHoleShutdown(EntityUid uid, WhiteHoleComponent component, ComponentShutdown args)
    {
        // White hole cleanup
    }

    /// <summary>
    /// Ejects consumed entities from the white hole.
    /// </summary>
    public void EjectConsumedEntity(EntityUid singularityUid, EntityUid consumedEntity)
    {
        if (HasComp<MindContainerComponent>(consumedEntity))
            return;

        if (!TryGetLinkedWhiteHole(singularityUid, out var wholeUid))
            return;

        // Spawn the consumed entity at the white hole location
        var wholeXform = Transform(wholeUid);

        // Recreate the entity at the white hole position
        var prototype = _entityManager.GetComponent<MetaDataComponent>(consumedEntity).EntityPrototype?.ID;
        if (prototype == null)
            return;

        var ejected = Spawn(prototype, wholeXform.Coordinates);

        // Apply outward velocity from the white hole
        if (TryComp<PhysicsComponent>(ejected, out var physics))
        {
            var hole = Comp<WhiteHoleComponent>(wholeUid);
            var wholePosition = _transformSystem.GetWorldPosition(wholeUid);
            var direction = wholePosition.Normalized();
            if (direction == Vector2.Zero)
                direction = new Vector2(1, 0);

            var velocity = direction * hole.EjectSpeed;
            _physics.SetLinearVelocity(ejected, velocity, body: physics);
        }
    }

    /// <summary>
    /// Sends a mind-bearing entity through the linked white hole instead of deleting it.
    /// </summary>
    public bool TrySendMindEntityToLinkedWhiteHole(EntityUid singularityUid, EntityUid entity)
    {
        if (!HasComp<MindContainerComponent>(entity))
            return false;

        if (!TryGetLinkedWhiteHole(singularityUid, out var wholeUid))
            return false;

        var wholeXform = Transform(wholeUid);
        _transformSystem.AttachToGridOrMap(entity);
        _transformSystem.SetCoordinates(entity, wholeXform.Coordinates);

        if (TryComp<PhysicsComponent>(entity, out var physics))
        {
            var hole = Comp<WhiteHoleComponent>(wholeUid);
            var wholePosition = _transformSystem.GetWorldPosition(wholeUid);
            var direction = wholePosition.Normalized();
            if (direction == Vector2.Zero)
                direction = new Vector2(1, 0);

            _physics.SetLinearVelocity(entity, direction * hole.EjectSpeed, body: physics);
        }

        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"] = MindTransitBluntDamage;
        _damageable.TryChangeDamage(entity, damage, ignoreResistances: true);
        return true;
    }

    private bool TryGetLinkedWhiteHole(EntityUid singularityUid, out EntityUid wholeUid)
    {
        var wholeQuery = EntityQueryEnumerator<WhiteHoleComponent>();
        while (wholeQuery.MoveNext(out var uid, out var hole))
        {
            if (hole.LinkedSingularity == singularityUid)
            {
                wholeUid = uid;
                return true;
            }
        }

        wholeUid = EntityUid.Invalid;
        return false;
    }
}
