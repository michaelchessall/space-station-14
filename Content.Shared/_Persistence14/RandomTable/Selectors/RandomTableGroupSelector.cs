using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Persistence14.RandomTable.Selectors;

/// <summary>
/// Selects an item from the children of the selector base
/// </summary>
public sealed partial class RandomTableGroupSelector : RandomTableSelector
{
    [DataField]
    public List<RandomTableSelector> Children = new();

    /// <inheritdoc/>
    protected override IEnumerable<RandomTableValueDefinition> RunImplementation(RandomTableContext ctx)
    {
        var totalWeight = SumWeights(ctx, out var activeChildren, useConditions: true);
        if (totalWeight <= 0f) // If there are no valid children do nothing.
            yield break;

        var rand = ctx.Random.NextFloat() * totalWeight;
        var acc = 0f;

        foreach (var child in activeChildren)
        {
            acc += child.Weight;
            if (acc > rand)
            {
                foreach (var item in child.Run(ctx))
                    yield return item;

                yield break;
            }
        }
    }

    /// <inheritdoc/>
    public override IEnumerable<(RandomTableValueDefinition value, float prob)> ListImplementation(RandomTableContext ctx, float probabilityMultipler = 1f)
    {
        var totalWeight = SumWeights(ctx, out var activeChildren, useConditions: false);
        if (totalWeight <= 0) totalWeight = 1; // Literally just idiot proofing this...

        foreach (var child in activeChildren)
        {
            var childProbability = child.Weight / totalWeight;
            foreach (var (value, prob) in child.ListImplementation(ctx, probabilityMultipler * childProbability))
                yield return (value, prob);
        }
    }

    /// <summary>
    /// Sums the weights of any active children. For utility, provides the list of active children as an output variable.
    /// </summary>
    private float SumWeights(RandomTableContext ctx, out List<RandomTableSelector> activeChildren, bool useConditions = false)
    {
        activeChildren = new();

        if (Children is null) return 0f;
        float sum = 0f;


        foreach (var child in Children)
        {
            if (child is null)
                continue;
            var valid = child.CheckConditions(ctx) || !useConditions;

            if (!useConditions || child.CheckConditions(ctx)) // Ignore inactive children.
            {
                sum += child.Weight;
                activeChildren.Add(child);
            }
        }

        return sum;
    }
}