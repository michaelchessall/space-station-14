using Content.Server.Worldgen.Components;
using Content.Server.Worldgen.Components.Debris;
using Content.Server.Worldgen.Systems.Biomes;
using Content.Shared.EntityTable;
using Content.Shared.Maps;
using Content.Shared.Mind.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Worldgen.Systems.Debris;

/// <summary>
///     This handles populating simple structures, simply using a loot table for each tile.
/// </summary>
public sealed class SimpleFloorPlanPopulatorSystem : BaseWorldSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly BiomeSelectionSystem _biomeSelection = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly WorldControllerSystem _world = default!;
    [Dependency] private readonly ChunkOwnedEntitySystem _ownedEntity = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<SimpleFloorPlanPopulatorComponent, LocalStructureLoadedEvent>(OnFloorPlanBuilt);
    }

    private void OnFloorPlanBuilt(EntityUid uid, SimpleFloorPlanPopulatorComponent component,
        LocalStructureLoadedEvent args)
    {
        var placeables = new List<string?>(4);
        var grid = Comp<MapGridComponent>(uid);
        var enumerator = _map.GetAllTilesEnumerator(uid, grid);
        while (enumerator.MoveNext(out var tile))
        {
            var coords = _map.GridTileToLocal(uid, grid, tile.Value.GridIndices);
            var selector = _turf.GetContentTileDefinition(tile.Value).ID;
            if (!component.Caches.TryGetValue(selector, out var cache))
                continue;

            placeables.Clear();
            cache.GetSpawns(_random, ref placeables);

            foreach (var proto in placeables)
            {
                if (proto is null)
                    continue;

                var spawned = Spawn(proto, coords);
                if (HasComp<MindContainerComponent>(spawned))
                    _ownedEntity.BeginTrackingEntity(spawned);
            }
        }

        // Spawn Mobs
        if (
            !TryComp<OwnedDebrisComponent>(uid, out var _)
        )
            return;

        var map = _transform.GetMap(uid);
        if (map == null)
            return; // ...

        var bounds = _transform.GetWorldMatrix(uid).TransformBox(grid.LocalAABB);
        var worldChunk = _world.GetOrCreateChunk(WorldGen.WorldToChunkCoords(bounds.Center).Ceiled(), map.Value);
        if (worldChunk == null || !TryComp<WorldChunkComponent>(worldChunk, out var worldChunkComp))
            return;

        var biomeProto = _biomeSelection.GetBiomeForChunk(new Entity<WorldChunkComponent>(worldChunk.Value, worldChunkComp));
        if (
            biomeProto != null
            && biomeProto.DebrisEntityTable.HasValue
            && _protoMan.Resolve(biomeProto.DebrisEntityTable, out var type)
        )
        {
            var spawns = _entityTable.GetSpawns(type);
            foreach (var proto in spawns)
            {
                var randomCoords = new EntityCoordinates(
                    map.Value,
                    bounds.Center + _random.NextAngle().ToVec() * (float)(Math.Ceiling(grid.LocalAABB.MaxDimension / 2f) + _random.Next(1, 4))
                );
                _ownedEntity.GenerateEntity(proto, randomCoords);
            }
        }
    }
}

