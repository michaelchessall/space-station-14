using Content.Shared.Hands.Components;
using Content.Shared.Inventory.Events;

namespace Content.Shared.Hands.EntitySystems;

public sealed class ExtraHandsEquipmentSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExtraHandsEquipmentComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ExtraHandsEquipmentComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(Entity<ExtraHandsEquipmentComponent> ent, ref GotEquippedEvent args)
    {
        if (!TryComp<HandsComponent>(args.Equipee, out var handsComp))
            return;

        foreach (var (handName, hand) in ent.Comp.Hands)
        {
            var handId = $"{handName}";
            _hands.AddHand((args.Equipee, handsComp), handId, hand.Location);
        }
    }

    private void OnUnequipped(Entity<ExtraHandsEquipmentComponent> ent, ref GotUnequippedEvent args)
    {
        if (!TryComp<HandsComponent>(args.Equipee, out var handsComp))
            return;

        foreach (var handName in ent.Comp.Hands.Keys)
        {
            var handId = $"{handName}";
            _hands.RemoveHand((args.Equipee, handsComp), handId);
        }
    }
}
