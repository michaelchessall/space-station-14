using Content.Shared.EntityEffects;

namespace Content.Shared._Persistence14.EntityEffects;

/// <summary>
///     An entity effect to add radiation intensity to an object. Automatically configures radioactive decay and activation components.
///     Can be configured to add the RadiationSourceComponent if not already present.
/// </summary>
public sealed partial class Irradiate : EntityEffectBase<Irradiate>
{
    [DataField(required: true)]
    public float Intensity;

    [DataField]
    public TimeSpan HalfLife = TimeSpan.Zero;
    public bool Decays => HalfLife > TimeSpan.Zero;

    [DataField]
    public float MaxIntensity = 0f;
    public bool Activates => MaxIntensity > Intensity;

    [DataField]
    public bool AddComponentIfAbsent = true;
}