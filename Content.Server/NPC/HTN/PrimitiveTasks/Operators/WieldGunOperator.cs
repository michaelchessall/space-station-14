using Content.Server.Weapons.Ranged.Systems;
using Content.Server.Wieldable;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Wieldable.Components;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Wields (two-hands) the NPC's currently-held/innate gun if needed. Guns with
/// GunRequiresWieldComponent (shotguns, rifles, snipers, launchers, bows, some battery guns)
/// cannot fire at all unless wielded - SharedWieldableSystem.OnShootAttempt cancels every shot
/// otherwise, independent of ammo/bolt/LOS. Nothing in the stock ranged-combat HTN chain ever
/// wields a gun.
///
/// Wielding needs enough OTHER free hands beyond the one holding the gun
/// (WieldableComponent.FreeHandsRequired) - ClearActiveHandCompound only frees the active hand,
/// which by this point already holds the gun, so it doesn't help here.
///
/// UnwieldAll runs first to clear any existing wield state the stock way (each wielded item's own
/// TryUnwield, which properly deletes its virtual item and frees the hand). The fallback
/// hand-clearing loop below explicitly skips anything still flagged as a virtual item - dropping
/// one directly bypasses that cleanup and desyncs the original item's Wielded flag from what the
/// hand actually shows.
///
/// Update()-only, not Plan()-based: HTNOperator.Plan runs during PLANNING, which the planner can
/// speculatively backtrack out of (see HTNPlanJob.RestoreTolastDecomposedTask), rolling back
/// blackboard state but not real, already-performed side effects. Update() only runs once a plan
/// is actually committed.
/// </summary>
public sealed partial class WieldGunOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var gunSystem = _entManager.System<GunSystem>();
        var wieldSystem = _entManager.System<WieldableSystem>();
        var handsSystem = _entManager.System<SharedHandsSystem>();

        if (!gunSystem.TryGetGun(owner, out var gun))
            return HTNOperatorStatus.Finished;

        if (!_entManager.TryGetComponent<WieldableComponent>(gun.Owner, out var wieldable))
            return HTNOperatorStatus.Finished;

        if (wieldable.Wielded)
            return HTNOperatorStatus.Finished;

        if (_entManager.TryGetComponent<HandsComponent>(owner, out var hands))
        {
            // Clear any existing wield state the stock way first, rather than this operator ever
            // touching a virtual item itself.
            wieldSystem.UnwieldAll((owner, hands), force: true);

            var freeable = handsSystem.CountFreeableHands((owner, hands), except: gun.Owner);

            if (freeable < wieldable.FreeHandsRequired)
            {
                foreach (var held in handsSystem.EnumerateHeld((owner, hands)))
                {
                    if (freeable >= wieldable.FreeHandsRequired)
                        break;

                    if (held == gun.Owner)
                        continue;

                    // Never drop a virtual item directly - see class doc.
                    if (_entManager.HasComponent<VirtualItemComponent>(held))
                        continue;

                    if (handsSystem.TryDrop((owner, hands), held))
                        freeable++;
                }
            }
        }

        wieldSystem.TryWield(gun.Owner, wieldable, owner);

        return HTNOperatorStatus.Finished;
    }
}
