using Content.Server.Worldgen.Components;
using Content.Server.Worldgen.Systems.Biomes;
using Content.Shared.CCVar;
using Content.Shared.EntityTable;
using Content.Shared.Ghost;
using Content.Shared.Mind.Components;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;
using System.Numerics;

namespace Content.Server.Worldgen.Systems;

/// <summary>
///     This handles putting together chunk entities and notifying them about important changes.
/// </summary>
public sealed class WorldControllerSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _xformSys = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly BiomeSelectionSystem _biomeSelection = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChunkOwnedEntitySystem _ownedEntity = default!;


    private const int PlayerLoadRadius = 2;

    private const uint MaxLoadedChunkUpdatesPerTick = 100;
    private const uint ChunkLoaderTickDelay = 60; // Every 60 ticks

    private ISawmill _sawmill = default!;
    private Dictionary<EntityUid, Dictionary<Vector2i, List<EntityUid>>> _chunksToLoad = new();
    private readonly Queue<Entity<LoadedChunkComponent, WorldChunkComponent>> _loadedChunks = new();

    /// <inheritdoc />
    public override void Initialize()
    {
        _sawmill = _logManager.GetSawmill("world");
        SubscribeLocalEvent<LoadedChunkComponent, ComponentStartup>(OnChunkLoadedCore);
        SubscribeLocalEvent<LoadedChunkComponent, ComponentShutdown>(OnChunkUnloadedCore);
        SubscribeLocalEvent<WorldChunkComponent, ComponentShutdown>(OnChunkShutdown);
    }

    /// <summary>
    ///     Handles deleting chunks properly.
    /// </summary>
    private void OnChunkShutdown(EntityUid uid, WorldChunkComponent component, ComponentShutdown args)
    {
        if (!TryComp<WorldControllerComponent>(component.Map, out var controller))
            return;

        if (HasComp<LoadedChunkComponent>(uid))
        {
            var ev = new WorldChunkUnloadedEvent(uid, component.Coordinates);
            RaiseLocalEvent(component.Map, ref ev);
            RaiseLocalEvent(uid, ref ev, broadcast: true);
        }

        controller.Chunks.Remove(component.Coordinates);
    }

    /// <summary>
    ///     Handles the inner logic of loading a chunk, i.e. events.
    /// </summary>
    private void OnChunkLoadedCore(EntityUid uid, LoadedChunkComponent component, ComponentStartup args)
    {
        if (!TryComp<WorldChunkComponent>(uid, out var chunk))
            return;

        var ev = new WorldChunkLoadedEvent(uid, chunk.Coordinates);
        RaiseLocalEvent(chunk.Map, ref ev);
        RaiseLocalEvent(uid, ref ev, broadcast: true);
        //_sawmill.Debug($"Loaded chunk {ToPrettyString(uid)} at {chunk.Coordinates}");
    }

    /// <summary>
    ///     Handles the inner logic of unloading a chunk, i.e. events.
    /// </summary>
    private void OnChunkUnloadedCore(EntityUid uid, LoadedChunkComponent component, ComponentShutdown args)
    {
        if (!TryComp<WorldChunkComponent>(uid, out var chunk))
            return;

        if (Terminating(uid))
            return; // SAFETY: This is in case a loaded chunk gets deleted, to avoid double unload.

        var ev = new WorldChunkUnloadedEvent(uid, chunk.Coordinates);
        RaiseLocalEvent(chunk.Map, ref ev);
        RaiseLocalEvent(uid, ref ev);
        //_sawmill.Debug($"Unloaded chunk {ToPrettyString(uid)} at {coords}");
    }

    /// <inheritdoc />
    public override void Update(float frameTime)
    {
        CheckChunksToLoad();
        UpdateLoadedChunks();
    }

    private void CheckChunksToLoad()
    {
        if (_gameTiming.CurTick.Value % ChunkLoaderTickDelay != 0)
            return;
        _chunksToLoad = new();

        var controllerEnum = EntityQueryEnumerator<WorldControllerComponent>();
        while (controllerEnum.MoveNext(out var uid, out _))
        {
            _chunksToLoad[uid] = new();
        }

        if (_chunksToLoad.Count == 0)
            return; // Just bail early.

        var loaderEnum = EntityQueryEnumerator<WorldLoaderComponent, TransformComponent>();

        while (loaderEnum.MoveNext(out var uid, out var worldLoader, out var xform))
        {
            var mapOrNull = xform.MapUid;
            if (mapOrNull is null)
                continue;
            var map = mapOrNull.Value;
            if (!_chunksToLoad.ContainsKey(map))
                continue;

            var wc = _xformSys.GetWorldPosition(xform);
            var coords = WorldGen.WorldToChunkCoords(wc);
            var chunks = new GridPointsNearEnumerator(coords.Floored(),
                (int)Math.Ceiling(worldLoader.Radius / (float)WorldGen.ChunkSize) + 1);

            var set = _chunksToLoad[map];

            while (chunks.MoveNext(out var chunk))
            {
                if (!set.TryGetValue(chunk.Value, out _))
                    set[chunk.Value] = new List<EntityUid>(4);
                set[chunk.Value].Add(uid);
            }
        }

        var mindEnum = EntityQueryEnumerator<MindContainerComponent, TransformComponent>();
        var ghostQuery = GetEntityQuery<GhostComponent>();

        // Mindful entities get special privilege as they're always a player and we don't want the illusion being broken around them.
        while (mindEnum.MoveNext(out var uid, out var mind, out var xform))
        {
            if (!mind.HasMind)
                continue;
            if (ghostQuery.HasComponent(uid))
                continue;
            var mapOrNull = xform.MapUid;
            if (mapOrNull is null)
                continue;
            var map = mapOrNull.Value;
            if (!_chunksToLoad.ContainsKey(map))
                continue;

            var wc = _xformSys.GetWorldPosition(xform);
            var coords = WorldGen.WorldToChunkCoords(wc);
            var chunks = new GridPointsNearEnumerator(coords.Floored(), PlayerLoadRadius);

            var set = _chunksToLoad[map];

            while (chunks.MoveNext(out var chunk))
            {
                if (!set.TryGetValue(chunk.Value, out _))
                    set[chunk.Value] = new List<EntityUid>(4);
                set[chunk.Value].Add(uid);
            }
        }



        if (_chunksToLoad.All(x => x.Value.Count == 0))
            return;

        var startTime = _gameTiming.RealTime;
        var count = 0;
        var loadedQuery = GetEntityQuery<LoadedChunkComponent>();
        var controllerQuery = GetEntityQuery<WorldControllerComponent>();
        foreach (var (map, chunks) in _chunksToLoad)
        {
            var controller = controllerQuery.GetComponent(map);
            foreach (var (chunk, loaders) in chunks)
            {
                var ent = GetOrCreateChunk(chunk, map, controller); // Ensure everything loads.
                LoadedChunkComponent? c = null;
                if (ent is not null && !loadedQuery.TryGetComponent(ent.Value, out c))
                {
                    c = AddComp<LoadedChunkComponent>(ent.Value);

                    c.LoadedOn = _gameTiming.CurTime;
                    DoEntitySpawns(ent.Value);
                    count += 1;
                }

                if (c is not null)
                    c.Loaders = loaders;
            }
        }

        if (count > 0)
        {
            var timeSpan = _gameTiming.RealTime - startTime;
            _sawmill.Debug($"Loaded {count} chunks in {timeSpan.TotalMilliseconds:N2}ms.");
        }
    }

    private void UpdateLoadedChunks()
    {
        if (_loadedChunks.Count == 0)
        {
            var loadedEnum = EntityQueryEnumerator<LoadedChunkComponent, WorldChunkComponent>();
            while (loadedEnum.MoveNext(out var uid, out var loadedChunkComp, out var chunk))
                _loadedChunks.Enqueue((uid, loadedChunkComp, chunk));
            return; // Enqueue only as this is already quite expensive.
        }
        var chunkUnloadDelay = TimeSpan.FromSeconds(_configurationManager.GetCVar(CCVars.WorldChunkUnloadDelay));
        var chunksUnloaded = 0u;
        var iterations = 0u;

        while (iterations < MaxLoadedChunkUpdatesPerTick && _loadedChunks.TryDequeue(out var ent))
        {
            iterations++;
            if (Exists(ent) && !TerminatingOrDeleted(ent) && !ent.Comp1.Deleted)
            {
                ent.Deconstruct(out var uid, out var loadedChunkComp, out var chunk);
                var coords = chunk.Coordinates;

                if (_chunksToLoad.ContainsKey(chunk.Map) && !_chunksToLoad[chunk.Map].ContainsKey(coords) && loadedChunkComp.LoadedOn + chunkUnloadDelay < _gameTiming.CurTime)
                {
                    // Make sure these chunks get unloaded at the end of the tick.
                    RemCompDeferred<LoadedChunkComponent>(uid);
                    chunksUnloaded++;
                }
            }
        }

        if (chunksUnloaded > 0)
            _sawmill.Debug($"Queued {chunksUnloaded} chunks for unload.");
    }

    private void DoEntitySpawns(EntityUid uid)
    {
        if (!TryComp<WorldChunkComponent>(uid, out var worldChunkComp)) return;
        var biomeProto = _biomeSelection.GetBiomeForChunk(new Entity<WorldChunkComponent>(uid, worldChunkComp));
        if (
            biomeProto != null
            && biomeProto.ChunkEntityTable.HasValue
            && _protoMan.Resolve(biomeProto.ChunkEntityTable, out var type)
        )
        {
            var chunkCenter = WorldGen.ChunkToWorldCoordsCentered(worldChunkComp.Coordinates);
            var halfChunk = WorldGen.ChunkSize / 2f;
            foreach (var spawnProto in _entityTable.GetSpawns(type))
            {
                var rX = chunkCenter.X + _random.NextFloat(-halfChunk, halfChunk);
                var rY = chunkCenter.Y + _random.NextFloat(-halfChunk, halfChunk);
                var randomCoords = new Vector2(rX, rY);

                _ownedEntity.GenerateEntity(spawnProto, new EntityCoordinates(worldChunkComp.Map, randomCoords));
            }
        }
    }

    /// <summary>
    ///     Attempts to get a chunk, creating it if it doesn't exist.
    /// </summary>
    /// <param name="chunk">Chunk coordinates to get the chunk entity for.</param>
    /// <param name="map">Map the chunk is in.</param>
    /// <param name="controller">The controller this chunk belongs to.</param>
    /// <returns>A chunk, if available.</returns>
    [Pure]
    public EntityUid? GetOrCreateChunk(Vector2i chunk, EntityUid map, WorldControllerComponent? controller = null)
    {
        if (!Resolve(map, ref controller))
            throw new Exception($"Tried to use {ToPrettyString(map)} as a world map, without actually being one.");

        if (controller.Chunks.TryGetValue(chunk, out var ent))
            return ent;
        return CreateChunkEntity(chunk, map, controller);
    }

    /// <summary>
    ///     Constructs a new chunk entity, attaching it to the map.
    /// </summary>
    /// <param name="chunkCoords">The coordinates the new chunk should be initialized for.</param>
    /// <param name="map"></param>
    /// <param name="controller"></param>
    /// <returns></returns>
    private EntityUid CreateChunkEntity(Vector2i chunkCoords, EntityUid map, WorldControllerComponent controller)
    {
        var chunk = Spawn(controller.ChunkProto, MapCoordinates.Nullspace);
        StartupChunkEntity(chunk, chunkCoords, map, controller);
        _metaData.SetEntityName(chunk, $"Chunk {chunkCoords.X}/{chunkCoords.Y}");
        return chunk;
    }

    private void StartupChunkEntity(EntityUid chunk, Vector2i coords, EntityUid map,
        WorldControllerComponent controller)
    {
        if (!TryComp<WorldChunkComponent>(chunk, out var chunkComponent))
        {
            _sawmill.Error($"Chunk {ToPrettyString(chunk)} is missing WorldChunkComponent.");
            return;
        }

        ref var chunks = ref controller.Chunks;

        chunks[coords] = chunk; // Add this entity to chunk index.
        chunkComponent.Coordinates = coords;
        chunkComponent.Map = map;
        var ev = new WorldChunkAddedEvent(chunk, coords);
        RaiseLocalEvent(map, ref ev, broadcast: true);
    }
}

/// <summary>
///     A directed event fired when a chunk is initially set up in the world. The chunk is not loaded at this point.
/// </summary>
[ByRefEvent]
[PublicAPI]
public readonly record struct WorldChunkAddedEvent(EntityUid Chunk, Vector2i Coords);

/// <summary>
///     A directed event fired when a chunk is loaded into the world, i.e. a player or other world loader has entered vicinity.
/// </summary>
[ByRefEvent]
[PublicAPI]
public readonly record struct WorldChunkLoadedEvent(EntityUid Chunk, Vector2i Coords);

/// <summary>
///     A directed event fired when a chunk is unloaded from the world, i.e. no world loaders remain nearby.
/// </summary>
[ByRefEvent]
[PublicAPI]
public readonly record struct WorldChunkUnloadedEvent(EntityUid Chunk, Vector2i Coords);
