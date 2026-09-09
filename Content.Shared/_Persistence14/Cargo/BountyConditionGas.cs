using Content.Shared.Atmos;
using Content.Shared.Atmos.Piping.Unary.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Persistence14.Cargo;

public sealed partial class BountyConditionGas : BountyCondition
{
    [DataField(readOnly: true)]
    public Gas Gas;

    [DataField(required: true)]
    public float Quantity = 1f;

    /// <inheritdoc/>
    public override float CheckCondition(EntityUid containerUid, IEntityManager entityManager)
    {
        var quantity = 0f;

        foreach (var uid in EnumerateContainedEntities(containerUid, entityManager))
        {
            if (!entityManager.TryGetComponent<GasCanisterComponent>(uid, out var canisterComponent))
                continue;

            quantity += canisterComponent.Air.GetMoles(Gas);

            if (quantity >= Quantity)
                return 1f;
        }

        return Math.Clamp(quantity / Quantity, 0f, 1f);
    }

    /// <inheritdoc/>
    public override IEnumerable<string> GetManifestEntry(IEntityManager entityManager, IPrototypeManager prototypeManager)
    {
        yield return Loc.GetString("bounty-condition-gas", ("gas", Gas), ("moles", Quantity));
    }

    /// <inheritdoc/>
    public override bool TryGetPricePer(float totalPrice, out float pricePer, out string unitName)
    {
        pricePer = totalPrice / Quantity;
        unitName = Loc.GetString("bounty-condition-unit-mole");
        return true;
    }
}