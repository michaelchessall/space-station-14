namespace Content.Shared.Radiation.Components;

/// <summary>
///     Irradiate all objects in range.
/// </summary>
[RegisterComponent]
public sealed partial class RadiationSourceComponent : Component
{
    /// <summary>
    ///     Radiation intensity in center of the source in rads per second.
    ///     From there radiation rays will travel over distance and loose intensity
    ///     when hit radiation blocker.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("intensity")]
    public float Intensity = 1;

    /// <summary>
    ///     Defines how fast radiation rays will loose intensity
    ///     over distance. The bigger the value, the shorter range
    ///     of radiation source will be.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("slope")]
    public float Slope = 0.5f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Enabled = true;

    /// <summary>
    ///     Optional minimum time (seconds) between this source participating in gridcast updates.
    ///     Set to 0 to process every radiation update tick.
    /// </summary>
    [DataField("updateInterval"), ViewVariables(VVAccess.ReadWrite)]
    public float UpdateInterval = 0f;

    /// <summary>
    ///     If true, apply a deterministic per-entity phase offset to update scheduling.
    ///     This helps spread expensive source calculations across different ticks.
    /// </summary>
    [DataField("staggerUpdates"), ViewVariables(VVAccess.ReadWrite)]
    public bool StaggerUpdates = false;

    [ViewVariables]
    public float NextUpdateTime;

    [ViewVariables]
    public bool UpdateScheduleInitialized;
}
