using Content.Shared.Botany;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Botany.Components;

[RegisterComponent]
public sealed partial class PlantHolderComponent : Component
{
    /// <summary>
    /// Game time for the next plant reagent update.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    /// <summary>
    /// Time between plant reagent consumption updates.
    /// </summary>
    [DataField]
    public TimeSpan UpdateDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public int LastProduce;

    [DataField] // TODO: Comment out with plant nutrient rework
    public int MissingGas;

    /// <summary>
    /// Time between plant growth updates.
    /// </summary>
    [DataField]
    public TimeSpan CycleDelay = TimeSpan.FromSeconds(15f);

    /// <summary>
    /// Game time when the plant last did a growth update.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan LastCycle = TimeSpan.Zero;

    /// <summary>
    /// Sound played when any reagent is transferred into the plant holder.
    /// </summary>
    [DataField]
    public SoundSpecifier? WateringSound;

    [DataField]
    public bool UpdateSpriteAfterUpdate;

    /// <summary>
    /// Set to true if the plant holder displays plant warnings (e.g. water low) in the sprite and
    /// examine text. Used to differentiate hydroponic trays from simple soil plots.
    /// </summary>
    [DataField]
    public bool DrawWarnings = false;

    [DataField] // TODO: Comment out with plant nutrient rework
    public float WaterLevel = 100f;

    [DataField] // TODO: Comment out with plant nutrient rework
    public float NutritionLevel = 100f;

    [DataField]
    public Dictionary<ProtoId<PlantNutrientPrototype>, FixedPoint2> Nutrients = new();

    [DataField] // TODO: Comment out with plant nutrient rework
    public float PestLevel;

    [DataField]
    public float WeedLevel;

    [DataField] // TODO: Comment out with plant nutrient rework
    public float Toxins;

    [DataField]
    public int Age;

    [DataField]
    public int SkipAging;

    [DataField]
    public bool Dead;

    [DataField]
    public bool Harvest;

    [DataField]
    public bool HarvestAge;

    /// <summary>
    /// Set to true if this plant has been clipped by seed clippers. Used to prevent a single plant
    /// from repeatedly being clipped.
    /// </summary>
    [DataField]
    public bool Sampled;

    /// <summary>
    /// Multiplier for the number of entities produced at harvest.
    /// </summary>
    [DataField]
    public int YieldMod = 1;

    [DataField]
    public float MutationMod = 1f;

    [DataField]
    public float MutationLevel;

    [DataField]
    public float Health;

    [DataField]
    public float WeedCoefficient = 1f;

    [DataField]
    public SeedData? Seed;

    /// <summary>
    /// Persistence: Plant nutrient rework, True if the plant is losing health due to too low temperature.
    /// </summary>
    [DataField]
    public bool LowHeat;

    /// <summary>
    /// Persistence: Plant nutrient rework, True if the plant is losing health due to too high temperature.
    /// </summary>
    [DataField]
    public bool HighHeat;

    /// <summary>
    /// Persistence: Plant nutrient rework, True if the plant is losing health due to too low pressure.
    /// </summary>
    [DataField]
    public bool LowPressure;

    /// <summary>
    /// Persistence: Plant nutrient rework, True if the plant is losing health due to too high pressure.
    /// </summary>
    [DataField]
    public bool HighPressure;

    /// <summary>
    /// Not currently used.
    /// </summary>
    [DataField]
    public bool ImproperLight;

    /// <summary>
    /// Set to true to force a plant update (visuals, component, etc.) regardless of the current
    /// update cycle time. Typically used when some interaction affects this plant.
    /// </summary>
    [DataField]
    public bool ForceUpdate;

    [DataField]
    public string SoilSolutionName = "soil";

    [ViewVariables]
    public Entity<SolutionComponent>? SoilSolution = null;
}
