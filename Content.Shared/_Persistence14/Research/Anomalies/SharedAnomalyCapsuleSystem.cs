using System.Linq;
using Content.Shared._Persistence14.RandomTable;
using Content.Shared._Persistence14.RandomTable.State;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Persistence14.Research.Anomalies;

public sealed partial class SharedAnomalyCapsuleSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly RandomTableSystem _randomTable = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public const string Sawmill = "anomaly-capsule";

    public override void Initialize()
    {
        SubscribeModuleRelayEvent<AfterInteractEvent>();
        SubscribeLocalEvent<AnomalyCapsuleComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
    }

    private void OnInsertAttempt(Entity<AnomalyCapsuleComponent> capsule, ref ItemSlotInsertAttemptEvent args)
    {
        if (HasComp<AnomalyCapsuleCoreComponent>(args.Item))
            return; // Cores are fine... whitelists handle them.

        if (!TryComp<AnomalyCapsuleModuleComponent>(args.Item, out var moduleComp))
            return; // Should also be caught by the whitelists.

        foreach (var module in GetModules(capsule))
        {
            if (!_whitelist.IsWhitelistPassOrNull(module.Comp.Whitelist, args.Item))
            {
                args.Cancelled = true;
                PopupInsertFailure(capsule);
                return;
            }

            if (!_whitelist.IsWhitelistFailOrNull(module.Comp.Blacklist, args.Item))
            {
                args.Cancelled = true;
                PopupInsertFailure(capsule);
                return;
            }
        }
    }

    private void PopupInsertFailure(Entity<AnomalyCapsuleComponent> capsule)
    {
        if (!_net.IsClient || !_timing.IsFirstTimePredicted)
            return;

        _popup.PopupEntity(Loc.GetString("capsule-insert-module-failed"), capsule.Owner);
    }

    public bool HasCore(Entity<AnomalyCapsuleComponent> capsule) => TryGetCore(capsule, out _);

    public bool TryGetCore(Entity<AnomalyCapsuleComponent> capsule, out Entity<AnomalyCapsuleCoreComponent> core)
    {
        core = default!;

        if (!_slots.TryGetSlot(capsule.Owner, capsule.Comp.CoreSlot, out var slot) ||
            slot.Item is not { } coreUid ||
            !TryComp<AnomalyCapsuleCoreComponent>(coreUid, out var coreComp))
            return false;

        core = (coreUid, coreComp);
        return true;
    }

    public bool TryGetAnomalyPrototype(Entity<AnomalyCapsuleComponent> capsule, out AnomalyPrototype anomalyPrototype, RandomTableStateComponent? state = null)
    {
        anomalyPrototype = default!;
        if (!TryGetCore(capsule, out var core))
            return false;

        var run = _randomTable.RunPrototype<AnomalyPrototype>(core.Comp.AnomalyPool, state: state);
        if (run.Count() <= 0)
            return false;

        anomalyPrototype = run.First();
        return true;
    }

    /// <summary>
    /// Sends the provided event and args to all modules in the capsule.
    /// </summary>
    public void RelayEventToModules<TEvent>(Entity<AnomalyCapsuleComponent> capsule, ref TEvent args) where TEvent : EntityEventArgs
    {
        foreach (var module in GetModules(capsule))
        {
            RaiseLocalEvent(module.Owner, args);
        }
    }

    private void SubscribeModuleRelayEvent<TEvent>() where TEvent : EntityEventArgs
    {
        SubscribeLocalEvent<AnomalyCapsuleComponent, TEvent>(RelayEventToModules);
    }

    /// <summary>
    /// Retrieves all modules in the declares module slot ids.
    /// </summary>
    public IEnumerable<Entity<AnomalyCapsuleModuleComponent>> GetModules(Entity<AnomalyCapsuleComponent> capsule)
    {
        foreach (var moduleSlot in capsule.Comp.ModuleSlots)
        {
            if (!_slots.TryGetSlot(capsule.Owner, moduleSlot, out var slot) ||
                slot.Item is not { } moduleUid ||
                !TryComp<AnomalyCapsuleModuleComponent>(moduleUid, out var moduleComp))
                continue;

            yield return (moduleUid, moduleComp);
        }
    }
}