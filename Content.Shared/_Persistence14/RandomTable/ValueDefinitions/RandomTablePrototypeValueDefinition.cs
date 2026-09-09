using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Prototypes;

namespace Content.Shared._Persistence14.RandomTable.ValueDefinition;

public sealed partial class RandomTablePrototypeValueDefinition : RandomTableValueDefinition
{
    [DataField("prototype")]
    private string _protoId = default!;

    protected override object? Get(RandomTableContext ctx) => _protoId;

    protected override bool OverrideTypeCondition<T>(RandomTableContext ctx, [NotNullWhen(true)] out T t)
    {
        t = default!;

        if (!typeof(T).IsAssignableTo(typeof(IPrototype)))
            return false;

        if (ctx.PrototypeManager.TryIndex(typeof(T), _protoId, out var prototype))
        {
            t = (T)prototype;
            return true;
        }
        return false;
    }

    public RandomTablePrototypeValueDefinition(string val)
    {
        _protoId = val;
    }
    public RandomTablePrototypeValueDefinition() { }

    public static implicit operator RandomTablePrototypeValueDefinition(string val) => new RandomTablePrototypeValueDefinition(val);
    public static implicit operator string(RandomTablePrototypeValueDefinition def) => def._protoId;

    public override int GetHashCode() => _protoId.GetHashCode();
}