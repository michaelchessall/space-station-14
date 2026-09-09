using Content.Shared.Atmos;
using Content.Shared.MiningFluid.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Persistence14.Atmos.Geyser;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class GasGeyserComponent : Component
{
    /// <summary>
    /// Game time
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField, AutoNetworkedField]
    public TimeSpan NextEruptionTime = TimeSpan.Zero;

    /// <summary>
    /// Minimum time between erruptions.
    /// </summary>
    [DataField]
    public TimeSpan EruptionDelay = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Amount of time added to <see cref="EruptionDelay"/> to get the maximum delay.
    /// </summary>
    [DataField]
    public TimeSpan EruptionRangeDeltaPositive = TimeSpan.FromSeconds(0);

    /// <summary>
    /// Amount of time subtracted from <see cref="EruptionDelay"/> to get the minimum delay.
    /// </summary>
    [DataField]
    public TimeSpan EruptionRangeDeltaNegative = TimeSpan.FromSeconds(0);

    public TimeSpan MaxEruptionDelay => EruptionDelay + EruptionRangeDeltaPositive;
    public TimeSpan MinEruptionDelay => EruptionDelay - EruptionRangeDeltaNegative;

    /// <summary>
    /// Gases released into the atmosphere when the geyser erupts.
    /// </summary>
    [DataField(required: true, customTypeSerializer: typeof(GasArraySerializer)), AutoNetworkedField]
    public float[] Moles = new float[Atmospherics.AdjustedNumberOfGases];

    /// <summary>
    /// Optional weighted gas additions applied independently each eruption.
    /// Mirrors TrappedFluid's variableMixture behavior (per-gas prob + moles).
    /// </summary>
    [DataField("variableMoles")]
    public Dictionary<Gas, VariableFluidDefinition> VariableMoles = new();

    /// <summary>
    /// When external gas mixture exceeds this amount of moles, geysers cannot errupt.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxExternalMoles = float.PositiveInfinity;

    /// <summary>
    /// When external gas mixture exceeds this pressure, geysers cannot errupt.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxExternalPressure = Atmospherics.GasMinerDefaultMaxExternalPressure;

    /// <summary>
    /// Tempurature of gas spawned from the geyser.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SpawnTemperature = Atmospherics.T20C;

    /// <summary>
    /// Don't play with this unless you know what you are doing...
    /// </summary>
    [DataField]
    public string ErruptionAnimationKey = "geyser_animated";
}
