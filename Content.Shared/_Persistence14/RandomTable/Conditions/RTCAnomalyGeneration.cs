using Content.Shared._Persistence14.Research.Anomalies;

namespace Content.Shared._Persistence14.RandomTable.Conditions;

public sealed partial class RTCAnomalyGeneration : RandomTableCondition
{
    [DataField("category", required: true)]
    private AnomalyCategory _requiredCategory;

    public const string ContextKey = "anomaly-generation-category";

    protected override bool EvaluateImplementation(RandomTableSelector selector, RandomTableContext ctx)
    {
        if (ctx.State is null || !ctx.State.Data.TryGetValue(ContextKey, out var data))
            return true;

        if (!data.TryGetInt(out var requiredCategory))
            return false;

        return requiredCategory == (int)_requiredCategory;
    }
}