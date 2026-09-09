using System.Runtime.CompilerServices;
using Content.Shared._Persistence14.RandomTable.Count;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Persistence14.RandomTable;

[ImplicitDataDefinitionForInheritors, UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract partial class RandomTableSelector
{
    /// <summary>
    /// Weight used when picking between selectors.
    /// </summary>
    [DataField]
    public float Weight = 1f;

    /// <summary>
    /// The set of conditions which must be true for the selector to activate.
    /// </summary>
    [DataField]
    public List<RandomTableCondition> Conditions = new();

    [DataField]
    public CountSelector Rolls = new ConstantCountSelector(1);

    /// <summary>
    /// The evalutaion mode for the set of conditions. <br/>
    /// See <see cref="RandomTableConditionMode"/> for more info.<br/>
    /// Default set to All. 
    /// </summary>
    [DataField]
    public RandomTableConditionMode ConditionMode = RandomTableConditionMode.All;

    /// <summary>
    /// Runs the table selector grabing a random value based on the specific evaluation parameters of the child implementation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public IEnumerable<RandomTableValueDefinition> Run(RandomTableContext ctx)
    {
        if (!CheckConditions(ctx))
            yield break;

        var rolls = Rolls.Get(ctx);

        for (int i = 0; i < rolls; i++)
        {
            foreach (var item in RunImplementation(ctx))
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Child specific implementation of the Run command. Returns all accepted and valid members of the selector.
    /// </summary>
    protected abstract IEnumerable<RandomTableValueDefinition> RunImplementation(RandomTableContext ctx);

    /// <summary>
    /// Provides a simple list of all items in the table ignoring rolls and conditions.
    /// </summary>
    public abstract IEnumerable<(RandomTableValueDefinition value, float prob)> ListImplementation(RandomTableContext ctx, float probabilityMultiplier = 1f);

    /// <summary>
    /// Calculates a condensed list of items, combining like items from children tables.
    /// </summary>
    public Dictionary<RandomTableValueDefinition, float> List(RandomTableContext ctx, float probabilityMultipler = 1f)
    {
        var list = ListImplementation(ctx, probabilityMultipler);
        Dictionary<RandomTableValueDefinition, float> dict = new();

        foreach (var (value, prob) in list)
        {
            dict[value] = dict.GetValueOrDefault(value) + prob;
        }

        return dict;
    }

    public bool CheckConditions(RandomTableContext ctx)
    {
        if (Conditions.Count == 0) return true; // If there no conditions to check, who cares.

        bool all = true;
        bool any = false;

        foreach (var condition in Conditions)
        {
            if (condition.Evaluate(this, ctx))
                any = true;
            else all = false;
        }

        switch (ConditionMode)
        {
            case RandomTableConditionMode.All:
                return all;
            case RandomTableConditionMode.Any:
                return any;
            case RandomTableConditionMode.None:
                return !any;
            default:
                throw new NotImplementedException();
        }
    }
}

