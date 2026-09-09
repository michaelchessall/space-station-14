using Content.Server.Botany.Components;
using Content.Shared.EntityEffects;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Botany;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Botany.Systems;

public sealed partial class BotanySystem
{
    [Dependency] private readonly SharedEntityEffectsSystem _entityEffects = default!;

    public void ProduceGrown(EntityUid uid, ProduceComponent produce, Dictionary<ProtoId<PlantNutrientPrototype>, FixedPoint2> nutrients)
    {
        if (!TryGetSeed(produce, out var seed))
            return;

        foreach (var mutation in seed.Mutations)
        {
            if (mutation.AppliesToProduce)
                _entityEffects.TryApplyEffect(uid, mutation.Effect);
        }

        if (!_solutionContainerSystem.EnsureSolution(uid,
                produce.SolutionName,
                out var solutionContainer,
                FixedPoint2.Zero))
            return;

        solutionContainer.RemoveAllSolution();

        Dictionary<ProtoId<PlantNutrientPrototype>, FixedPoint2> bonusRatio = new();

        foreach (var nutrient in seed.TotalRequirements)
        {
            var required = nutrient.Value.Requirement;
            var bonus = nutrient.Value.BonusRequirement;
            if (bonus > 0)
                bonusRatio.Add(nutrient.Key, FixedPoint2.Clamp((nutrients.GetValueOrDefault(nutrient.Key) - required) / bonus, 0, 1));
        }

        foreach (var (chem, quantity) in seed.Chemicals)
        {
            var amount = quantity.BaseAmount;
            foreach (var requirement in quantity.Requirements)
            {
                if (requirement.Value.BonusAmount > 0 && bonusRatio.TryGetValue(requirement.Key, out var ratio))
                {
                    amount += requirement.Value.BonusAmount * ratio;
                }
            }
            solutionContainer.MaxVolume += amount;
            solutionContainer.AddReagent(chem, amount);
        }
    }

    public void OnProduceExamined(EntityUid uid, ProduceComponent comp, ExaminedEvent args)
    {
        if (comp.Seed == null)
            return;

        using (args.PushGroup(nameof(ProduceComponent)))
        {
            foreach (var m in comp.Seed.Mutations)
            {
                // Don't show mutations that have no effect on produce (sentience)
                if (!m.AppliesToProduce)
                    continue;

                if (m.Description != null)
                    args.PushMarkup(Loc.GetString(m.Description));
            }
        }
    }
}
