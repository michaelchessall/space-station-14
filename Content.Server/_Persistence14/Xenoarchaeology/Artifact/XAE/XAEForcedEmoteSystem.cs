using Content.Server.Chat;
using Content.Server.Chat.Systems;
using Content.Server.Xenoarchaeology.Artifact.XAE.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.XAE;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Server.Xenoarchaeology.Artifact.XAE;

/// <summary>
/// System for a xeno artifact effect that forces nearby mobs into a repeated emote "fit"
/// (e.g. uncontrollable laughter) for a fixed duration. Line of sight is ignored. The repeat itself
/// (interval + probability) is driven by the game's AutoEmote machinery; this system only applies
/// the auto-emote to living mobs in range and removes it again once the duration elapses.
/// </summary>
public sealed class XAEForcedEmoteSystem : BaseXAESystem<XAEForcedEmoteComponent>
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AutoEmoteSystem _autoEmote = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    /// <summary> Pre-allocated and re-used collection. </summary>
    private readonly HashSet<EntityUid> _entities = new();

    /// <inheritdoc/>
    protected override void OnActivated(Entity<XAEForcedEmoteComponent> ent, ref XenoArtifactNodeActivatedEvent args)
    {
        var comp = ent.Comp;
        var endTime = _timing.CurTime + comp.Duration;

        _entities.Clear();
        _lookup.GetEntitiesInRange(args.Coordinates, comp.Radius, _entities);
        foreach (var mob in _entities)
        {
            // Only living mobs are affected.
            if (!_mobState.IsAlive(mob))
                continue;

            // Reuse the game's AutoEmote timer to drive the repeat. Because the AutoEmote prototype is
            // non-forced, only mobs that can actually perform the emote will - anything that can't
            // (wrong species, muzzled, etc.) is silently skipped, matching how weh / laughing gas act.
            EnsureComp<AutoEmoteComponent>(mob);
            _autoEmote.AddEmote(mob, comp.AutoEmote);

            // AutoEmote has no lifetime of its own, so we time the fit out ourselves. A mob can be
            // under several different forced emotes at once, so each is tracked separately;
            // re-triggering the same emote just refreshes its end time.
            var fit = EnsureComp<ForcedEmoteFitComponent>(mob);
            fit.EndTimes[comp.AutoEmote] = endTime;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ForcedEmoteFitComponent>();
        while (query.MoveNext(out var uid, out var fit))
        {
            // Snapshot the keys so we can mutate the dictionary while iterating.
            foreach (var autoEmote in fit.EndTimes.Keys.ToArray())
            {
                if (now < fit.EndTimes[autoEmote])
                    continue;

                // removeEmpty (default true) also drops the AutoEmoteComponent once no auto-emotes
                // remain, unless the mob has other auto-emotes of its own (e.g. a cluwne), which are
                // left untouched.
                _autoEmote.RemoveEmote(uid, autoEmote);
                fit.EndTimes.Remove(autoEmote);
            }

            if (fit.EndTimes.Count == 0)
                RemCompDeferred<ForcedEmoteFitComponent>(uid);
        }
    }
}
