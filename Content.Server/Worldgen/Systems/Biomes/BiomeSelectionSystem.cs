using Content.Server.Worldgen.Components;
using Content.Server.Worldgen.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using System.Linq;

namespace Content.Server.Worldgen.Systems.Biomes;

/// <summary>
///     This handles biome selection, evaluating which biome to apply to a chunk based on noise channels.
/// </summary>
public sealed class BiomeSelectionSystem : BaseWorldSystem
{
    [Dependency] private readonly NoiseIndexSystem _noiseIdx = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ISerializationManager _ser = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<BiomeSelectionComponent, ComponentStartup>(OnBiomeSelectionStartup);
        SubscribeLocalEvent<BiomeSelectionComponent, WorldChunkAddedEvent>(OnWorldChunkAdded);
    }

    /// <summary>
    /// Gets the biome prototype from its ID while inheriting parent's noise ranges.
    /// </summary>
    /// <param name="biomeId"></param>
    /// <returns></returns>
    private BiomePrototype GetBiomePrototype(string biomeId)
    {
        var biome = _proto.Index<BiomePrototype>(biomeId);

        if (biome.Parents != null)
            foreach (var parentBiomeID in biome.Parents)
            {
                var parentBiome = GetBiomePrototype(parentBiomeID);
                foreach (var noiseRange in parentBiome.NoiseRanges)
                    biome.NoiseRanges[noiseRange.Key] = noiseRange.Value;
            }
        return biome;
    }

    private void OnWorldChunkAdded(EntityUid uid, BiomeSelectionComponent component, ref WorldChunkAddedEvent args)
    {
        if (!TryComp<WorldChunkComponent>(args.Chunk, out var chunkComponent))
            return;

        var biomeProto = GetBiomeForChunk(new Entity<WorldChunkComponent>(args.Chunk, chunkComponent));

        if (biomeProto != null)
            biomeProto.Apply(args.Chunk, _ser, EntityManager);
        else
            Log.Error($"Biome selection ran out of biomes to select? See biomes list: {component.Biomes}");
    }

    public BiomePrototype? GetBiomeForChunk(Entity<WorldChunkComponent> chunk)
    {
        if (!TryComp<BiomeSelectionComponent>(chunk.Comp.Map, out var selection))
            return null;

        foreach (var biomeId in selection.Biomes)
        {
            var biome = GetBiomePrototype(biomeId);

            if (!CheckBiomeValidity(chunk.Comp.Map, biome, chunk.Comp.Coordinates))
                continue;

            return biome;
        }
        return null;
    }

    private void OnBiomeSelectionStartup(EntityUid uid, BiomeSelectionComponent component, ComponentStartup args)
    {
        // surely this can't be THAAAAAAAAAAAAAAAT bad right????
        var sorted = component.Biomes
            .Select(x => (Id: x, _proto.Index<BiomePrototype>(x).Priority))
            .OrderByDescending(x => x.Priority)
            .Select(x => x.Id)
            .ToList();

        component.Biomes = sorted; // my hopes and dreams rely on this being pre-sorted by priority.
    }

    private bool CheckBiomeValidity(EntityUid uid, BiomePrototype biome, Vector2i coords)
    {
        foreach (var (noise, ranges) in biome.NoiseRanges)
        {
            var value = _noiseIdx.Evaluate(uid, noise, coords);
            var anyValid = false;
            foreach (var range in ranges)
            {
                if (range.X < value && value < range.Y)
                {
                    anyValid = true;
                    break;
                }
            }

            if (!anyValid)
                return false;
        }

        return true;
    }
}

