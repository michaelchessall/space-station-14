namespace Content.Shared._Persistence14.Radiation;

/// <summary>
/// Allows an object to gain radioactive intensity by being exposed to radiation from other sources. This intensity is scaled 
/// </summary>
[RegisterComponent]
public sealed partial class RadiationActivationComponent : Component
{
    /// <summary>
    /// Maximum intensity this entity can accumulate.
    /// </summary>
    [DataField]
    public float MaxIntensity = 10f;

    /// <summary>
    /// How effectively incoming radiation becomes activity.
    /// 
    /// At default rate, constant exposure to a given radiation intensity will accumulate that same intensity over 5 hours, ignoring decay.
    /// </summary>
    [DataField]
    public float ActivationRate = 1f / 18000f;
}