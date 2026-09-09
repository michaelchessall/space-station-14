using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Weapons.Ranged.Systems;
using Content.Server.Wieldable;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Whitelist;
using Content.Shared.Wieldable.Components;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Reloads the NPC's currently-held/innate gun if it's completely out of ammo. Tries three
/// strategies in order, covering every ammo provider type in the game:
///
///   1. ItemSlots swap - Magazine/ChamberMagazine/some Ballistic guns with a detachable magazine.
///      Ejects the spent one (if any) and inserts a compatible, ammo-bearing replacement.
///
///   2. Per-shell Ballistic insert - internal-tube guns (BallisticAmmoProviderComponent, no
///      ItemSlots, e.g. several shotgun variants). Inserts every compatible round found, one at a
///      time, until full or out of rounds.
///
///   3. Revolver insert - RevolverAmmoProviderComponent. Prefers a speedloader (fills multiple
///      chambers at once) over individual rounds.
///
/// Battery/BasicEntity/Container/Solution-based guns correctly find nothing via any of these
/// three and fail below - none of those are manually reloadable by an NPC.
///
/// Two-handed (Wieldable) guns get unwielded before insertion and rewielded afterward regardless
/// of outcome: TryWield occupies the off-hand with a virtual item, leaving no free hand to hold
/// whatever's being inserted.
///
/// Plan() is deliberately SEARCH-ONLY with no side effects - it only tells the planner whether
/// this branch is viable, so an NPC with a permanently dry gun and no spare ammo fails cleanly
/// instead of re-matching this same branch forever. The actual mutation (unwield, eject, insert,
/// rewield) only happens in Update(), because Plan() runs during PLANNING and can be
/// speculatively backtracked out of (see HTNPlanJob.RestoreTolastDecomposedTask), which rolls
/// back blackboard state but not any real side effect Plan() already performed.
///
/// Unwielding and rewielding the SAME gun within a single Update() call is unsafe: unwielding
/// queues its virtual item's deletion (SharedVirtualItemSystem.DeleteVirtualItem ->
/// PredictedQueueDel), which isn't flushed until the entity manager's deferred-deletion queue
/// runs at end of tick - so the item is still physically in the hand when TryWield is called
/// again in the same tick, which desyncs the Wielded flag and leaves a stale virtual item stuck
/// in the hand permanently. Fixed by never unwielding and rewielding in one call: this operator
/// unwields and returns Continuing, deferring the actual reload + rewield to the NEXT tick (see
/// PendingRewieldKey) once the deletion has flushed and the hand is genuinely empty.
/// </summary>
public sealed partial class ReloadGunOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    /// <summary>
    /// Marks that we've unwielded the gun this task and are waiting a tick before reloading +
    /// rewielding it - see the class doc for why that tick has to elapse.
    /// </summary>
    private const string PendingRewieldKey = "ReloadGunPendingRewield";

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var gunSystem = _entManager.System<GunSystem>();
        var itemSlots = _entManager.System<ItemSlotsSystem>();
        var inventory = _entManager.System<InventorySystem>();
        var whitelistSystem = _entManager.System<EntityWhitelistSystem>();

        if (!gunSystem.TryGetGun(owner, out var gun))
            return (false, null);

        var gunAmmoEv = new GetAmmoCountEvent();
        _entManager.EventBus.RaiseLocalEvent(gun.Owner, ref gunAmmoEv);

        // Still has ammo - nothing to reload yet, leave it to keep fighting.
        if (gunAmmoEv.Count > 0)
            return (true, null);

        var canReload = CanItemSlotsReload(gun.Owner, owner, itemSlots, inventory, whitelistSystem) ||
                         CanBallisticReload(gun.Owner, owner, inventory, whitelistSystem) ||
                         CanRevolverReload(gun.Owner, owner, inventory, whitelistSystem);

        // Empty, and nothing found to reload it with - fail outright so the planner moves on
        // rather than getting stuck fighting with a gun that can never fire.
        return (canReload, null);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var gunSystem = _entManager.System<GunSystem>();
        var wieldSystem = _entManager.System<WieldableSystem>();
        var itemSlots = _entManager.System<ItemSlotsSystem>();
        var inventory = _entManager.System<InventorySystem>();
        var whitelistSystem = _entManager.System<EntityWhitelistSystem>();

        if (!gunSystem.TryGetGun(owner, out var gun))
        {
            blackboard.Remove<bool>(PendingRewieldKey);
            return HTNOperatorStatus.Finished;
        }

        // Second tick: the deferred unwield deletion from last tick has now flushed, safe to
        // reload and rewield - see the class doc.
        if (blackboard.TryGetValue<bool>(PendingRewieldKey, out var pendingRewield, _entManager) && pendingRewield)
        {
            blackboard.Remove<bool>(PendingRewieldKey);

            _ = TryItemSlotsReload(gun.Owner, owner, itemSlots, inventory, whitelistSystem) ||
                TryBallisticReload(gun.Owner, owner, gunSystem, inventory, whitelistSystem) ||
                TryRevolverReload(gun.Owner, owner, gunSystem, inventory, whitelistSystem);

            if (_entManager.TryGetComponent<WieldableComponent>(gun.Owner, out var rewieldable))
                wieldSystem.TryWield(gun.Owner, rewieldable, owner);

            return HTNOperatorStatus.Finished;
        }

        var gunAmmoEv = new GetAmmoCountEvent();
        _entManager.EventBus.RaiseLocalEvent(gun.Owner, ref gunAmmoEv);

        if (gunAmmoEv.Count > 0)
            return HTNOperatorStatus.Finished;

        var wasWielded = _entManager.TryGetComponent<WieldableComponent>(gun.Owner, out var wieldable) &&
                          wieldable.Wielded;

        if (!wasWielded)
        {
            // Nothing wielded to race against - reload immediately, no need to wait a tick.
            _ = TryItemSlotsReload(gun.Owner, owner, itemSlots, inventory, whitelistSystem) ||
                TryBallisticReload(gun.Owner, owner, gunSystem, inventory, whitelistSystem) ||
                TryRevolverReload(gun.Owner, owner, gunSystem, inventory, whitelistSystem);

            return HTNOperatorStatus.Finished;
        }

        wieldSystem.TryUnwield(gun.Owner, wieldable!, owner);
        blackboard.SetValue(PendingRewieldKey, true);
        return HTNOperatorStatus.Continuing;
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);
        blackboard.Remove<bool>(PendingRewieldKey);
    }

    // ---- Plan()-time, read-only viability checks (no side effects) ----

    private bool CanItemSlotsReload(EntityUid gun, EntityUid owner, ItemSlotsSystem itemSlots,
        InventorySystem inventory, EntityWhitelistSystem whitelistSystem)
    {
        if (!_entManager.TryGetComponent<ItemSlotsComponent>(gun, out var slots))
            return false;

        foreach (var (_, slot) in slots.Slots)
        {
            if (slot.Whitelist == null)
                continue;

            var replacement = FindOne(owner, slot.Whitelist, whitelistSystem, inventory);

            // Mirrors TryItemSlotsReload's own CanInsert pre-check - otherwise Update() could keep
            // failing to actually reload while Plan() keeps saying it's possible.
            if (replacement != null && itemSlots.CanInsert(gun, replacement.Value, owner, slot, swap: true))
                return true;
        }

        return false;
    }

    private bool CanBallisticReload(EntityUid gun, EntityUid owner, InventorySystem inventory,
        EntityWhitelistSystem whitelistSystem)
    {
        return _entManager.TryGetComponent<BallisticAmmoProviderComponent>(gun, out var ballistic) &&
               ballistic.Whitelist != null &&
               FindOne(owner, ballistic.Whitelist, whitelistSystem, inventory) != null;
    }

    private bool CanRevolverReload(EntityUid gun, EntityUid owner, InventorySystem inventory,
        EntityWhitelistSystem whitelistSystem)
    {
        return _entManager.TryGetComponent<RevolverAmmoProviderComponent>(gun, out var revolver) &&
               revolver.Whitelist != null &&
               FindOne(owner, revolver.Whitelist, whitelistSystem, inventory) != null;
    }

    // ---- Update()-time, real mutation ----

    private bool TryItemSlotsReload(EntityUid gun, EntityUid owner, ItemSlotsSystem itemSlots,
        InventorySystem inventory, EntityWhitelistSystem whitelistSystem)
    {
        if (!_entManager.TryGetComponent<ItemSlotsComponent>(gun, out var slots))
            return false;

        foreach (var (_, slot) in slots.Slots)
        {
            if (slot.Whitelist == null)
                continue;

            var replacement = FindOne(owner, slot.Whitelist, whitelistSystem, inventory);
            if (replacement == null)
                continue;

            // Verify the insert will actually succeed BEFORE ejecting the current magazine - the
            // real CanInsert check is stricter than the whitelist (e.g. ItemSlotInsertAttemptEvent
            // subscribers can cancel for reasons the whitelist alone doesn't capture), and ejecting
            // first would permanently strand the gun with no magazine at all if it then failed.
            // swap: true checks insertability as if the slot were already empty, without touching
            // it, so a failing candidate never costs us the one we have.
            if (!itemSlots.CanInsert(gun, replacement.Value, owner, slot, swap: true))
                continue;

            if (slot.HasItem)
                itemSlots.TryEject(gun, slot, owner, out _);

            if (itemSlots.TryInsert(gun, slot, replacement.Value, owner))
                return true;
        }

        return false;
    }

    private bool TryBallisticReload(EntityUid gun, EntityUid owner, GunSystem gunSystem,
        InventorySystem inventory, EntityWhitelistSystem whitelistSystem)
    {
        if (!_entManager.TryGetComponent<BallisticAmmoProviderComponent>(gun, out var ballistic) ||
            ballistic.Whitelist == null)
        {
            return false;
        }

        var insertedAny = false;

        foreach (var round in FindAll(owner, ballistic.Whitelist, whitelistSystem, inventory).ToList())
        {
            if (!gunSystem.TryBallisticInsert((gun, ballistic), round, owner))
                break; // full, or this candidate stopped qualifying between find and insert

            insertedAny = true;
        }

        return insertedAny;
    }

    private bool TryRevolverReload(EntityUid gun, EntityUid owner, GunSystem gunSystem,
        InventorySystem inventory, EntityWhitelistSystem whitelistSystem)
    {
        if (!_entManager.TryGetComponent<RevolverAmmoProviderComponent>(gun, out var revolver) ||
            revolver.Whitelist == null)
        {
            return false;
        }

        var insertedAny = false;

        // Speedloaders first - one insert can fill multiple chambers at once, so they're strictly
        // more efficient than loading round by round.
        var candidates = FindAll(owner, revolver.Whitelist, whitelistSystem, inventory)
            .OrderByDescending(candidate => _entManager.HasComponent<SpeedLoaderComponent>(candidate))
            .ToList();

        foreach (var round in candidates)
        {
            if (!gunSystem.TryRevolverInsert((gun, revolver), round, owner))
                continue; // this specific candidate didn't take (e.g. already-empty speedloader) - others might

            insertedAny = true;

            var ammoEv = new GetAmmoCountEvent();
            _entManager.EventBus.RaiseLocalEvent(gun, ref ammoEv);
            if (ammoEv.Capacity > 0 && ammoEv.Count >= ammoEv.Capacity)
                break;
        }

        return insertedAny;
    }

    // ---- Shared search helpers (read-only, safe to call from either Plan() or Update()) ----

    private EntityUid? FindOne(EntityUid owner, EntityWhitelist whitelist,
        EntityWhitelistSystem whitelistSystem, InventorySystem inventory)
    {
        return FindAll(owner, whitelist, whitelistSystem, inventory).FirstOrDefault();
    }

    private IEnumerable<EntityUid> FindAll(EntityUid owner, EntityWhitelist whitelist,
        EntityWhitelistSystem whitelistSystem, InventorySystem inventory)
    {
        foreach (var item in inventory.GetHandOrInventoryEntities(owner))
        {
            if (TryQualify(item, whitelist, whitelistSystem))
                yield return item;

            if (_entManager.TryGetComponent<StorageComponent>(item, out var storage))
            {
                foreach (var stored in storage.StoredItems.Keys)
                {
                    if (TryQualify(stored, whitelist, whitelistSystem))
                        yield return stored;
                }
            }
        }
    }

    private bool TryQualify(EntityUid candidate, EntityWhitelist whitelist, EntityWhitelistSystem whitelistSystem)
    {
        if (!whitelistSystem.IsValid(whitelist, candidate))
            return false;

        // A speedloader's own ammo count isn't meaningful until TakeAmmoEvent fires during
        // insertion, so skip the ammo check for it and let TryRevolverInsert reject an empty one.
        if (_entManager.HasComponent<SpeedLoaderComponent>(candidate))
            return true;

        var ammoEv = new GetAmmoCountEvent();
        _entManager.EventBus.RaiseLocalEvent(candidate, ref ammoEv);
        return ammoEv.Count > 0;
    }
}
