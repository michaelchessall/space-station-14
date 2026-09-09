using Content.Shared._Persistence14.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Persistence14.Cargo;

/// <summary>
/// A bounty condition verifying the quantity of a particular reagent in a container.
/// </summary>
public sealed partial class BountyConditionReagent : BountyCondition
{
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    [DataField(required: true)]
    public FixedPoint2 Quantity;

    /// <inheritdoc/>
    public override float CheckCondition(EntityUid containerUid, IEntityManager entityManager)
    {
        var solutionSystem = entityManager.System<SharedSolutionContainerSystem>();

        var quantity = FixedPoint2.Zero;

        foreach (var uid in EnumerateContainedEntities(containerUid, entityManager))
        {
            if (!entityManager.TryGetComponent<BountySolutionComponent>(uid, out var bountySolution))
                continue;

            if (!solutionSystem.TryGetSolution(uid, bountySolution.Solution, out var solutionEnt, out var solution))
                continue;

            quantity += solution.GetTotalPrototypeQuantity(Reagent);
            if (quantity > Quantity)
                return 1f;
        }

        return Math.Clamp((float)(quantity / Quantity), 0f, 1f);
    }

    /// <inheritdoc/>
    public override IEnumerable<string> GetManifestEntry(IEntityManager entityManager, IPrototypeManager prototypeManager)
    {
        yield return Loc.GetString("bounty-condition-reagent", ("reagent", prototypeManager.Index(Reagent).LocalizedName), ("quantity", Quantity));
    }

    /// <inheritdoc/>
    public override bool TryGetPricePer(float totalPrice, out float pricePer, out string unitName)
    {
        pricePer = totalPrice / (float)Quantity;
        unitName = Loc.GetString("bounty-condition-unit-unit");
        return true;
    }
}