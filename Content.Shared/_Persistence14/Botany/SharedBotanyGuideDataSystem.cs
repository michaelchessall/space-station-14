using Content.Shared.Botany;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Persistence14.Botany;

/// <summary>
/// This handles the chemistry guidebook and caching it.
/// </summary>
public abstract class SharedBotanyGuideDataSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager PrototypeManager = default!;

    protected readonly Dictionary<string, PlantNutrientGuideEntry> Registry = new();

    public IReadOnlyDictionary<string, PlantNutrientGuideEntry> PlantNutrientGuideRegistry => Registry;

    // Only ran on the server
    public abstract void ReloadAllPlantNutrientPrototypes();
}

[Serializable, NetSerializable]
public sealed class PlantNutrientGuideRegistryChangedEvent : EntityEventArgs
{
    public PlantNutrientGuideChangeset Changeset;

    public PlantNutrientGuideRegistryChangedEvent(PlantNutrientGuideChangeset changeset)
    {
        Changeset = changeset;
    }
}

[Serializable, NetSerializable]
public sealed class PlantNutrientGuideChangeset
{
    public Dictionary<string, PlantNutrientGuideEntry> GuideEntries;

    public HashSet<string> Removed;

    public PlantNutrientGuideChangeset(Dictionary<string, PlantNutrientGuideEntry> guideEntries, HashSet<string> removed)
    {
        GuideEntries = guideEntries;
        Removed = removed;
    }
}
