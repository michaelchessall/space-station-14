using Content.Shared.Guidebook;
using Content.Shared.Atmos;

namespace Content.Server.Power.Generation.GasGenerator;

[RegisterComponent]
public sealed partial class GasGeneratorComponent : Component
{
    /// <summary>
    /// The node name for the fuel inlet where gas is accepted.
    /// </summary>
    public const string NodeNameInlet = "inlet";

    /// <summary>
    /// The node name for the exhaust outlet where spent gases are expelled.
    /// </summary>
    public const string NodeNameOutlet = "outlet";

    /// <summary>
    /// Optimal ratio of primary input gas (inputGas1) to total fuel (0-1 ratio).
    /// Used as reference point for efficiency calculations.
    /// </summary>
    [DataField("optimalInputRatio")]
    public float OptimalInputRatio = 0.2f; // 20% primary, 80% secondary is optimal

    // Temperature-based fields removed: temperature no longer affects generation.

    /// <summary>
    /// Primary fuel gas (e.g., Methane).
    /// </summary>
    [DataField("inputGas1")]
    public Gas InputGas1 = Gas.Methane;

    /// <summary>
    /// Secondary fuel gas (e.g., Oxygen).
    /// </summary>
    [DataField("inputGas2")]
    public Gas InputGas2 = Gas.Oxygen;

    /// <summary>
    /// Molar ratio of InputGas1 consumed per combustion cycle.
    /// For CH4: 1.0 mole consumed per cycle.
    /// </summary>
    [DataField("inputGas1Ratio")]
    public float InputGas1Ratio = 1.0f;

    /// <summary>
    /// Molar ratio of InputGas2 consumed per combustion cycle.
    /// For O2 in CH4 combustion: 2.0 moles consumed per mole CH4.
    /// </summary>
    [DataField("inputGas2Ratio")]
    public float InputGas2Ratio = 2.0f;

    /// <summary>
    /// Primary waste gas (e.g., WaterVapor).
    /// </summary>
    [DataField("wasteGas1")]
    public Gas WasteGas1 = Gas.WaterVapor;

    /// <summary>
    /// Secondary waste gas (e.g., CarbonDioxide).
    /// </summary>
    [DataField("wasteGas2")]
    public Gas WasteGas2 = Gas.CarbonDioxide;

    /// <summary>
    /// Maximum pressure (in kPa) that can accumulate inside the generator chamber.
    /// Internal pump will stop transferring gas once this pressure is reached.
    /// </summary>
    [DataField("maxInternalPressure")]
    public float MaxInternalPressure = 150f; // kPa - realistic for small methane engines

    /// <summary>
    /// Internal combustion chamber volume in liters.
    /// Used to calculate pressure from gas moles.
    /// </summary>
    [DataField("internalVolume")]
    public float InternalVolume = 200f; // liters

    /// <summary>
    /// Maximum inlet flow rate (in moles per second) for the internal pump.
    /// Simulates realistic fuel delivery system limitations.
    /// </summary>
    [DataField("maxInletFlowRate")]
    public float MaxInletFlowRate = 2.0f; // moles/sec

    /// <summary>
    /// Volume (liters) applied to the inlet pipe node for this generator.
    /// Exposed to YAML so different generator prototypes can specify inlet volumes.
    /// </summary>
    [DataField("inletNodeVolume")]
    public float InletNodeVolume = 1.0f; // liters

    /// <summary>
    /// Maximum possible fuel consumption rate (in moles per second at full efficiency).
    /// Adjusted by efficiency modifiers.
    /// </summary>
    [DataField("maxFuelConsumptionRate")]
    public float MaxFuelConsumptionRate = 10.0f; // moles/sec

    /// <summary>
    /// Base fraction of available primary fuel consumed per update tick (0-1).
    /// This used to be hardcoded in the system as `0.05f`.
    /// </summary>
    [DataField("baseConsumptionFraction")]
    public float BaseConsumptionFraction = 0.05f;

    /// <summary>
    /// Maximum power output (in watts).
    /// Mapped from YAML field `maxOutput`.
    /// </summary>
    [GuidebookData]
    [DataField("maxOutput")]
    public float MaxPowerOutput = 50000f; // 50 kW

