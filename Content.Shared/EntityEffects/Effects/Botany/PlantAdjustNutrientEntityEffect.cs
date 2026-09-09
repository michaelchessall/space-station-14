using Robust.Shared.Prototypes;
using Content.Shared.Botany;
using Content.Shared.FixedPoint;

namespace Content.Shared.EntityEffects.Effects.Botany;

/// <summary>
/// A type of <see cref="EntityEffectBase{T}"/> which modifies the nutrient of a Seed in a PlantHolder.
/// These are not modified by scale as botany has no concept of scale.
/// </summary>
/// <typeparam name="T">The effect inheriting this BaseEffect</typeparam>
/// <inheritdoc cref="EntityEffect"/>
public sealed partial class PlantAdjustNutrient : EntityEffectBase<PlantAdjustNutrient>
{
    /// <summary>
    /// How much we're adjusting the given nutrient by.
    /// </summary>
    [DataField(required: true)]
    public FixedPoint2 Amount { get; private set; } = 1;

    /// <summary>
    /// The given nutrient
    /// </summary>
    [DataField(required: true)]
    public ProtoId<PlantNutrientPrototype> Nutrient { get; set; }

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return prototype.Resolve(Nutrient, out PlantNutrientPrototype? proto)
            ? Loc.GetString("entity-effect-guidebook-plant-nutrient",
                ("nutrient", Loc.GetString(proto.LocalizedName)),
                ("amount", Amount.ToString()),
                ("color", proto.SubstanceColor),
                ("chance", Probability))
            : null;

    }
}
