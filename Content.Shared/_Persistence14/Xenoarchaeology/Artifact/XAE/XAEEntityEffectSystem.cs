using System.Linq;
using System.Numerics;
using Content.Shared._Persistence14.Xenoarcheology.Artifact.XAE.Components;
using Content.Shared.EntityEffects;
using Content.Shared.Whitelist;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.XAE;
using Robust.Shared.Random;

namespace Content.Shared._Persistence14.Xenoarcheology.Artifact.XAE;

public sealed partial class XAEEntityEffectSystem : BaseXAESystem<XAEEntityEffectComponent>
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedEntityEffectsSystem _effects = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    protected override void OnActivated(Entity<XAEEntityEffectComponent> ent, ref XenoArtifactNodeActivatedEvent args)
    {
        foreach (var effect in ent.Comp.Effects)
        {
            var targets = GetTargets(ent, effect, args).ToHashSet();
            foreach (var target in targets)
            {
                _effects.ApplyEffect(target, effect.Effect, user: args.User);
            }
        }
    }

    private IEnumerable<EntityUid> GetTargets(Entity<XAEEntityEffectComponent> ent, ArtifactEntityEffect effect, XenoArtifactNodeActivatedEvent args)
    {
        var artifactPos = _transform.GetWorldPosition(ent.Owner);

        // Ignores all distance/whitelist restrictions
        if (effect.Flags.HasFlag(XAEEntityEffectTargetFlags.Artifact))
        {
            yield return args.Artifact;
        }

        /// Ignores whitelist and minimum distance restrictions
        if (effect.Flags.HasFlag(XAEEntityEffectTargetFlags.User))
        {
            if (args.User is { } user)
            {
                var userPos = _transform.GetWorldPosition(user);
                var dist = Vector2.DistanceSquared(artifactPos, userPos);
                if (dist <= effect.MaxRange * effect.MaxRange)
                {
                    // User flag ignores minimum distance.
                    yield return user;
                }
            }
        }

        var targets = GetInRange(ent.Owner, effect.MinRange, effect.MaxRange, effect.Whitelist, effect.Blacklist).ToList();
        if (effect.Flags.HasFlag(XAEEntityEffectTargetFlags.Nearest))
        {
            targets.Sort((a, b) => a.distSquared.CompareTo(b.distSquared));
            var count = Math.Min(effect.Count, targets.Count);
            for (int i = count - 1; i >= 0; i--)
            {
                yield return targets[i].uid;
                targets.RemoveAt(i);
            }
        }

        if (effect.Flags.HasFlag(XAEEntityEffectTargetFlags.Nearby))
        {
            foreach (var (target, _) in targets)
                yield return target;
        }

        if (effect.Flags.HasFlag(XAEEntityEffectTargetFlags.Random))
        {
            var count = Math.Min(effect.Count, targets.Count);
            for (int i = 0; i < count; i++)
                yield return _random.PickAndTake(targets).uid;
        }
    }

    private IEnumerable<(EntityUid uid, float distSquared)> GetInRange(
        EntityUid source,
        float minRange, float maxRange,
        EntityWhitelist? whitelist, EntityWhitelist? blacklist)
    {
        var sourceXform = Transform(source);
        var sourcePos = _transform.GetWorldPosition(sourceXform);

        var minRangeSquared = minRange * minRange;

        foreach (var target in _lookup.GetEntitiesInRange(sourceXform.Coordinates, maxRange))
        {
            if (target == source)
                continue;

            if (_whitelist.IsWhitelistFail(whitelist, target) ||
                _whitelist.IsWhitelistPass(blacklist, target))
                continue;

            var targetXform = Transform(target);

            if (targetXform.MapID != sourceXform.MapID)
                continue;

            var targetPos = _transform.GetWorldPosition(targetXform);
            var distanceSquared = Vector2.DistanceSquared(targetPos, sourcePos);

            if (distanceSquared < minRangeSquared)
                continue;

            yield return (target, distanceSquared);
        }
    }
}