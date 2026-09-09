using Content.Shared.Destructible.Thresholds;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Xenoarchaeology.Artifact.Components;

/// <summary>
/// Stores metadata about a particular artifact node
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedXenoArtifactSystem)), AutoGenerateComponentState]
public sealed partial class XenoArtifactNodeComponent : Component
{
    /// <summary>
    /// Depth within the graph generation.
    /// Used for sorting.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Depth;

    /// <summary>
    /// Denotes whether an artifact node has been activated at least once (through the required triggers).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Locked = true;

    /// <summary>
    /// List of trigger descriptions that this node require for activation.
    /// </summary>
    [DataField, AutoNetworkedField]
    public LocId? TriggerTip;

    /// <summary>
    /// The entity whose graph this node is a part of.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Attached;

    #region Durability
    /// <summary>
    /// Marker, is durability of node degraded or not.
    /// </summary>
    public bool Degraded => Durability <= 0 && ActivatedOnce;

    /// <summary>
    /// The amount of generic activations a node has left before becoming fully degraded and useless.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Durability;

    /// <summary>
    /// Marks whether the artifact has ever been activated. Used for the initial activation of 0 durability nodes.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ActivatedOnce = false;
    
    /// <summary>
    /// The maximum amount of times a node can be generically activated before becoming useless
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxDurability = 5;

    /// <summary>
    /// The maximum factor by which using the durability of an artifact will scale it's Research Value.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DurabilityResearchMultiplier = 4f;

    /// <summary>
    /// The variance from MaxDurability present when a node is created.
    /// </summary>
    [DataField, AutoNetworkedField]
    public MinMax MaxDurabilityCanDecreaseBy = new(0, 2);

    /// <summary>
    /// The lifetime consumed durability of the node.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int TotalConsumedDurability = 0;

    /// <summary>
    /// The threshold at which the node has a <see cref="ControlPointShatterProbability"/> chance of shattering on activation.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int ControlPointShatterDurabilityThreshold = 15;

    /// <summary>
    /// The probability the node will shatter at <see cref="ControlPointShatterDurabilityThreshold"/> 
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float ControlPointShatterProbability = .1f;

    /// <summary>
    /// The threshold at which the node has a 100% chance of shattering on activation.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxShatterDurabilityThreshold = 40;

    /// <summary>
    /// Shattered nodes cannot be have their durability increased.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool Shattered = false;

    /// <summary>
    /// Determines the pattern shown on the UI for the analysis console.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public ShatterPatternTypes ShatterPattern = ShatterPatternTypes.Strike;

    /// <summary>
    /// Sound to play when a node is shattered.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public SoundSpecifier ShatterSound = new SoundPathSpecifier("/Audio/Effects/glass_break1.ogg");
    #endregion

    #region Research
    /// <summary>
    /// The amount of points a node is worth with no scaling
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BasePointValue = 2000;

    /// <summary>
    /// Amount of points available currently for extracting.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ResearchValue;

    /// <summary>
    /// Amount of points already extracted from node.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ConsumedResearchValue;

    /// <summary>
    /// Fraction (0-1) of this node's required triggers that artifexium filled in rather than being
    /// triggered manually. 0 = unlocked normally. Drives the research penalty in UpdateNodeResearchValue.
    /// </summary>
    [DataField]
    public float ArtifexiumUnlockFraction;

    /// <summary>
    /// Research reduction applied when this node was barely artifexium-assisted (fraction -> 0).
    /// </summary>
    [DataField]
    public float ArtifexiumMinPenalty = 0.1f;

    /// <summary>
    /// Research reduction applied when this node was fully artifexium-unlocked (fraction = 1).
    /// </summary>
    [DataField]
    public float ArtifexiumMaxPenalty = 0.5f;
    #endregion
}

public enum ShatterPatternTypes
{
    Strike, Bolt, Spider, Fracture, Split, Fragment
}
