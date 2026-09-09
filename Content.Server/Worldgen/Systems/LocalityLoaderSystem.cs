using Content.Server.Worldgen.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.Worldgen.Systems;

/// <summary>
///     This handles loading in objects based on distance from player, using some metadata on chunks.
/// </summary>
public sealed class LocalityLoaderSystem : BaseWorldSystem
{
    [Dependency] private readonly TransformSystem _xformSys = default!;

    /// <summary>
    /// Max amount of LocalityLoaders to check per tick
    /// </summary>
    private const uint MaxChecksPerTick = 50;
    private readonly Queue<Entity<LocalityLoaderComponent, TransformComponent>> _pendingLocs = new();

    /// <inheritdoc />
    public override void Update(float frameTime)
    {
        if (_pendingLocs.Count == 0)
        {
            var loadedEnum = EntityQueryEnumerator<LocalityLoaderComponent, TransformComponent>();
            while (loadedEnum.MoveNext(out var uid, out var loadedChunkComp, out var chunk))
                _pendingLocs.Enqueue((uid, loadedChunkComp, chunk));
            return; // Enqueue only as this is already quite expensive.
        }

        var loadedQuery = GetEntityQuery<LoadedChunkComponent>();
        var xformQuery = GetEntityQuery<TransformComponent>();
        var controllerQuery = GetEntityQuery<WorldControllerComponent>();

        var iterations = 0u;
        while (iterations < MaxChecksPerTick && _pendingLocs.TryDequeue(out var ent))
        {
            iterations++;
            if (Exists(ent) && !TerminatingOrDeleted(ent) && !ent.Comp1.Deleted)
            {
                ent.Deconstruct(out var uid, out var loadable, out var xform);
                if (!controllerQuery.TryGetComponent(xform.MapUid, out var controller))
                {
                    RaiseLocalEvent(uid, new LocalStructureLoadedEvent());
                    RemCompDeferred<LocalityLoaderComponent>(uid);
                    continue;
                }

                var coords = GetChunkCoords(uid, xform);
                var done = false;
                for (var i = -1; i < 2 && !done; i++)
                {
                    for (var j = -1; j < 2 && !done; j++)
                    {
                        var chunk = GetOrCreateChunk(coords + (i, j), xform.MapUid!.Value, controller);
                        if (!loadedQuery.TryGetComponent(chunk, out var loaded) || loaded.Loaders is null)
                            continue;

                        foreach (var loader in loaded.Loaders)
                        {
                            if (!xformQuery.TryGetComponent(loader, out var loaderXform))
                                continue;

                            if ((_xformSys.GetWorldPosition(loaderXform) - _xformSys.GetWorldPosition(xform)).IsLongerThan(loadable.LoadingDistance))
                                continue;

                            RaiseLocalEvent(uid, new LocalStructureLoadedEvent());
                            RemCompDeferred<LocalityLoaderComponent>(uid);
                            done = true;
                            break;
                        }
                    }
                }
            }
        }
    }
}

/// <summary>
///     A directed fired on a loadable entity when a local loader enters it's vicinity.
/// </summary>
public record struct LocalStructureLoadedEvent;

