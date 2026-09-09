using Content.Server.Audio;
using Content.Server.Power.EntitySystems;
using Content.Shared.Construction;
using Content.Shared.Construction.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Power;
using Robust.Shared.Timing;

namespace Content.Server.Construction;

/// <inheritdoc/>
public sealed class FlatpackSystem : SharedFlatpackSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FlatpackCreatorComponent, FlatpackCreatorStartPackBuiMessage>(OnStartPack);
        SubscribeLocalEvent<FlatpackCreatorComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnStartPack(Entity<FlatpackCreatorComponent> ent, ref FlatpackCreatorStartPackBuiMessage args)
    {
        TryStartPack(ent);
    }

    /// <summary>
    /// Begins packing the board currently in the creator's slot, if powered, idle and the materials are covered.
    /// </summary>
    public bool TryStartPack(Entity<FlatpackCreatorComponent> ent)
    {
        var (uid, comp) = ent;
        if (!this.IsPowered(ent, EntityManager) || comp.Packing)
            return false;

        if (!_itemSlots.TryGetSlot(uid, comp.SlotId, out var itemSlot) || itemSlot.Item is not { } board)
            return false;

        if (!TryGetFlatpackCreationCost(ent, board, out var cost))
            return false;

        if (!MaterialStorage.CanChangeMaterialAmount(uid, cost))
            return false;

        _itemSlots.SetLock(uid, comp.SlotId, true);
        comp.Packing = true;
        comp.PackEndTime = _timing.CurTime + comp.PackDuration;
        Appearance.SetData(uid, FlatpackCreatorVisuals.Packing, true);
        _ambientSound.SetAmbience(uid, true);
        Dirty(uid, comp);
        return true;
    }

    /// <summary>
    /// Inserts a freshly-made board into the creator and immediately starts packing it. Used to automate
    /// flatpacking from another machine. Returns false (and ejects the board) if it couldn't start.
    /// </summary>
    public bool TryPackBoard(Entity<FlatpackCreatorComponent> ent, EntityUid board)
    {
        if (ent.Comp.Packing)
            return false;

        if (!_itemSlots.TryInsert(ent.Owner, ent.Comp.SlotId, board, null))
            return false;

        if (TryStartPack(ent))
            return true;

        _itemSlots.TryEject(ent.Owner, ent.Comp.SlotId, null, out _);
        return false;
    }

    /// <summary>True if the creator is powered off nothing and its board slot is empty — ready to pack.</summary>
    public bool IsIdle(Entity<FlatpackCreatorComponent> ent)
    {
        if (ent.Comp.Packing)
            return false;

        return !_itemSlots.TryGetSlot(ent.Owner, ent.Comp.SlotId, out var slot) || slot.Item == null;
    }

    private void OnPowerChanged(Entity<FlatpackCreatorComponent> ent, ref PowerChangedEvent args)
    {
        if (args.Powered)
            return;
        FinishPacking(ent, true);
    }

    private void FinishPacking(Entity<FlatpackCreatorComponent> ent, bool interrupted)
    {
        var (uid, comp) = ent;

        _itemSlots.SetLock(uid, comp.SlotId, false);
        comp.Packing = false;
        Appearance.SetData(uid, FlatpackCreatorVisuals.Packing, false);
        _ambientSound.SetAmbience(uid, false);
        Dirty(uid, comp);

        if (interrupted)
            return;

        if (!_itemSlots.TryGetSlot(uid, comp.SlotId, out var itemSlot) || itemSlot.Item is not { } board)
            return;

        if (!TryGetFlatpackCreationCost(ent, board, out var cost) ||
            !TryGetFlatpackResultPrototype(board, out var proto))
            return;

        if (!MaterialStorage.TryChangeMaterialAmount((ent, null), cost))
            return;

        var flatpack = Spawn(comp.BaseFlatpackPrototype, Transform(ent).Coordinates);
        SetupFlatpack(flatpack, proto.Value, board);
        Del(board);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FlatpackCreatorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Packing)
                continue;

            if (_timing.CurTime < comp.PackEndTime)
                continue;

            FinishPacking((uid, comp), false);
        }
    }
}
