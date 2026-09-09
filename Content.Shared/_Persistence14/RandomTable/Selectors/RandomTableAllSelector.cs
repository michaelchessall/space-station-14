using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Persistence14.RandomTable.Selectors;

/// <summary>
/// Returns all children items as valid items from the table.
/// </summary>
public sealed partial class RandomTableAllSelector : RandomTableSelector
{
    [DataField]
    public List<RandomTableSelector> Children = new();

    /// <inheritdoc/>
    protected override IEnumerable<RandomTableValueDefinition> RunImplementation(RandomTableContext ctx)
    {
        foreach (var child in Children)
            foreach (var item in child.Run(ctx))
                yield return item;
    }

    public override IEnumerable<(RandomTableValueDefinition value, float prob)> ListImplementation(RandomTableContext ctx, float probabilityMultipler = 1f)
    {
        foreach (var child in Children)
            foreach (var (value, prob) in child.ListImplementation(ctx, probabilityMultipler))
                yield return (value, prob);
    }
}