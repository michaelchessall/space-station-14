using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Persistence14.RandomTable.Selectors;

public sealed partial class RandomTableNestedSelector : RandomTableSelector
{
    [DataField(required: true)]
    public ProtoId<RandomTablePrototype> TableId = default!;

    protected override IEnumerable<RandomTableValueDefinition> RunImplementation(RandomTableContext ctx)
    {
        var table = ctx.PrototypeManager.Index(TableId).Table;

        foreach (var item in table.Run(ctx))
            yield return item;
    }

    public override IEnumerable<(RandomTableValueDefinition value, float prob)> ListImplementation(RandomTableContext ctx, float probabilityMultipler = 1f)
    {
        var table = ctx.PrototypeManager.Index(TableId).Table;

        foreach (var (value, prob) in table.ListImplementation(ctx, probabilityMultipler))
            yield return (value, prob);
    }
}