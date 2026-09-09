using Content.Shared.Administration.Logs;
using Content.Shared.CCVar;
using Content.Shared.DragDrop;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NameModifier.EntitySystems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared.Bed.Cryostorage;

/// <summary>
/// This handles <see cref="CryostorageComponent"/>
/// </summary>
public abstract class SharedCryostorageSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly ISharedAdminLogManager AdminLog = default!;
    [Dependency] protected readonly SharedMindSystem Mind = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] protected readonly NameModifierSystem _nameModifier = default!;

    protected EntityUid? PausedMap { get; private set; }

    protected bool CryoSleepRejoiningEnabled;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<CryostorageComponent, EntInsertedIntoContainerMessage>(OnInsertedContainer);
        SubscribeLocalEvent<CryostorageComponent, EntRemovedFromContainerMessage>(OnRemovedContainer);
        SubscribeLocalEvent<CryostorageComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<CryostorageComponent, ComponentShutdown>(OnShutdownContainer);
        SubscribeLocalEvent<CryostorageComponent, CanDropTargetEvent>(OnCanDropTarget);
        SubscribeLocalEvent<CryostorageComponent, PersonalCryoEvent>(OnPersonalCryo);
        SubscribeLocalEvent<CryostorageComponent, RefreshNameModifiersEvent>(OnRefreshName);

        SubscribeLocalEvent<CryostorageContainedComponent, EntGotRemovedFromContainerMessage>(OnRemovedContained);
        SubscribeLocalEvent<CryostorageContainedComponent, ComponentShutdown>(OnShutdownContained);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        Subs.CVar(_configuration, CCVars.GameCryoSleepRejoining, OnCvarChanged, true);
    }

    private void OnRefreshName(Entity<CryostorageComponent> ent, ref RefreshNameModifiersEvent args)
    {
        if(ent.Comp.PersonalMode && ent.Comp.PersonalName != null)
        {
            args.AddModifier($"Personal Cryopod ({ent.Comp.PersonalName})", 1);
        }
    }

    private void OnPersonalCryo(Entity<CryostorageComponent> ent, ref PersonalCryoEvent args)
    {
        ent.Comp.PersonalOccupied = args.state;
        _appearance.SetData(ent, CryostorageVisuals.Full, args.state);
        _nameModifier.RefreshNameModifiers(ent.Owner);

    }

    private void OnCvarChanged(bool value)
    {
        CryoSleepRejoiningEnabled = value;
    }

    protected virtual void OnInsertedContainer(Entity<CryostorageComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        var (_, comp) = ent;
        if (args.Container.ID != comp.ContainerId)
            return;

        _appearance.SetData(ent, CryostorageVisuals.Full, true);
        if (!Timing.IsFirstTimePredicted)
            return;

        var containedComp = EnsureComp<CryostorageContainedComponent>(args.Entity);
        var delay = Mind.TryGetMind(args.Entity, out _, out _) ? comp.GracePeriod : comp.NoMindGracePeriod;
        containedComp.GracePeriodEndTime = Timing.CurTime + delay;
        containedComp.Cryostorage = ent;
        Dirty(args.Entity, containedComp);
    }

    private void OnRemovedContainer(Entity<CryostorageComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        var (_, comp) = ent;
        if (args.Container.ID != comp.ContainerId)
            return;

        if (ent.Comp.PersonalMode == true)
        {
            ent.Comp.PersonalName = null;

        }
        if(!ent.Comp.PersonalOccupied)
            _appearance.SetData(ent, CryostorageVisuals.Full, args.Container.ContainedEntities.Count > 0);
    }

    private void UpdatePersonalOccupied(Entity<CryostorageComponent> ent, bool occupied)
    {
        ent.Comp.PersonalOccupied = occupied;
        _appearance.SetData(ent, CryostorageVisuals.Full, occupied);
    }
    private void OnInsertAttempt(Entity<CryostorageComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        var (_, comp) = ent;
        if (args.Container.ID != comp.ContainerId)
            return;

        if(ent.Comp.PersonalMode == true)
        {
            if(ent.Comp.PersonalName != null && ent.Comp.PersonalName != Name(args.EntityUid))
            {
                args.Cancel();
                return;
            }
        }

        if (_mobState.IsIncapacitated(args.EntityUid))
        {
            args.Cancel();
            return;
        }

        if (!HasComp<CanEnterCryostorageComponent>(args.EntityUid) || !TryComp<MindContainerComponent>(args.EntityUid, out var mindContainer))
        {
            args.Cancel();
            return;
        }

        //if (Mind.TryGetMind(args.EntityUid, out _, out var mindComp, mindContainer) &&
        //    (mindComp.PreventSuicide || mindComp.PreventGhosting))
        //{
        //    args.Cancel();
        //}
    }

    private void OnShutdownContainer(Entity<CryostorageComponent> ent, ref ComponentShutdown args)
    {
        var comp = ent.Comp;
        foreach (var stored in comp.StoredPlayers)
        {
            if (TryComp<CryostorageContainedComponent>(stored, out var containedComponent))
            {
                containedComponent.Cryostorage = null;
                Dirty(stored, containedComponent);
            }
        }

        comp.StoredPlayers.Clear();
        Dirty(ent, comp);
    }

    private void OnCanDropTarget(Entity<CryostorageComponent> ent, ref CanDropTargetEvent args)
    {
        if (args.Dragged == args.User)
            return;

        if (!_player.TryGetSessionByEntity(args.Dragged, out var session) ||
            session.AttachedEntity != args.Dragged)
            return;

        args.CanDrop = false;
        args.Handled = true;
    }

    private void OnRemovedContained(Entity<CryostorageContainedComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        var (uid, comp) = ent;
        if (!IsInPausedMap(uid))
            RemCompDeferred(ent, comp);
    }

    private void OnShutdownContained(Entity<CryostorageContainedComponent> ent, ref ComponentShutdown args)
    {
        var comp = ent.Comp;

        CompOrNull<CryostorageComponent>(comp.Cryostorage)?.StoredPlayers.Remove(ent);
        ent.Comp.Cryostorage = null;
        Dirty(ent, comp);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent _)
    {
        DeletePausedMap();
    }

    private void DeletePausedMap()
    {
        if (PausedMap == null || !Exists(PausedMap))
            return;

        Del(PausedMap.Value);
        PausedMap = null;
    }

    protected void EnsurePausedMap()
    {
        if (PausedMap != null && Exists(PausedMap))
            return;

        var mapUid = _map.CreateMap();
        _meta.SetEntityName(mapUid, Loc.GetString("cryostorage-paused-map-name"));
        _map.SetPaused(mapUid, true);
        PausedMap = mapUid;
    }

    public bool IsInPausedMap(Entity<TransformComponent?> entity)
    {
        var (_, comp) = entity;
        comp ??= Transform(entity);

        return comp.MapUid != null && comp.MapUid == PausedMap;
    }
}

[ByRefEvent]
public record struct PersonalCryoEvent(bool state);

