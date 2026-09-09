using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared._Persistence14.Research.Anomalies;

[Prototype]
public sealed partial class AnomalyPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = default!;

    [DataField("name", required: true)]
    public LocId DisplayName { get; set; } = default!;

    /// <summary>
    /// The entity to be spawned when spawning an environmental version of the anomaly.
    /// </summary>
    [DataField("environmental")]
    public EntProtoId? EnvironmentalAnomalyEntity = null;
    /// <summary>
    /// A list of environmental anomalies that may be spawned from this anomaly. Allows optional anomaly variants (i.e. rock anomaly variants).
    /// </summary>
    [DataField("environmentalVariants")]
    public Dictionary<EntProtoId, float> EnvironmentalAnomalyVariants = new();
    public bool HasEnvironmental => EnvironmentalAnomalyEntity != null || EnvironmentalAnomalyVariants.Count > 0;
    /// <summary>
    /// The weight affecting odds of spawning the environmental version of the anomaly. Ignored if <see cref="EnvironmentalAnomalyEntity"/> is null or undefined.
    /// </summary>
    [DataField]
    public float EnvironmentalWeight = 1f;

    /// <summary>
    /// The entity to be spawned when spawning an infectious version of the anomaly.
    /// </summary>
    [DataField("infectious")]
    public EntProtoId? InfectiousAnomalyEntity = null;
    /// <summary>
    /// A list of infectious anomalies that may be spawned from this anomaly. Allows optional anomaly variants (i.e. rock anomaly variants).
    /// </summary>
    [DataField("infectiousVariants")]
    public Dictionary<EntProtoId, float> InfectiousAnomalyVariants = new();
    public bool HasInfectious => InfectiousAnomalyEntity != null || InfectiousAnomalyVariants.Count > 0;
    /// <summary>
    /// The weight affecting odds of spawning the infectious version of the anomaly. Ignored if <see cref="InfectiousAnomalyEntity"/> is null or undefined.
    /// </summary>
    [DataField]
    public float InfectiousWeight = 1f;

    [DataField("image")]
    private EntProtoId? _imageEnt = null;
    /// <summary>
    /// The entity whose image is rendered in UIs. May be manually overriden. By default, matches environmental entity.
    /// </summary>
    public EntProtoId ImageEntity
    {
        get
        {
            if (_imageEnt is { } image)
                return image;

            if (EnvironmentalAnomalyEntity is { } environmental)
                return environmental;

            if (EnvironmentalAnomalyVariants.Count > 0)
                return EnvironmentalAnomalyVariants.First().Key;

            if (InfectiousAnomalyEntity is { } infectious)
                return infectious;

            if (InfectiousAnomalyVariants.Count > 0)
                return InfectiousAnomalyVariants.First().Key;

            throw new InvalidOperationException($"Anomaly prototype {ID} has no entity available for its image.");
        }
    }


    public bool TryGetSpawnableProtoId(IRobustRandom random, out EntProtoId spawnable)
    {
        spawnable = default!;
        if (!HasEnvironmental && !HasInfectious) return false;

        if (!HasInfectious) return TrySpawnEnvironmental(random, out spawnable);
        if (!HasEnvironmental) return TrySpawnInfectious(random, out spawnable);

        var total = EnvironmentalWeight + InfectiousWeight;
        var pick = random.Next(total);
        if (pick < EnvironmentalWeight)
            return TrySpawnEnvironmental(random, out spawnable);
        return TrySpawnInfectious(random, out spawnable);
    }

    public bool TrySpawnEnvironmental(IRobustRandom random, out EntProtoId spawnable)
    {
        spawnable = default!;
        if (HasEnvironmental)
        {
            if (EnvironmentalAnomalyEntity is { } environmental)
            {
                spawnable = environmental;
                return true;
            }

            spawnable = PickVariant(random, EnvironmentalAnomalyVariants);
            return true;
        }
        return false;
    }

    public bool TrySpawnInfectious(IRobustRandom random, out EntProtoId spawnable)
    {
        spawnable = default!;
        if (HasInfectious)
        {
            if (InfectiousAnomalyEntity is { } infectious)
            {
                spawnable = infectious;
                return true;
            }

            spawnable = PickVariant(random, InfectiousAnomalyVariants);
            return true;
        }
        return false;
    }

    private EntProtoId PickVariant(IRobustRandom random, Dictionary<EntProtoId, float> variants)
    {
        var totalWeight = variants.Values.Sum();
        var pick = random.Next(totalWeight);

        foreach (var (proto, weight) in variants)
        {
            pick -= weight;
            if (pick <= 0f)
                return proto;
        }
        return variants.Last().Key;
    }
}