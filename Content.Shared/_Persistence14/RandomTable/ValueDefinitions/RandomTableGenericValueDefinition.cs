namespace Content.Shared._Persistence14.RandomTable.ValueDefinition;

public abstract partial class RandomTableGenericValueDefinition<T> : RandomTableValueDefinition
{
    [DataField("value", required: true)]
    private T? _t = default!;

    protected override object? Get(RandomTableContext ctx) => _t;

    public RandomTableGenericValueDefinition() { }
    public RandomTableGenericValueDefinition(T t)
    {
        _t = t;
    }

    public static implicit operator T(RandomTableGenericValueDefinition<T> def) => def._t ?? default!;
}