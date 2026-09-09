using Content.Shared.CCVar;
using Content.Shared.Movement.Events; // Persistence: SSD Command
using Content.Shared.StatusEffectNew;
using Content.Shared.Verbs; // Persistence: SSD Verb
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility; // Persistence: SSD Verb

namespace Content.Shared.SSDIndicator;

/// <summary>
///     Handle changing player SSD indicator status
/// </summary>
public sealed class SSDIndicatorSystem : EntitySystem
{
    public static readonly EntProtoId StatusEffectSSDSleeping = "StatusEffectSSDSleeping";

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    private bool _icSsdSleep;
    private float _icSsdSleepTime;

    public override void Initialize()
    {
        SubscribeLocalEvent<SSDIndicatorComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SSDIndicatorComponent, PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<SSDIndicatorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SSDIndicatorComponent, MoveInputEvent>(OnEntityTryInput); // Persistence: SSD Command
        SubscribeLocalEvent<SSDIndicatorComponent, GetVerbsEvent<Verb>>(GetVerb); // Persistence: SSD Verb

        _cfg.OnValueChanged(CCVars.ICSSDSleep, obj => _icSsdSleep = obj, true);
        _cfg.OnValueChanged(CCVars.ICSSDSleepTime, obj => _icSsdSleepTime = obj, true);
    }

    private void OnPlayerAttached(EntityUid uid, SSDIndicatorComponent component, PlayerAttachedEvent args)
    {
        // Persistence: SSD Command
        StopSSD(uid, component);
    }

    private void OnPlayerDetached(EntityUid uid, SSDIndicatorComponent component, PlayerDetachedEvent args)
    {
        // Persistence: SSD Command
        component.ManualSSD = false;
        StartSSD(uid, component);
    }

    // Prevents mapped mobs to go to sleep immediately
    private void OnMapInit(EntityUid uid, SSDIndicatorComponent component, MapInitEvent args)
    {
        if (!_icSsdSleep || !component.IsSSD)
            return;

        component.FallAsleepTime = _timing.CurTime + TimeSpan.FromSeconds(_icSsdSleepTime);
        component.NextUpdate = _timing.CurTime + component.UpdateInterval;
        Dirty(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_icSsdSleep)
            return;

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<SSDIndicatorComponent>();

        while (query.MoveNext(out var uid, out var ssd))
        {
            // Forces the entity to sleep when the time has come
            if (!ssd.IsSSD
                || ssd.NextUpdate > curTime
                || ssd.FallAsleepTime > curTime
                || TerminatingOrDeleted(uid))
                continue;

            _statusEffects.TryUpdateStatusEffectDuration(uid, StatusEffectSSDSleeping);
            ssd.NextUpdate += ssd.UpdateInterval;
            Dirty(uid, ssd);
        }
    }

    /// <summary>
    /// Persistence: Set the given entity as SSD
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    public void StartSSD(EntityUid uid, SSDIndicatorComponent component)
    {
        component.IsSSD = true;

        // Sets the time when the entity should fall asleep
        if (_icSsdSleep)
        {
            component.FallAsleepTime = _timing.CurTime + TimeSpan.FromSeconds(_icSsdSleepTime);
        }

        Dirty(uid, component);
    }

    /// <summary>
    /// Persistence: Set the given entity as not SSD
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    public void StopSSD(EntityUid uid, SSDIndicatorComponent component)
    {
        component.IsSSD = false;

        // Removes force sleep and resets the time to zero
        if (_icSsdSleep)
        {
            component.FallAsleepTime = TimeSpan.Zero;
            _statusEffects.TryRemoveStatusEffect(uid, StatusEffectSSDSleeping);
        }

        Dirty(uid, component);
    }

    /// <summary>
    /// Persistence: Toggle SSD state & use ManualSSD flag.
    /// </summary>
    /// <param name="uid"></param>
    public void ToggleManualSSD(EntityUid uid)
    {
        if (!TryComp<SSDIndicatorComponent>(uid, out var component))
            return;

        if (component.IsSSD)
        {
            component.ManualSSD = false;
            StopSSD(uid, component);
        }
        else
        {
            component.ManualSSD = true;
            StartSSD(uid, component);
        }
    }

    /// <summary>
    /// Persistence: Disables SSD on SSD entities that attempt to move.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    /// <param name="args"></param>
    private void OnEntityTryInput(EntityUid uid, SSDIndicatorComponent component, MoveInputEvent args)
    {
        if (!component.IsSSD)
            return;

        ToggleManualSSD(uid);
    }

    /// <summary>
    /// Persistence: Adds the manual SSD verb to uid when args.User == uid
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    /// <param name="args"></param>
    private void GetVerb(EntityUid uid, SSDIndicatorComponent component, GetVerbsEvent<Verb> args)
    {
        if (args.User != uid)
            return;

        var label = "verb-manual-ssd-label-off";
        var desc = "verb-manual-ssd-desc-off";
        if (component.IsSSD)
        {
            label = "verb-manual-ssd-label-on";
            desc = "verb-manual-ssd-desc-on";
        }

        args.Verbs.Add(new Verb
        {
            Act = () => ToggleManualSSD(uid),
            Text = Loc.GetString(label),
            Message = Loc.GetString(desc),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Effects/ssd.rsi/default0-blue.png")),
        });
    }
}
