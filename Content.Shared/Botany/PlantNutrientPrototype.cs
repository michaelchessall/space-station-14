using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Botany;

/// <summary>
/// This is a prototype for...
/// </summary>
[Prototype]
[DataDefinition]
public sealed partial class PlantNutrientPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    private LocId Name { get; set; }

    [ViewVariables(VVAccess.ReadOnly)]
    public string LocalizedName => Loc.GetString(Name);

    [DataField("desc", required: true)]
    private LocId Description { get; set; }

    [ViewVariables(VVAccess.ReadOnly)]
    public string LocalizedDescription => Loc.GetString(Description);

    [DataField("color")]
    public Color SubstanceColor { get; private set; } = Color.White;
}

[Serializable, NetSerializable]
public struct PlantNutrientGuideEntry
{
    public string ReagentPrototype;

    //public Dictionary<ProtoId<MetabolismStagePrototype>, ReagentEffectsGuideEntry>? GuideEntries;

    //public List<string>? PlantMetabolisms = null;

    public PlantNutrientGuideEntry(PlantNutrientPrototype proto,
        IPrototypeManager prototype,
        IEntitySystemManager entSys)
    {
        ReagentPrototype = proto.ID;/*
        GuideEntries = proto.Metabolisms?.Metabolisms
            .Select(x => (x.Key, x.Value.MakeGuideEntry(prototype, entSys, proto)))
            .ToDictionary(x => x.Key, x => x.Item2);
        if (proto.PlantMetabolisms.Count > 0)
        {
            PlantMetabolisms =
                new List<string>(proto.GuidebookReagentEffectsDescription(prototype,
                    entSys,
                    proto.PlantMetabolisms,
                    FixedPoint2.New(1f)));
        }*/
    }
}
