using Content.Server.Anomaly.Effects.Components;
using Content.Server.Chat.Systems;
using Content.Server.Examine;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Server.Tether;
using Content.Shared.Actions;
using Content.Shared._Persistence14.PersistentIdentifier;
using Content.Shared._Persistence14.PersistentIdentifier.Reference;
using Content.Shared.Anomaly;
using Content.Shared.Anomaly.Components;
using Content.Shared.Anomaly.Effects.Components;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Implants.Components;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Server.Anomaly.Effects;

/// <summary>
/// Drives the Eye anomaly's pulse phase (mind swap) and, once the crack-open animation finishes,
/// its crit/tether phase: reaches out to every mob within TetherRange (and line of sight) with a
/// continuously-tracking tether (see Content.Server.Tether.TetherVisualSystem - a generic,
/// reusable system, not Eye-specific), captures their mind into a vessel (or grants a MindShield
/// holder a grace period first), sets up a guardian AI (HTN) and hivemind faction on the
/// possessed body, and relays the captured mind's speech through every tethered body. Breaks
/// cleanly if the target dies or wanders too far. The crack-open sprite/core/pulse-stopping
/// behavior itself is still entirely generic (see anomaly_eye.yml's Anomaly component fields),
/// unrelated to this.
/// </summary>
public sealed class EyeAnomalySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ExamineSystem _examine = default!;
    [Dependency] private readonly TetherVisualSystem _tetherVisual = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly HeadsetSystem _headsetSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly NPCSystem _npcSystem = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly PersistentIdentifierSystem _pid = default!;

    private readonly HashSet<Entity<MobStateComponent>> _pulseTargets = new();
    private readonly HashSet<EntityUid> _returning = new();
    private readonly HashSet<Entity<MobStateComponent>> _tetherTargets = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EyeAnomalyComponent, AnomalyPulseEvent>(OnPulse);
        SubscribeLocalEvent<EyeAnomalyComponent, AnomalySupercriticalSettledEvent>(OnSupercriticalSettled);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EyeMindVesselComponent, EntitySpokeEvent>(OnVesselSpoke);
        SubscribeLocalEvent<EyeMindVesselComponent, GetSpeechTransmitRangeEvent>(OnVesselTransmitRange);
        SubscribeLocalEvent<EyeMindVesselComponent, GetSosOverrideEvent>(OnTetheredSosOverride);
    }

    /// <summary>
    /// Lets a tethered (alive) victim use the same "Send SOS" action a dead player would - the
    /// action and its cooldown both live on the VESSEL (granted in CaptureVictimMind), not the
    /// original body, so message text/coordinates and the radio sender name still need to be
    /// pulled from the original body here. Always uses the plain/unshielded message - the manual
    /// button never uses the MindShield-aware wording (see TriggerEyeSos for that one).
    /// </summary>
    private void OnTetheredSosOverride(Entity<EyeMindVesselComponent> ent, ref GetSosOverrideEvent args)
    {
        args.AllowWhileAlive = true;

        if (!_pid.TryResolveId(ent.Comp.OriginalBody, out var body))
            return;

        args.SpeakerOverride = body.Owner;

        if (_pid.TryResolveId(ent.Comp.Eye, out var eye) && TryComp<EyeAnomalyComponent>(eye.Owner, out var eyeComp))
        {
            var mapPos = _transform.GetWorldPosition(body.Owner);
            args.MessageOverride = string.Format(eyeComp.SosMessageUnshielded, Name(body.Owner), mapPos.X, mapPos.Y);
        }
    }

    /// <summary>
    /// A vessel's own speech should never show a visible bubble at its own position (co-located
    /// with the eye, which is meant to stay silent) - EntitySpokeEvent still fires normally for
    /// the relay itself, HideChat only suppresses the bubble.
    /// </summary>
    private void OnVesselTransmitRange(Entity<EyeMindVesselComponent> vessel, ref GetSpeechTransmitRangeEvent args)
    {
        args.Range = ChatTransmitRange.HideChat;
    }

    /// <summary>
    /// When a captured mind's vessel speaks, relay the exact same message through every body
    /// CURRENTLY tethered to the same eye - a hivemind chorus. The eye anomaly itself never
    /// speaks. Re-sent through each body's own TrySendInGameICMessage so it behaves like real
    /// in-character speech in every respect (audible range, chat log, radio prefix handling)
    /// rather than a pushed chat popup. ignoreActionBlocker is needed since these bodies are
    /// mindless and would otherwise be treated as unable to act.
    /// </summary>
    private void OnVesselSpoke(Entity<EyeMindVesselComponent> vessel, ref EntitySpokeEvent args)
    {
        // EntitySpokeEvent.ObfuscatedMessage is set for a whisper - relay using the same chat
        // type the vessel actually used. The CLEAR message is relayed either way (not the
        // obfuscated one) - each recipient's own distance from the relaying body determines
        // their own obfuscation, exactly like a normal whisper.
        var chatType = args.ObfuscatedMessage != null ? InGameICChatType.Whisper : InGameICChatType.Speak;

        var query = EntityQueryEnumerator<TetheredByEyeComponent>();
        while (query.MoveNext(out var body, out var tether))
        {
            if (tether.Eye.TargetId != vessel.Comp.Eye.TargetId)
                continue;

            _chat.TrySendInGameICMessage(body, args.Message, chatType, ChatTransmitRange.Normal, ignoreActionBlocker: true);
        }

        // Radio transmission mirrors HeadsetSystem.OnSpeak, but for a hivemind speaking through a
        // vessel rather than a single wearer with one headset. Any tethered victim whose OWN
        // headset has access to this channel lets the message through using THEIR headset (and
        // identity) as the source - to everyone else it looks like that victim spoke normally.
        if (args.Channel != null)
        {
            var radioQuery = EntityQueryEnumerator<TetheredByEyeComponent>();
            while (radioQuery.MoveNext(out var radioBody, out var radioTether))
            {
                if (radioTether.Eye.TargetId != vessel.Comp.Eye.TargetId)
                    continue;

                if (!_inventory.TryGetSlotEntity(radioBody, "ears", out var headsetEnt))
                    continue;

                if (!TryComp<HeadsetComponent>(headsetEnt, out var headset))
                    continue;

                if (!VictimHasChannelAccess(radioBody, headset, args.Channel))
                    continue;

                _radio.SendRadioMessage(radioBody, args.Message, args.Channel, headsetEnt.Value);
            }
        }
    }

    // =====================================================================================
    // CRIT PHASE - tether, mind capture, and guardian AI
    // =====================================================================================

    /// <summary>
    /// Once the crack-open animation has fully finished (not at the start of it - see
    /// AnomalySupercriticalSettledEvent), reach out to every alive mob within TetherRange and in
    /// line of sight with a tether, capturing minds (or granting a MindShield grace period) and
    /// setting up guardian AI as described in TryTetherNearby. Every subsequent pulse retries the
    /// same acquisition for anyone who wandered in later - see OnPulse. If this initial burst
    /// catches nobody at all, the eye dies right away instead of sitting there pulsing forever
    /// waiting for a victim that may never come.
    /// </summary>
    private void OnSupercriticalSettled(Entity<EyeAnomalyComponent> ent, ref AnomalySupercriticalSettledEvent args)
    {
        if (TryTetherNearby(ent) == 0)
            BeginEyeDeath(ent);
    }

    /// <summary>
    /// Reaches out to every alive, in-line-of-sight, not-already-tethered mob within TetherRange
    /// and tethers them - swapping their faction to EyeThrall immediately, then either capturing
    /// their mind right away or granting a MindShield holder a grace period first (see
    /// SetupHivemindFaction/SetupGuardianAI/CaptureVictimMind below). Returns how many new
    /// tethers were made. Used both for the initial burst the moment the crack-open animation
    /// settles, and again on every post-crit pulse to grab anyone who wandered into range since.
    /// </summary>
    private int TryTetherNearby(Entity<EyeAnomalyComponent> ent)
    {
        _tetherTargets.Clear();
        var coordinates = _transform.GetMapCoordinates(ent);
        _lookup.GetEntitiesInRange(coordinates, ent.Comp.TetherRange, _tetherTargets);

        var tetheredCount = 0;
        foreach (var target in _tetherTargets)
        {
            if (target.Owner == ent.Owner)
                continue;

            if (HasComp<TetheredByEyeComponent>(target))
                continue; // already tethered (possibly by another eye) - leave it be

            if (!_mobState.IsAlive(target))
                continue;

            if (!_examine.InRangeUnOccluded(ent.Owner, target.Owner, ent.Comp.TetherRange))
                continue;

            var tether = AddComp<TetheredByEyeComponent>(target.Owner);
            _pid.AssignIdReference(ref tether.Eye, ent.Owner);
            var visual = _tetherVisual.SpawnTether(ent.Owner, target.Owner, ent.Comp.TetherVisualPrototype,
                TimeSpan.FromSeconds(ent.Comp.ConnectDuration),
                TimeSpan.FromSeconds(ent.Comp.DisconnectDuration));
            _pid.AssignIdReference(ref tether.VisualEntity, visual);

            // A MindShield holder gets a grace window where they're visibly tethered but keep
            // free control of their body - the tether itself doesn't care about MindShield at
            // all, only whether/when the mind actually gets taken. Update() counts this down and
            // finishes the capture once it expires. The faction swap happens right away either
            // way (see SetupHivemindFaction) so a grace-period holder isn't attacked by
            // already-possessed thralls just for not having been taken over yet.
            var hasMindShield = HasComp<MindShieldComponent>(target.Owner);

            SetupHivemindFaction(target.Owner, tether);

            if (hasMindShield)
            {
                tether.State = TetherState.Grace;
                tether.MindShieldGraceRemaining = (float)ent.Comp.MindShieldGrace.TotalSeconds;
            }
            else
            {
                tether.State = TetherState.Connected;
                SetupGuardianAI(ent, target.Owner);
                CaptureVictimMind(ent, target.Owner, tether);
                TriggerEyeSos(ent, target.Owner, hadMindShield: false);
            }

            CancelEyeDeath(ent);

            tetheredCount++;
        }

        return tetheredCount;
    }

    /// <summary>
    /// Swaps a victim's factions to EyeThrall - called for EVERY new tether, including a
    /// MindShield holder's grace window, BEFORE their mind is ever actually captured. Without
    /// this, a MindShield holder still exercising free will during grace would remain in their
    /// original faction, which other already-possessed thralls would treat as hostile - meaning
    /// tethered allies would attack a fellow victim who hasn't even been taken over yet. Separate
    /// from SetupGuardianAI (the HTN/AI part) since grace-period victims still have free will and
    /// should NOT be AI-controlled yet.
    /// </summary>
    private void SetupHivemindFaction(EntityUid victim, TetheredByEyeComponent tether)
    {
        var npcFaction = EnsureComp<NpcFactionMemberComponent>(victim);
        tether.OldFactions.Clear();
        tether.OldFactions.UnionWith(npcFaction.Factions);
        _npcFaction.ClearFactions((victim, npcFaction), false);
        _npcFaction.AddFaction((victim, npcFaction), "EyeThrall");
    }

    /// <summary>
    /// Grants the guardian AI (HTN) itself - called only once a victim's mind is ACTUALLY
    /// captured (immediately for a non-MindShield victim, or at grace-period expiry for one who
    /// had a MindShield), unlike SetupHivemindFaction (the faction swap alone), which happens
    /// right away for every new tether including the grace window. Assumes the faction swap has
    /// already happened - does not touch factions itself.
    /// </summary>
    private void SetupGuardianAI(Entity<EyeAnomalyComponent> ent, EntityUid victim)
    {
        var htn = EnsureComp<HTNComponent>(victim);
        htn.RootTask = new HTNCompoundTask { Task = "EyeGuardianCompound" };
        htn.Blackboard.SetValue("EyeAnchorCoordinates", _transform.GetMoverCoordinates(ent));
        htn.Blackboard.SetValue("EyePatrolRange", ent.Comp.PatrolRadius);
        htn.Blackboard.SetValue("EyeChaseRange", ent.Comp.ChaseRange);
        // NearbyHostilesQuery (used by the guardian compound's melee-combat branch) reads its
        // detection range straight from these two standard blackboard keys - see
        // NPCUtilitySystem/NPCBlackboard.GetVisionRadiusKey - so pointing them at AttackRange is
        // all that's needed for "only engage once someone steps this close".
        htn.Blackboard.SetValue("VisionRadius", ent.Comp.AttackRange);
        htn.Blackboard.SetValue("AggroVisionRadius", ent.Comp.AttackRange);
        _npcSystem.WakeNPC(victim, htn);
        _htn.Replan(htn);
    }

    /// <summary>
    /// Whether victim carries ent's JobImplantPrototype (default "JobNetworkImplant", granted to
    /// virtually every crew job - see AddImplantSpecial in job ymls) - used purely to decide
    /// whether a capture is worth an SOS broadcast/button at all. Wildlife (e.g. cows) gets
    /// tethered/possessed exactly like a crew member, but has nobody to plausibly notify, so this
    /// gates that specifically without touching tether/mind-capture/guardian-AI/hivemind speech.
    /// </summary>
    private bool HasJobImplant(Entity<EyeAnomalyComponent> ent, EntityUid victim)
    {
        if (!TryComp<ImplantedComponent>(victim, out var implanted))
            return false;

        var implantProto = ent.Comp.JobImplantPrototype;
        return implanted.ImplantContainer.ContainedEntities.Any(implant => Prototype(implant)?.ID == implantProto.Id);
    }

    /// <summary>
    /// Actually moves a victim's mind into a vessel - shared between an immediate capture (no
    /// MindShield) and a delayed one (MindShield grace period just expired, see Update()). Only
    /// mindless targets (no player controlling them) skip this entirely - the tether stays
    /// purely visual for them, since there's no mind to move anywhere. The guardian AI itself
    /// (see SetupGuardianAI) is set up separately and unconditionally, regardless of whether this
    /// method finds a mind or not.
    /// </summary>
    private void CaptureVictimMind(Entity<EyeAnomalyComponent> ent, EntityUid victim, TetheredByEyeComponent tether)
    {
        // If they had a MindShield, it failed to stop this and is destroyed in the process.
        RemComp<MindShieldComponent>(victim);

        if (!_mind.TryGetMind(victim, out var mindId, out _))
            return;

        var vessel = Spawn(ent.Comp.MindVesselPrototype, _transform.GetMapCoordinates(ent));
        var vesselComp = AddComp<EyeMindVesselComponent>(vessel);
        _pid.AssignIdReference(ref vesselComp.Eye, ent.Owner);
        _pid.AssignIdReference(ref vesselComp.OriginalBody, victim);

        // Parented to the eye (not just spawned at its current position) so the vessel's own
        // transform - and therefore hearing/chat range - stays glued to the eye even if it moves.
        _transform.SetParent(vessel, ent.Owner);

        // Camera continuously follows the EYE's position/rotation instead of the vessel's own,
        // so the trapped player genuinely sees from the eye's viewpoint, live.
        EnsureComp<EyeComponent>(vessel);
        _eye.SetTarget(vessel, ent.Owner);

        // Guardian AI (faction swap + HTN) is already set up by the caller - see
        // SetupHivemindFaction/SetupGuardianAI - before this method is ever invoked, which is what
        // lets NPCSystem.OnPlayerNPCDetach (only wakes the NPC if an HTNComponent is already
        // present) fire correctly the instant the player detaches below.
        _mind.TransferTo(mindId, vessel);

        _pid.AssignIdReference(ref tether.Mind, mindId);
        _pid.AssignIdReference(ref tether.MindHost, vessel);

        // Grants the same "Open Death Network" action a dead player gets, on the VESSEL (what the
        // player is actually attached to). Only granted if the ORIGINAL body has a job implant -
        // wildlife still gets everything else (tether, mind capture, guardian AI, hivemind
        // speech), just no SOS button, since there's nobody who'd plausibly be notified about them.
        if (HasJobImplant(ent, victim))
        {
            EnsureComp<MobStateActionsComponent>(vessel);
            EntityUid? sosAction = null;
            if (_actions.AddAction(vessel, ref sosAction, "ActionAcceptDeath"))
            {
                tether.SosActionEntity = sosAction;

                // ActionAcceptDeath's prototype has startDelay: true, useDelay: 10 - fine for a
                // dead player with nowhere to be, but a tethered victim should be able to use this
                // immediately - clear that startup cooldown right away.
                _actions.ClearCooldown(sosAction);
            }
        }

        RecomputeAggregatedRadioChannels(ent);
    }

    /// <summary>
    /// Determines whether a victim can actually use a specific radio channel via their own
    /// currently-worn headset. This fork replaced the vanilla encryption-key channel system
    /// entirely - access is computed dynamically from the headset's configured station target
    /// (HeadsetComponent.TransmitTo) plus the wearer's own crew assignment, via
    /// HeadsetSystem.HasChannelAccess. Unencrypted channels are treated as universally accessible
    /// with any working headset, mirroring RadioSystem.SendRadioMessage's own behavior.
    /// </summary>
    private bool VictimHasChannelAccess(EntityUid victim, HeadsetComponent headset, RadioChannelPrototype channel)
    {
        if (!channel.Encrypted)
            return true;

        if (headset.TransmitTo.Count == 0)
            return false;

        foreach (var stationId in headset.TransmitTo)
        {
            if (_stationSystem.GetStationByID(stationId) is not { } station)
                continue;

            if (_headsetSystem.HasChannelAccess(victim, station, channel))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Recomputes the union of radio channels every CURRENTLY tethered victim under this eye can
    /// receive, and applies that same combined set to EVERY vessel under this eye, not just
    /// whichever one just joined or left. This is a hivemind: every captured mind shares
    /// awareness of whatever any of them could individually hear, exactly like a shared earpiece
    /// channel would work for a normal crew. Called whenever the tethered group changes size - a
    /// new capture, or a tether breaking.
    /// </summary>
    private void RecomputeAggregatedRadioChannels(EntityUid eye)
    {
        var combined = new HashSet<ProtoId<RadioChannelPrototype>>();
        var vessels = new List<EntityUid>();
        var allChannels = _prototypeManager.EnumeratePrototypes<RadioChannelPrototype>().ToList();

        var query = EntityQueryEnumerator<TetheredByEyeComponent>();
        while (query.MoveNext(out var body, out var tether))
        {
            if (!_pid.CompareId(tether.Eye, eye))
                continue;

            if (_inventory.TryGetSlotEntity(body, "ears", out var headsetEnt) &&
                TryComp<HeadsetComponent>(headsetEnt, out var headset))
            {
                foreach (var channel in allChannels)
                {
                    if (VictimHasChannelAccess(body, headset, channel))
                        combined.Add(channel.ID);
                }
            }

            if (_pid.TryResolveId(tether.MindHost, out var vesselEnt))
                vessels.Add(vesselEnt.Owner);
        }

        foreach (var vessel in vessels)
        {
            var vesselRadio = EnsureComp<ActiveRadioComponent>(vessel);
            vesselRadio.Channels = new HashSet<ProtoId<RadioChannelPrototype>>(combined);
            Dirty(vessel, vesselRadio);
        }
    }

    /// <summary>
    /// Broadcasts a distress radio message for a victim whose mind was just captured - hooks into
    /// the same underlying mechanism (RadioSystem, "Common" channel) and the exact same
    /// per-victim cooldown field (MobStateActionsComponent.SOSCooldown, shared with the "accept
    /// death" SOS action - CCVars.AcceptDeathTime) as the stock death-triggered one, but
    /// deliberately does NOT check that cooldown before sending - being tethered is meant to
    /// always broadcast immediately. Only the MANUAL "Send SOS" button (via the stock
    /// ValidateSOS) respects that cooldown.
    ///
    /// hadMindShield selects the MindShield-destroyed message for THIS capture specifically - no
    /// history tracked or needed, since a later capture with a fresh MindShield is just as
    /// legitimately "shielded" an event as the first one was.
    /// </summary>
    private void TriggerEyeSos(Entity<EyeAnomalyComponent> ent, EntityUid victim, bool hadMindShield)
    {
        if (!HasJobImplant(ent, victim))
            return;

        if (!TryComp<MobStateActionsComponent>(victim, out var actions))
            return;

        var template = hadMindShield ? ent.Comp.SosMessageMindShielded : ent.Comp.SosMessageUnshielded;

        var mapPos = _transform.GetWorldPosition(victim);
        var message = string.Format(template, Name(victim), mapPos.X, mapPos.Y);

        _radio.SendRadioMessage(victim, message, "Common", victim, true, false);

        actions.SOSCooldown = _timing.CurTime + TimeSpan.FromSeconds(_configurationManager.GetCVar(CCVars.AcceptDeathTime));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var homeQuery = EntityQueryEnumerator<MindSwapHomeComponent>();
        while (homeQuery.MoveNext(out var mindUid, out var home))
        {
            if (now >= home.ReturnAt)
                ReturnHome(mindUid);
        }

        // Finish any eye's death animation once its timer elapses - drop the core and delete the
        // anomaly. CancelEyeDeath (called from TryTetherNearby) can still abort this beforehand if
        // a new victim gets tethered mid-animation.
        var dyingQuery = EntityQueryEnumerator<EyeAnomalyComponent, AnomalyComponent>();
        while (dyingQuery.MoveNext(out var eyeUid, out var eyeCompDying, out var anomalyCompDying))
        {
            if (!eyeCompDying.IsDying || eyeCompDying.DyingSince is not { } dyingSince)
                continue;

            var deathAnimationDuration = anomalyCompDying.DeathAnimationDuration ?? TimeSpan.FromSeconds(2);

            if (now < dyingSince + deathAnimationDuration)
                continue;

            if (anomalyCompDying.CorePrototype is { } corePrototype)
                Spawn(corePrototype, _transform.GetMapCoordinates(eyeUid));

            QueueDel(eyeUid);
        }

        // AllEntityQuery, NOT the normal EntityQueryEnumerator - this loop specifically needs to
        // keep seeing a victim after they go critical/dead. A possessed body has no mind of its
        // own, and stock NPCSystem.OnMobStateChange automatically calls SleepNPC -> SetPaused
        // the instant MobState becomes Critical/Dead precisely because there's no mind attached.
        // The normal EntityQueryEnumerator silently skips paused entities, so with the plain
        // query this loop would stop seeing a body forever the moment it went critical/dead, and
        // the death/distance break-condition below would never fire for them again.
        var tetherQuery = AllEntityQuery<TetheredByEyeComponent>();
        while (tetherQuery.MoveNext(out var victim, out var tether))
        {
            if (!_pid.TryResolveId(tether.Eye, out var eyeEnt) || !TryComp<EyeAnomalyComponent>(eyeEnt.Owner, out var eyeComp))
            {
                BreakTether(victim, tether);
                continue;
            }

            var eyeUid = eyeEnt.Owner;

            // MindShield grace period - the tether is already attached but the mind hasn't
            // actually been taken yet. Once this runs out, finish the capture exactly like an
            // unshielded victim would have gotten immediately.
            if (tether.State == TetherState.Grace)
            {
                tether.MindShieldGraceRemaining -= frameTime;
                if (tether.MindShieldGraceRemaining is <= 0f)
                {
                    tether.State = TetherState.Connected;
                    SetupGuardianAI((eyeUid, eyeComp), victim);
                    CaptureVictimMind((eyeUid, eyeComp), victim, tether);
                    TriggerEyeSos((eyeUid, eyeComp), victim, hadMindShield: true);
                }
            }

            var eyeCoords = _transform.GetMapCoordinates(eyeUid);
            var victimCoords = _transform.GetMapCoordinates(victim);
            var sameMap = eyeCoords.MapId == victimCoords.MapId;
            var distance = sameMap ? (victimCoords.Position - eyeCoords.Position).Length() : float.MaxValue;

            // Break conditions: dead, or wandered past the guardian AI's own chase leash
            // (EyeChaseRange - see eye_guardian.yml's "return to anchor" branch, which pulls a
            // chasing body back BEFORE it gets here)
            var breakDistance = eyeComp.ChaseRange + 1f;
            if (!_mobState.IsAlive(victim) || !sameMap || distance > breakDistance)
                BreakTether(victim, tether);
        }
    }

    private void BreakTether(EntityUid victim, TetheredByEyeComponent tether)
    {
        // Send the mind straight back home, regardless of why the tether is breaking (death,
        // crit, or going out of range). TransferTo is a no-op if the mind has since ended up
        // somewhere else entirely, so this is safe to call unconditionally.
        if (_pid.TryResolveId(tether.Mind, out var mindEnt))
            _mind.TransferTo(mindEnt.Owner, victim);

        // Tear down the guardian AI (if it was ever set up - a grace-period escape never gets
        // this far) and hand the body's factions back exactly as they were. RemComp triggers
        // NPCSystem.OnNPCShutdown automatically, cleaning up ActiveNPCComponent/plan state.
        RemComp<HTNComponent>(victim);

        // Factions get restored unconditionally, separately from the HTN removal above -
        // SetupHivemindFaction runs the moment ANY victim is tethered (including a MindShield
        // holder's grace window, before their mind is ever captured), so a holder who escapes
        // DURING grace also needs their faction restored here, even without an HTNComponent.
        var npcFaction = EnsureComp<NpcFactionMemberComponent>(victim);
        _npcFaction.RemoveFaction((victim, npcFaction), "EyeThrall", false);
        _npcFaction.AddFactions((victim, npcFaction), tether.OldFactions);
        tether.OldFactions.Clear();

        if (_pid.TryResolveId(tether.MindHost, out var vesselEnt))
            QueueDel(vesselEnt.Owner);

        // Revoke the manually-granted SOS button - tracked separately from
        // MobStateActionsComponent.GrantedActions so this can't accidentally remove an action the
        // stock system granted for an unrelated reason (e.g. genuinely dying).
        if (tether.SosActionEntity is { } sosAction)
            _actions.RemoveAction(sosAction);

        // Plays the retract animation over the tether's DisconnectDuration, then the tether
        // deletes itself - instead of vanishing instantly.
        if (_pid.TryResolveId(tether.VisualEntity, out var visualEnt))
            _tetherVisual.BeginDisconnect(visualEnt.Owner);

        RemComp<TetheredByEyeComponent>(victim);

        // Resolve the eye once for the post-break bookkeeping. If it no longer resolves (the eye
        // itself was destroyed - which is one of the ways a tether breaks), there's nothing left
        // to recompute channels for or to check for death, so skipping both is correct.
        if (_pid.TryResolveId(tether.Eye, out var eyeEnt))
        {
            RecomputeAggregatedRadioChannels(eyeEnt.Owner);
            CheckEyeDeathTrigger(eyeEnt.Owner);
        }
    }

    /// <summary>
    /// Call after any tether breaks - if this eye now has NONE left, start its death animation.
    /// Does nothing if the eye is already dying or no longer exists.
    /// </summary>
    private void CheckEyeDeathTrigger(EntityUid eye)
    {
        if (!Exists(eye) || !TryComp<EyeAnomalyComponent>(eye, out var eyeComp))
            return;

        if (eyeComp.IsDying)
            return;

        var query = EntityQueryEnumerator<TetheredByEyeComponent>();
        while (query.MoveNext(out var tether))
        {
            if (_pid.CompareId(tether.Eye, eye))
                return; // still has at least one thrall
        }

        BeginEyeDeath((eye, eyeComp));
    }

    /// <summary>
    /// Starts the death animation/state directly, without the "does it still have a thrall" check
    /// CheckEyeDeathTrigger does - used both by CheckEyeDeathTrigger itself once it's confirmed no
    /// thralls remain, and by OnSupercriticalSettled when the initial tether burst catches nobody
    /// at all.
    /// </summary>
    private void BeginEyeDeath(Entity<EyeAnomalyComponent> ent)
    {
        ent.Comp.IsDying = true;
        ent.Comp.DyingSince = _timing.CurTime;
        _appearance.SetData(ent, AnomalyVisuals.Dying, true);
    }

    /// <summary>
    /// Call whenever a NEW victim gets tethered - if the eye was mid-death-animation, a fresh
    /// thrall means it's not "done" after all, so cancel the sequence and revert the visual.
    /// </summary>
    private void CancelEyeDeath(Entity<EyeAnomalyComponent> ent)
    {
        if (!ent.Comp.IsDying)
            return;

        ent.Comp.IsDying = false;
        ent.Comp.DyingSince = null;
        _appearance.SetData(ent, AnomalyVisuals.Dying, false);
    }

    private void OnPulse(Entity<EyeAnomalyComponent> ent, ref AnomalyPulseEvent args)
    {
        // Post-crit, the pulse's EFFECT changes entirely: instead of shuffling minds, it retries
        // tether acquisition for anyone who wandered in since the initial burst on settling. (The
        // pulse's ANIMATION also changes - that's handled generically, see
        // AnomalyComponent.SupercriticalPulseState.) During the brief crit transition window
        // (supercritical started, but the crack-open animation hasn't settled yet) pulses do
        // nothing - mind-swapping mid-crack would be weird, and the tethers aren't out yet either.
        if (_appearance.TryGetData<bool>(ent, AnomalyVisuals.SupercriticalSettled, out var isSettled) && isSettled)
        {
            TryTetherNearby(ent);
            return;
        }

        if (_appearance.TryGetData<bool>(ent, AnomalyVisuals.Supercritical, out var isSupercritical) && isSupercritical)
            return;

        var severity = Math.Clamp(args.Severity, 0f, 1f);
        var comp = ent.Comp;

        var range = MathHelper.Lerp(comp.MinRange, comp.MaxRange, severity);
        var rawCount = (int)MathF.Round(MathHelper.Lerp(comp.MinTargets, comp.MaxTargets, severity));
        var targetCount = rawCount - rawCount % 2; // always an even number

        var durationSeconds = MathHelper.Lerp((float)comp.MinDuration.TotalSeconds, (float)comp.MaxDuration.TotalSeconds, severity);
        var duration = TimeSpan.FromSeconds(durationSeconds);

        if (targetCount < 2)
            return;

        var coordinates = _transform.GetMapCoordinates(ent);
        _pulseTargets.Clear();
        _lookup.GetEntitiesInRange(coordinates, range, _pulseTargets);

        // Eligible: alive, and capable of holding a mind (whether or not it currently has one) -
        // this deliberately includes plain animals with no mind at all, so a swap can occasionally
        // land a player in a mindless critter's body instead of another crew member.
        var eligible = new List<EntityUid>();
        foreach (var target in _pulseTargets)
        {
            if (!_mobState.IsAlive(target))
                continue;

            if (!HasComp<MindContainerComponent>(target))
                continue;

            eligible.Add(target);
        }

        if (eligible.Count < 2)
            return;

        targetCount = Math.Min(targetCount, eligible.Count - eligible.Count % 2);
        if (targetCount < 2)
            return;

        _random.Shuffle(eligible);

        for (var i = 0; i < targetCount / 2; i++)
        {
            var bodyA = eligible[i * 2];
            var bodyB = eligible[i * 2 + 1];
            SwapMinds(ent, bodyA, bodyB, duration);
        }
    }

    private void SwapMinds(Entity<EyeAnomalyComponent> ent, EntityUid bodyA, EntityUid bodyB, TimeSpan duration)
    {
        var hadMindA = _mind.TryGetMind(bodyA, out var mindA, out _);
        var hadMindB = _mind.TryGetMind(bodyB, out var mindB, out _);

        if (hadMindA)
            _mind.TransferTo(mindA, bodyB);

        if (hadMindB)
            _mind.TransferTo(mindB, bodyA);

        if (hadMindA)
            TrackDisplacement(mindA, bodyA, ent, duration);
        if (hadMindB)
            TrackDisplacement(mindB, bodyB, ent, duration);
    }

    /// <summary>
    /// Records (or refreshes) that a mind is away from home. HomeBody only ever gets set on the
    /// FIRST displacement - EnsureComp hands back a freshly zeroed component whose HomeBody is the
    /// default (empty) PersistentEntityReference, so "does HomeBody resolve to a real entity"
    /// doubles as a clean "is this the first time" check without needing a separate flag.
    /// </summary>
    private void TrackDisplacement(EntityUid mindId, EntityUid trueHome, Entity<EyeAnomalyComponent> source, TimeSpan duration)
    {
        var home = EnsureComp<MindSwapHomeComponent>(mindId);

        // Only stamp HomeBody the first time: if the current reference doesn't resolve to a live
        // entity, this mind has never been displaced before (fresh component = EmptyId reference).
        if (!_pid.TryResolveId(home.HomeBody, out _))
            _pid.AssignIdReference(ref home.HomeBody, trueHome);

        home.ReturnAt = _timing.CurTime + duration;
        _pid.AssignIdReference(ref home.SourceAnomaly, source.Owner);
    }

    /// <summary>
    /// Sends a mind back to its true home body, cascading - if that body is currently occupied by
    /// a different mind, that mind gets sent home first (recursively), exactly as if it had also
    /// been reverted. Safe to call redundantly on a mind that's already home or has no pending
    /// displacement at all.
    /// </summary>
    private void ReturnHome(EntityUid mindId)
    {
        if (!TryComp<MindSwapHomeComponent>(mindId, out var home))
            return;

        if (!_pid.TryResolveId(home.HomeBody, out var homeEnt))
        {
            RemComp<MindSwapHomeComponent>(mindId);
            return;
        }

        var homeBody = homeEnt.Owner;

        if (_mind.TryGetMind(homeBody, out var currentOccupant, out _) && currentOccupant == mindId)
        {
            RemComp<MindSwapHomeComponent>(mindId);
            return;
        }

        if (!_returning.Add(mindId))
            return; // already mid-return further up this call stack - avoid infinite recursion

        try
        {
            if (_mind.TryGetMind(homeBody, out var occupantMind, out _) && occupantMind != mindId)
            {
                if (_returning.Contains(occupantMind))
                {
                    // Cycle: occupantMind is itself already mid-return in this cascade - clear it
                    // with no body instead of recursing forever.
                    _mind.TransferTo(occupantMind, null);
                }
                else
                {
                    ReturnHome(occupantMind);
                }
            }

            _mind.TransferTo(mindId, homeBody);
            RemComp<MindSwapHomeComponent>(mindId);
        }
        finally
        {
            _returning.Remove(mindId);
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState is not (MobState.Critical or MobState.Dead))
            return;

        if (!_mind.TryGetMind(args.Target, out var mindId, out _))
            return;

        if (!TryComp<MindSwapHomeComponent>(mindId, out var home))
            return;

        if (!_pid.TryResolveId(home.SourceAnomaly, out var srcEnt) ||
            !TryComp<EyeAnomalyComponent>(srcEnt.Owner, out var sourceComp))
            return;

        var shouldRevert = args.NewMobState == MobState.Dead ? sourceComp.RevertOnDeath : sourceComp.RevertOnCrit;

        if (!shouldRevert)
            return;

        ReturnHome(mindId);
    }
}
