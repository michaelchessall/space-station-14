using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._Persistence14.Cargo;

[ImplicitDataDefinitionForInheritors]
public abstract partial class BountyCondition
{
    private const int MAX_RECURSION_DEPTH = 100;

    /// <summary>
    /// Verifies the condition of this <see cref="BountyCondition"/>. Should return a float value between 0 and 1, representing the amount of the condition completed.<br/><br/>
    /// All-or-Nothing bounties should return exactly 1 (all) or 0 (nothing).
    /// </summary>
    public abstract float CheckCondition(EntityUid containerUid, IEntityManager entityManager);

    public abstract IEnumerable<string> GetManifestEntry(IEntityManager entityManager, IPrototypeManager prototypeManager);

    /// <summary>
    /// Enumerates the EntityUids of all contained entities in all containers, up to a maximum recursive depth of <see cref="MAX_RECURSION_DEPTH"/>.
    /// </summary>
    /// <param name="depth">For recursive use only, do not use.</param>
    protected IEnumerable<EntityUid> EnumerateContainedEntities(EntityUid uid, IEntityManager entityManager, int depth = 0)
    {
        yield return uid;
        if (depth >= MAX_RECURSION_DEPTH)
            yield break;

        if (!entityManager.TryGetComponent<ContainerManagerComponent>(uid, out var containerManagerComponent))
            yield break;

        foreach (var (id, container) in containerManagerComponent.Containers)
            foreach (var item in container.ContainedEntities)
                foreach (var result in EnumerateContainedEntities(item, entityManager, depth + 1))
                    yield return result;
    }

    /// <summary>
    /// Allows a BountyCondition to define a means of calculating the price per item/unit/etc, if applicable.
    /// </summary>
    public virtual bool TryGetPricePer(float totalPrice, out float pricePer, out string unitName)
    {
        pricePer = totalPrice;
        unitName = default!;
        return false;
    }
}