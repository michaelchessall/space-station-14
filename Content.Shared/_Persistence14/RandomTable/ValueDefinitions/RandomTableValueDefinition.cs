using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace Content.Shared._Persistence14.RandomTable;

[ImplicitDataDefinitionForInheritors, UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract partial class RandomTableValueDefinition : RandomTableSelector
{
    /// <summary>
    /// A simple getter for the defined value.
    /// </summary>
    protected abstract object? Get(RandomTableContext ctx);

    /// <summary>
    /// Attempts to retrieve the value. Returns true if value is not null and <see cref="TryGetCondition"/> (if defined) is true. 
    /// </summary>
    public virtual bool TryGet<T>(RandomTableContext ctx, [NotNullWhen(true)] out T? value)
    {
        value = default;

        var untyped = Get(ctx);
        if (untyped is not T typed)
            if (!OverrideTypeCondition(ctx, out typed))
                return false;

        if (!TryGetCondition(ctx, typed))
            return false;

        value = typed;
        return true;
    }

    /// <summary>
    /// An addtional condition on the TryGet method given some context and the returned value.
    /// </summary>
    protected virtual bool TryGetCondition<T>(RandomTableContext ctx, T? t) { return true; }

    /// <summary>
    /// An override for the simple untyped is not T check that allows for more involved conversions. Used for truncation and decimalification of number conversions.
    /// </summary>
    protected virtual bool OverrideTypeCondition<T>(RandomTableContext ctx, [NotNullWhen(true)] out T t) { t = default!; return false; }

    protected override IEnumerable<RandomTableValueDefinition> RunImplementation(RandomTableContext ctx)
    {
        yield return this;
    }

    public override IEnumerable<(RandomTableValueDefinition value, float prob)> ListImplementation(RandomTableContext ctx, float probabilityMultiplier = 1)
    {
        yield return (this, probabilityMultiplier);
    }

    public abstract override int GetHashCode();
}