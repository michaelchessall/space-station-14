using Robust.Shared.Prototypes;

namespace Content.Shared._Persistence14.Cargo;

/// <summary>
/// A bounty condition which returns the weighted average of all children conditions.
/// </summary>
public sealed partial class BountyConditionWeighted : BountyCondition
{
    [DataField]
    public List<WeightedBountyCondition> Conditions;

    /// <inheritdoc/>
    public override float CheckCondition(EntityUid containerUid, IEntityManager entityManager)
    {
        var sumValid = 0f;
        var sumWeights = 0f;
        foreach (var (condition, weight) in Conditions)
        {
            sumWeights += weight;
            sumValid += condition.CheckCondition(containerUid, entityManager) * weight;
        }

        return Math.Clamp(sumValid / sumWeights, 0f, 1f);
    }

    public override IEnumerable<string> GetManifestEntry(IEntityManager entityManager, IPrototypeManager prototypeManager)
    {
        foreach (var condition in Conditions)
            foreach (var entry in condition.Condition.GetManifestEntry(entityManager, prototypeManager))
                yield return entry;
    }
}

[DataDefinition]
public sealed partial class WeightedBountyCondition
{
    [DataField(required: true)]
    public BountyCondition Condition;

    [DataField]
    public float Weight = 1f;

    public void Deconstruct(out BountyCondition condition, out float weight)
    {
        condition = Condition;
        weight = Weight;
    }
}