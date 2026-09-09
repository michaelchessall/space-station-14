using Content.Shared._Persistence14.Cargo;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Cargo.Prototypes;

/// <summary>
/// This is a prototype for a cargo bounty, a set of items
/// that must be sold together in a labeled container in order
/// to receive a monetary reward.
/// </summary>
[Prototype]
public sealed partial class CargoBountyPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// A description for flava purposes.
    /// </summary>
    [DataField]
    public LocId Description = string.Empty;

    /// <summary>
    /// A group used for categorizing this bounty.
    /// </summary>
    [DataField]
    public ProtoId<CargoBountyGroupPrototype> Group = "StationBounty";

    /// <summary>
    /// The monetary reward for completing the bounty
    /// </summary>
    [DataField(required: true)]
    public float Reward;

    /// <summary>
    /// The amount of station XP awarded when the bounty is completed.
    /// </summary>
    [DataField]
    public int SuccessXP = 25;

    /// <summary>
    /// The amount of station XP lost if a bounty is not completed in time.
    /// </summary>
    [DataField]
    public int FailureXP = 15;

    /// <summary>
    /// The condition which must be completed to consider the bounty complete. May use BountyConditionAll or BountyConditionAny to allow multiple conditions to be required.
    /// </summary>
    [DataField(required: true)]
    public BountyCondition Condition = default!;

    /// <summary>
    /// A prefix appended to the beginning of a bounty's ID.
    /// </summary>
    [DataField]
    public string IdPrefix = "NT";

    /// <summary>
    /// Optional sprite representing this bounty.
    /// </summary>
    [DataField]
    public SpriteSpecifier? Sprite;
}
