using System.Numerics;
using Content.Client.Gravity;
using Content.Shared.Anomaly;
using Content.Shared.Anomaly.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.Anomaly;

public sealed partial class AnomalySystem : SharedAnomalySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly FloatingVisualizerSystem _floating = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnomalyComponent, AppearanceChangeEvent>(OnAppearanceChanged);
        SubscribeLocalEvent<AnomalyComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AnomalyComponent, AnimationCompletedEvent>(OnAnimationComplete);

        SubscribeLocalEvent<AnomalySupercriticalComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(EntityUid uid, AnomalyComponent component, ComponentStartup args)
    {
        _floating.FloatAnimation(uid, component.FloatingOffset, component.AnimationKey, component.AnimationTime);
    }

    private void OnAnimationComplete(EntityUid uid, AnomalyComponent component, AnimationCompletedEvent args)
    {
        if (args.Key != component.AnimationKey)
            return;
        _floating.FloatAnimation(uid, component.FloatingOffset, component.AnimationKey, component.AnimationTime);
    }

    /// <summary>
    /// Purely declarative - reads whatever the server's current authoritative appearance state
    /// says and shows exactly that, with no client-local memory of "have I already done this"
    /// needed at all. This is deliberate: a client that only just connected, or whose view of
    /// this entity was only just (re)established, gets an identical AppearanceChangeEvent to one
    /// that's been watching the whole time, and both must show the same correct result - if a
    /// custom crit animation had already finished and settled minutes ago, a brand new observer
    /// must see it settled immediately, never replay the transition.
    /// </summary>
    private void OnAppearanceChanged(EntityUid uid, AnomalyComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        if (!Appearance.TryGetData<bool>(uid, AnomalyVisuals.IsPulsing, out var rawPulsing, args.Component))
            rawPulsing = false;

        // Layer VISIBILITY treats "supercritical" as permanently pulsing (Animated layer stays
        // up forever once cracked) - but the raw IsPulsing value is kept separate above, since
        // post-settle it's what decides whether to show the dedicated pulse state or the calm
        // settled state.
        var pulsing = rawPulsing;

        var isSupercritical = Appearance.TryGetData<bool>(uid, AnomalyVisuals.Supercritical, out var super, args.Component) && super;
        if (isSupercritical)
            pulsing = true;

        if (HasComp<AnomalySupercriticalComponent>(uid))
            pulsing = true;

        if (!_sprite.LayerMapTryGet((uid, sprite), AnomalyVisualLayers.Base, out var layer, false) ||
            !_sprite.LayerMapTryGet((uid, sprite), AnomalyVisualLayers.Animated, out var animatedLayer, false))
            return;

        // Dying takes priority over everything else below - same declarative pattern as
        // Supercritical/SupercriticalSettled, just via DeathAnimationState instead.
        var isDying = Appearance.TryGetData<bool>(uid, AnomalyVisuals.Dying, out var dying, args.Component) && dying;

        if (isDying && component.DeathAnimationState is { } deathState)
        {
            _sprite.LayerSetVisible((uid, sprite), layer, false);
            _sprite.LayerSetVisible((uid, sprite), animatedLayer, true);
            _sprite.LayerSetRsiState((uid, sprite), animatedLayer, deathState);
            return;
        }

        _sprite.LayerSetVisible((uid, sprite), layer, !pulsing);
        _sprite.LayerSetVisible((uid, sprite), animatedLayer, pulsing);

        // Fully custom "going critical" animation, entirely declarative: settled takes priority
        // over the transition state, both of which are authoritative appearance data set only by
        // the server (see SharedAnomalySystem.Update for SupercriticalSettled). No GenericVisualizer
        // wiring needed, and critically, no risk of ever replaying the transition for a late
        // observer, since this always reflects the CURRENT state rather than "what changed."
        // Within the settled phase, an active pulse can show its own dedicated state
        // (SupercriticalPulseState) - the IsPulsing flag toggling back off returns it to the
        // settled state via this same declarative logic.
        var isSettled = Appearance.TryGetData<bool>(uid, AnomalyVisuals.SupercriticalSettled, out var settled, args.Component) && settled;

        if (isSettled)
        {
            if (rawPulsing && component.SupercriticalPulseState is { } pulseState)
            {
                _sprite.LayerSetRsiState((uid, sprite), animatedLayer, pulseState);
            }
            else if (component.SupercriticalSettledState is { } settledState)
            {
                _sprite.LayerSetRsiState((uid, sprite), animatedLayer, settledState);
            }
        }
        else if (isSupercritical && component.SupercriticalAnimationState is { } critState)
        {
            _sprite.LayerSetRsiState((uid, sprite), animatedLayer, critState);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AnomalyComponent, AnomalySupercriticalComponent, SpriteComponent>();

        while (query.MoveNext(out var uid, out var anomaly, out var super, out var sprite))
        {
            if (anomaly.SkipSupercriticalAnimation)
                continue;

            var completion = 1f - (float)((super.EndTime - _timing.CurTime) / anomaly.SupercriticalDuration);
            var scale = completion * (super.MaxScaleAmount - 1f) + 1f;
            _sprite.SetScale((uid, sprite), new Vector2(scale, scale));

            var transparency = (byte)(65 * (1f - completion) + 190);
            if (transparency < sprite.Color.AByte)
            {
                _sprite.SetColor((uid, sprite), sprite.Color.WithAlpha(transparency));
            }
        }
    }

    private void OnShutdown(Entity<AnomalySupercriticalComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.SetScale((ent.Owner, sprite), Vector2.One);
        _sprite.SetColor((ent.Owner, sprite), sprite.Color.WithAlpha(1f));
    }
}
