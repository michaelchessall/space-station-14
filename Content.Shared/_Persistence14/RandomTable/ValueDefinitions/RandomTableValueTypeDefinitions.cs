using System.Diagnostics.CodeAnalysis;

namespace Content.Shared._Persistence14.RandomTable.ValueDefinition;

public sealed partial class RandomTableStringValueDefinition : RandomTableValueDefinition
{
    [DataField("value")]
    private string _value = default!;

    /// <inheritdoc/>
    protected override object Get(RandomTableContext ctx) => _value;

    public RandomTableStringValueDefinition(string val)
    {
        _value = val;
    }
    public RandomTableStringValueDefinition() { }

    public static implicit operator RandomTableStringValueDefinition(string val) => new RandomTableStringValueDefinition(val);
    public static implicit operator string(RandomTableStringValueDefinition def) => def._value;

    public override int GetHashCode() => _value.GetHashCode();
}

public sealed partial class RandomTableIntValueDefinition : RandomTableValueDefinition
{
    [DataField("value")]
    private int _value = default!;

    /// <inheritdoc/>
    protected override object Get(RandomTableContext ctx) => _value;

    public RandomTableIntValueDefinition(int val)
    {
        _value = val;
    }
    public RandomTableIntValueDefinition() { }

    public static implicit operator RandomTableIntValueDefinition(int val) => new RandomTableIntValueDefinition(val);
    public static implicit operator int(RandomTableIntValueDefinition def) => def._value;

    /// <inheritdoc/>
    protected override bool OverrideTypeCondition<T>(RandomTableContext ctx, [NotNullWhen(true)] out T t)
    {
        t = default!;

        object? converted;

        if (typeof(T) == typeof(float))
            converted = (float)_value;
        else if (typeof(T) == typeof(double))
            converted = (double)_value;
        else if (typeof(T) == typeof(long))
            converted = (long)_value;
        else if (typeof(T) == typeof(short))
            converted = (short)_value;
        else
            return false;

        t = (T)converted;
        return true;
    }

    public override int GetHashCode() => _value.GetHashCode();

}

public sealed partial class RandomTableFloatValueDefinition : RandomTableValueDefinition
{
    [DataField("value")]
    private float _value = default!;

    /// <inheritdoc/>
    protected override object Get(RandomTableContext ctx) => _value;

    public RandomTableFloatValueDefinition(float val)
    {
        _value = val;
    }
    public RandomTableFloatValueDefinition() { }

    public static implicit operator RandomTableFloatValueDefinition(float val) => new RandomTableFloatValueDefinition(val);
    public static implicit operator float(RandomTableFloatValueDefinition def) => def._value;

    /// <inheritdoc/>
    protected override bool OverrideTypeCondition<T>(RandomTableContext ctx, [NotNullWhen(true)] out T t)
    {
        t = default!;

        object? converted;

        if (typeof(T) == typeof(int))
            converted = (int)_value;
        else if (typeof(T) == typeof(double))
            converted = (double)_value;
        else if (typeof(T) == typeof(long))
            converted = (long)_value;
        else if (typeof(T) == typeof(short))
            converted = (short)_value;
        else
            return false;

        t = (T)converted;
        return true;
    }

    public override int GetHashCode() => _value.GetHashCode();
}
