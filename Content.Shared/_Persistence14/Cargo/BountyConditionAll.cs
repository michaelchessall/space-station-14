using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared._Persistence14.Cargo;

/// <summary>
/// A binary bounty condition. Returns 1 if all child conditions are met. Otherwise 0.
/// </summary>
public sealed partial class BountyConditionAll : BountyCondition
{
    [DataField]
    public BountyCondition[] Conditions = [];

    [DataField]
    public float Threshold = 1f;

    public BountyConditionAll(params BountyCondition[] conditions)
    {
        Conditions = conditions;
    }

    /// <inheritdoc/>
    public override float CheckCondition(EntityUid containerUid, IEntityManager entityManager)
    {
        foreach (var condition in Conditions)
        {
            if (condition.CheckCondition(containerUid, entityManager) < Threshold)
                return 0f;
        }

        return 1f;
    }

    public override IEnumerable<string> GetManifestEntry(IEntityManager entityManager, IPrototypeManager prototypeManager)
    {
        foreach (var condition in Conditions)
            foreach (var entry in condition.GetManifestEntry(entityManager, prototypeManager))
                yield return entry;
    }
}