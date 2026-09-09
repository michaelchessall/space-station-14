using System.Linq;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Atmos.Prototypes;
using Content.Shared.Body;
using Content.Shared._Persistence14.Botany;
using Content.Shared.Botany;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Kitchen.Components;
using Content.Shared.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Client._Persistence14.Botany;

/// <inheritdoc/>
public sealed class BotanyGuideDataSystem : SharedBotanyGuideDataSystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    private static readonly ProtoId<MixingCategoryPrototype> DefaultMixingCategory = "DummyPlantOsmosis";
    private static readonly ProtoId<MixingCategoryPrototype> DefaultCondenseCategory = "DummyPlantRespiration";

    private readonly Dictionary<string, List<PlantNutrientSourceData>> _PlantNutrientSources = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PlantNutrientGuideRegistryChangedEvent>(OnReceiveRegistryUpdate);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        OnPrototypesReloaded(null);
    }

    private void OnReceiveRegistryUpdate(PlantNutrientGuideRegistryChangedEvent message)
    {
        var data = message.Changeset;
        foreach (var remove in data.Removed)
        {
            Registry.Remove(remove);
        }

        foreach (var (key, val) in data.GuideEntries)
        {
            Registry[key] = val;
        }
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs? ev)
    {
        // this doesn't check what prototypes are being reloaded because, to be frank, we use a lot of them.
        _PlantNutrientSources.Clear();
        foreach (var PlantNutrient in PrototypeManager.EnumeratePrototypes<PlantNutrientPrototype>())
        {
            _PlantNutrientSources.Add(PlantNutrient.ID, new());
        }

        foreach (var reagent in PrototypeManager.EnumeratePrototypes<ReagentPrototype>())
        {
            var data = new PlantNutrientReagentSourceData(new() { DefaultMixingCategory }, reagent);
            List<ProtoId<PlantNutrientPrototype>> nutrients = new();

            foreach (var nutrient in nutrients)
            {
                _PlantNutrientSources[nutrient].Add(data);
            }
        }

        foreach (var gas in PrototypeManager.EnumeratePrototypes<GasPrototype>())
        {
            if (gas.Nutrient == null)
                continue;

            var data = new PlantNutrientGasSourceData(
                new() { DefaultCondenseCategory },
                gas);
            _PlantNutrientSources[gas.Nutrient].Add(data);
        }
    }

    public List<PlantNutrientSourceData> GetPlantNutrientSources(string id)
    {
        return _PlantNutrientSources.GetValueOrDefault(id) ?? new List<PlantNutrientSourceData>();
    }

    // Is handled on server and updated on client via PlantNutrientGuideRegistryChangedEvent
    public override void ReloadAllPlantNutrientPrototypes()
    {
    }
}

/// <summary>
/// A generic class meant to hold information about a PlantNutrient source.
/// </summary>
public abstract class PlantNutrientSourceData
{
    /// <summary>
    /// The mixing type that applies to this source.
    /// </summary>
    public readonly IReadOnlyList<ProtoId<MixingCategoryPrototype>> MixingType;

    // <summary>
    // The number of distinct outputs. Used for primary ordering.
    // </summary>
    //public abstract int OutputCount { get; }

    /// <summary>
    /// A text string corresponding to this source. Typically a name. Used for secondary ordering.
    /// </summary>
    public abstract string IdentifierString { get; }

    protected PlantNutrientSourceData(List<ProtoId<MixingCategoryPrototype>> mixingType)
    {
        MixingType = mixingType;
    }
}

/// <summary>
/// Used to store a nutrient source that comes from absorbing reagents.
/// </summary>
public sealed class PlantNutrientReagentSourceData : PlantNutrientSourceData
{
    public readonly ReagentPrototype ReagentPrototype;

    //public override int OutputCount => ReactionPrototype.Products.Count + ReactionPrototype.Reactants.Count(r => r.Value.Catalyst);

    public override string IdentifierString => ReagentPrototype.ID;

    public PlantNutrientReagentSourceData(List<ProtoId<MixingCategoryPrototype>> mixingType, ReagentPrototype reagentPrototype)
        : base(mixingType)
    {
        ReagentPrototype = reagentPrototype;
    }
}

/// <summary>
/// Used to store a nutrient source that comes from absorbing gasses.
/// </summary>
public sealed class PlantNutrientGasSourceData : PlantNutrientSourceData
{
    public readonly GasPrototype GasPrototype;

    //public override int OutputCount => 1;

    public override string IdentifierString => Loc.GetString(GasPrototype.Name);

    public PlantNutrientGasSourceData(List<ProtoId<MixingCategoryPrototype>> mixingType, GasPrototype gasPrototype)
        : base(mixingType)
    {
        GasPrototype = gasPrototype;
    }
}