    /// <summary>
    /// Multiplier applied to effective power-per-mole to scale power output
    /// relative to mole consumption. This replaces the hardcoded `3.0f`.
    /// </summary>
    [DataField("powerScaleMultiplier")]
    public float PowerScaleMultiplier = 3.0f;

    /// <summary>
    /// Additional flat power boost applied multiplicatively (replaces hardcoded `1.2f`).
    /// </summary>
    [DataField("powerExtraBoost")]
    public float PowerExtraBoost = 1.2f;

    /// <summary>
    /// <summary>
    /// Simple fuel efficiency scalar (0-1).
    /// Applied directly to power output.
    /// </summary>
    [DataField("fuelEfficiency")]
    public float FuelEfficiency = 0.9f;

    /// <summary>
    /// Fuel slip rate (0-1). Represents unburned primary fuel (inputGas1) escaping as exhaust.
    /// Higher at lower loads. Typical value: 0.1 (10% slip).
    /// </summary>
    [DataField("fuelSlipRate")]
    public float FuelSlipRate = 0.1f;

    /// <summary>
    /// Combustion energy released per mole of primary fuel consumed (in Joules).
    /// This determines exhaust temperature. Default is FireMethaneEnergyReleased (560 kJ/mol).
    /// Can be customized per generator type in YAML for different fuel reactions.
    /// </summary>
    [DataField("combustionEnergyPerMole")]
    public float CombustionEnergyPerMole = 560000f; // 560 kJ/mol (methane default)

    /// <summary>
    /// Molar ratio of WasteGas1 produced per mole of primary fuel consumed.
    /// For CH4 combustion: H2O is produced at 2:1 ratio.
    /// </summary>
    [DataField("wasteGas1Ratio")]
    public float WasteGas1Ratio = 2.0f;

    /// <summary>
    /// Molar ratio of WasteGas2 produced per mole of primary fuel consumed.
    /// For CH4 combustion: CO2 is produced at 1:1 ratio.
    /// </summary>
    [DataField("wasteGas2Ratio")]
    public float WasteGas2Ratio = 1.0f;

    /// <summary>
    /// Current efficiency (0-1). Mirrors `FuelEfficiency` for UI.
    /// </summary>
    [ViewVariables]
    public float CurrentEfficiency = 0.9f;

    /// <summary>
    // Legacy granular efficiencies removed; simplified power model is YAML-driven.

    /// <summary>
    /// Current fuel consumption rate (in moles per second), adjusted by efficiency.
    /// </summary>
    [ViewVariables]
    public float CurrentConsumptionRate = 0.0f;

    /// <summary>
    /// Power being generated this tick (in joules per update tick).
    /// </summary>
    [ViewVariables]
    public float LastGeneration = 0.0f;

    /// <summary>
    /// Exponential moving average factor for smoothing power output.
    /// Higher values = more smoothing.
    /// </summary>
    [DataField("powerSmoothingFactor")]
    public float PowerSmoothingFactor = 0.0f; // disable smoothing by default for simple model

    /// <summary>
    /// Current ramp position for power ramping (smooth power curve).
    /// </summary>
    [ViewVariables]
    public float RampPosition = 0f; // disable ramping by default for simple model

    /// <summary>
    /// Multiplier for ramp adjustments (allows exponential ramping).
    /// </summary>
    [DataField("rampFactor")]
    public float RampFactor = 1.0f; // no ramping

    /// <summary>
    /// Minimum ramp position to ensure some power generation.
    /// </summary>
    [DataField("rampMinimum")]
    public float RampMinimum = 0f;

    /// <summary>
    /// Whether the generator is currently enabled (can still be powered off by power consumer).
    /// </summary>
    [DataField("enabled")]
    public bool Enabled = true;

    /// <summary>
    /// Internal gas mixture representing the combustion chamber.
    /// Gas is pumped from inlet pipes into this chamber, then consumed during combustion.
    /// </summary>
    [ViewVariables]
    public GasMixture? InternalAtmosphere;
}
