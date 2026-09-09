using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Persistence14.RandomTable.Selectors;

/// <summary>
/// An incredible simple "do nothing" selector that can be useful for the default table selector within <see cref="RandomTablePrototype"/>. 
/// </summary>
public sealed partial class RandomTableNullSelector : RandomTableSelector
{
    protected override IEnumerable<RandomTableValueDefinition> RunImplementation(RandomTableContext ctx)
    {
        yield break;
    }

    public override IEnumerable<(RandomTableValueDefinition, float)> ListImplementation(RandomTableContext ctx, float probabilityMultipler) 
    {
        yield break;
    }
}