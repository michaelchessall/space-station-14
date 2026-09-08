namespace Content.Shared._Persistence14.Radiation;

[RegisterComponent]
public sealed partial class RadioactiveDecayComponent : Component
{
    /// <summary>
    /// The amount of time it takes for the intensity of the entity to half in value.
    /// Follows exponential decay.
    /// 
    /// With default values, the entity will half in intensity once every 5 hours (once a shift).
    /// </summary>
    [DataField]
    public TimeSpan HalfLife = TimeSpan.FromHours(5);
}