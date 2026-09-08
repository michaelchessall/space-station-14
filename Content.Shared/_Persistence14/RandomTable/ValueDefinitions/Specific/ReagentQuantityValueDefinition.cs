using Content.Shared.Chemistry.Reagent;

namespace Content.Shared._Persistence14.RandomTable.ValueDefinition;

public sealed partial class ReagentQuantityValueDefinition : RandomTableValueDefinition
{
    [DataField(required: true)]
    private ReagentQuantity _value;

    protected override object? Get(RandomTableContext ctx) => _value;
}