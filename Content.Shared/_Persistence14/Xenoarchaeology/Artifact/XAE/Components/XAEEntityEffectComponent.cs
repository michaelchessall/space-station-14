using Content.Shared.EntityEffects;
using Content.Shared.Whitelist;
using Robust.Shared.Serialization;

namespace Content.Shared._Persistence14.Xenoarcheology.Artifact.XAE.Components;

[RegisterComponent, Access(typeof(XAEEntityEffectSystem))]
public sealed partial class XAEEntityEffectComponent : Component
{
    /// <summary>
    /// The effects to be applied and the flags to determine the target.
    /// </summary>
    [DataField(required: true)]
    public List<ArtifactEntityEffect> Effects = new();
}

[DataDefinition, NetSerializable, Serializable]
public sealed partial class ArtifactEntityEffect
{
    /// <summary>
    /// Targeting flags for the entity effect;
    /// </summary>
    [DataField(required: true)]
    public XAEEntityEffectTargetFlags Flags;

    /// <summary>
    /// A selection whitelist for nearest/nearby/random targeting flags.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist = null;

    /// <summary>
    /// A selection blacklist for nearest/nearby/random targeting flags.
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist = null;

    /// <summary>
    /// The effect to be applied to the target(s) based on the flags.
    /// </summary>
    [DataField(required: true)]
    public EntityEffect Effect;

    /// <summary>
    /// The number of times the effect should be applied.
    /// </summary>
    [DataField]
    public int Count = 1;

    /// <summary>
    /// The minimum range in which nearby/nearest/random targeting flags may target.
    /// </summary>
    [DataField]
    public float MinRange = 0f;

    /// <summary>
    /// The maximum range in which nearby/nearest/random targeting flags may target.
    /// </summary>
    [DataField]
    public float MaxRange = 10f;
}

[Flags]
public enum XAEEntityEffectTargetFlags : byte
{
    Artifact = 1 << 0,
    User = 1 << 1,
    Nearest = 1 << 2,
    Nearby = 1 << 3,
    Random = 1 << 4,
}