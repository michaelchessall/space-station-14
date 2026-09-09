using Content.Shared.Stacks;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared._Persistence14.Cargo;

/// <summary>
/// A bounty condition measuring how many contained entities that meet an entity whitelist and blacklist.<br/>
/// Returns the number of valid items divided by the desired quantity. Clamped to [0, 1].
/// </summary>
public sealed partial class BountyConditionEntityWhitelist : BountyCondition
{
    [DataField]
    public EntityWhitelist? Whitelist = null;

    [DataField]
    public EntityWhitelist? Blacklist = null;

    [DataField]
    public int Amount = 1;

    [DataField("name", required: true)]
    public LocId DisplayName;

    /// <inheritdoc/>
    public override float CheckCondition(EntityUid containerUid, IEntityManager entityManager)
    {
        var whitelistSystem = entityManager.System<EntityWhitelistSystem>();

        int success = 0;
        foreach (var uid in EnumerateContainedEntities(containerUid, entityManager))
        {
            if (whitelistSystem.IsWhitelistPassOrNull(Whitelist, uid) && whitelistSystem.IsWhitelistFailOrNull(Blacklist, uid))
            {
                if (entityManager.TryGetComponent<StackComponent>(uid, out var stackComponent))
                    success += stackComponent.Count;
                else
                    success += 1;
            }


            if (success >= Amount) return 1f;
        }

        return Math.Clamp((float)success / Amount, 0f, 1f);
    }

    /// <inheritdoc/>
    public override IEnumerable<string> GetManifestEntry(IEntityManager entityManager, IPrototypeManager prototypeManager)
    {
        yield return Loc.GetString("bounty-condition-item-quantity", ("item", Loc.GetString(DisplayName)), ("quantity", Amount));
    }

    /// <inheritdoc/>
    public override bool TryGetPricePer(float totalPrice, out float pricePer, out string unitName)
    {
        pricePer = 0f;
        unitName = Loc.GetString(DisplayName);
        if (Amount <= 1) return false;

        pricePer = totalPrice / Amount;
        return true;
    }
}