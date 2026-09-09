using System.Linq;
using Content.Server.Anomaly.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Audio;
using Content.Server.Materials;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Persistence14.RandomTable;
using Content.Shared._Persistence14.RandomTable.State;
using Content.Shared._Persistence14.Research.Anomalies;
using Content.Shared._Persistence14.Research.Anomalies.Modules;
using Content.Shared.Anomaly;
using Content.Shared.CCVar;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Materials;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Radio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Persistence14.Research.Anomalies;

public sealed partial class AnomalyGeneratorSystem : SharedAnomalyGeneratorSystem
{
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly SharedAnomalyCapsuleSystem _capsules = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _time = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly MaterialStorageSystem _material = default!;
    [Dependency] private readonly AmbientSoundSystem _ambient = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RandomTableSystem _randomTable = default!;
    private const int RandomCoordinateAttempts = 25;
    private const string Sawmill = "anomaly-generator";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnomalyGeneratorComponent, GenerateAnomalyEvent>(OnGenerateAnomaly);
        SubscribeLocalEvent<AnomalyGeneratorComponent, UpdateAnomalyGeneratorUIEvent>(OnUpdateUIEvent);
        SubscribeLocalEvent<AnomalyGeneratorComponent, BoundUIOpenedEvent>(OnBUIOpen);
        SubscribeLocalEvent<AnomalyGeneratorComponent, MaterialAmountChangedEvent>(OnMaterialQtyChange);
        SubscribeLocalEvent<AnomalyGeneratorComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<AnomalyGeneratorComponent, ItemSlotInsertAttemptEvent>(OnCapsuleInsertAttempt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<GeneratingAnomalyGeneratorComponent, AnomalyGeneratorComponent>();
        while (query.MoveNext(out var ent, out var active, out var gen))
        {
            if (_time.CurTime < active.EndTime)
                continue;

            active.AudioStream = _audio.Stop(active.AudioStream);
            FinishAnomalyGenerator((ent, gen));
        }
    }

    #region Event Hooks
    private void OnGenerateAnomaly(Entity<AnomalyGeneratorComponent> generator, ref GenerateAnomalyEvent args)
    {
        StartAnomalyGenerator(generator);
    }

    private void OnBUIOpen(Entity<AnomalyGeneratorComponent> generator, ref BoundUIOpenedEvent args)
    {
        UpdateGeneratorUi(generator);
    }

    private void OnMaterialQtyChange(Entity<AnomalyGeneratorComponent> generator, ref MaterialAmountChangedEvent args)
    {
        UpdateGeneratorUi(generator);
    }

    private void OnPowerChanged(Entity<AnomalyGeneratorComponent> generator, ref PowerChangedEvent args)
    {
        _ambient.SetAmbience(generator.Owner, args.Powered);
        if (args.Powered)
            return;

        CancelAnomalyGenerator(generator);
    }

    private void OnUpdateUIEvent(Entity<AnomalyGeneratorComponent> generator, ref UpdateAnomalyGeneratorUIEvent args) => UpdateGeneratorUi(generator);

    private void OnCapsuleInsertAttempt(Entity<AnomalyGeneratorComponent> generator, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Slot.ID != generator.Comp.CapsuleContainer)
            return;

        if (!TryComp<AnomalyCapsuleComponent>(args.Item, out var capsuleComp))
            return; // Should be caught by the whitelist...

        Entity<AnomalyCapsuleComponent> capsule = (args.Item, capsuleComp);

        if (_capsules.HasCore(capsule))
            return; // Insert as normal

        args.Cancelled = true;

        if (args.User is { } user)
        {
            _popup.PopupEntity(Loc.GetString("anomaly-generator-capsule-missing-core"), generator.Owner, user);
        }
    }
    #endregion

    #region UI

    private void UpdateGeneratorUi(Entity<AnomalyGeneratorComponent> generator)
    {
        var isGenerating = TryComp<GeneratingAnomalyGeneratorComponent>(generator.Owner, out var generatingComp);
        var isOnCooldown = _time.CurTime < generator.Comp.CooldownEndTime;

        var canGenerate = CanGenerateAnomaly(generator, out _);
        var hasCapsule = TryGetAnomalyCapsule(generator, out var capsule);

        var material = _material.GetMaterialAmount(generator.Owner, generator.Comp.RequiredMaterial);


        var list = new Dictionary<ProtoId<AnomalyPrototype>, float>();
        if (hasCapsule && _capsules.TryGetCore(capsule, out var core))
        {
            list = _randomTable.ListPrototype<AnomalyPrototype>(core.Comp.AnomalyPool).ToDictionary(
                pair => new ProtoId<AnomalyPrototype>(pair.prototype.ID),
                pair => pair.prob
            );
        }

        var forcedEnvironmental = false;
        var forcedInfectious = false;
        if (hasCapsule)
        {
            foreach (var module in _capsules.GetModules(capsule))
            {
                if (TryComp<CategorySpecifierCapsuleModuleComponent>(module.Owner, out var comp))
                {
                    if (comp.Category == AnomalyCategory.Environmental) forcedEnvironmental = true;
                    if (comp.Category == AnomalyCategory.Infectious) forcedInfectious = true;
                }
            }
        }


        var state = new AnomalyGeneratorBUIState
        {
            GenerateDuration = generator.Comp.GenerationLength,
            GenerateEndTime = isGenerating ? generatingComp?.EndTime : null,
            CooldownDuration = generator.Comp.CooldownLength,
            CooldownEndTime = isOnCooldown ? generator.Comp.CooldownEndTime : null,

            CanGenerateAnomaly = canGenerate,

            MaterialAmount = material / 100f,
            MaterialRequired = generator.Comp.MaterialPerAnomaly / 100f,
            Capsule = hasCapsule ? GetNetEntity(capsule.Owner) : null,

            AnomalyProbabilities = list,
            ForcedEnvironmental = forcedEnvironmental,
            ForcedInfectious = forcedInfectious
        };
        _ui.SetUiState(generator.Owner, AnomalyGeneratorUiKey.Key, state);
    }

    #endregion

    #region Generation
    private bool CanGenerateAnomaly(Entity<AnomalyGeneratorComponent> generator) => CanGenerateAnomaly(generator, out _);
    private bool CanGenerateAnomaly(Entity<AnomalyGeneratorComponent> generator, out Entity<AnomalyCapsuleComponent> capsule)
    {
        capsule = default!;

        if (!this.IsPowered(generator.Owner, EntityManager))
            return false; // Generator is unpowered

        if (_material.GetMaterialAmount(generator.Owner, generator.Comp.RequiredMaterial) < generator.Comp.MaterialPerAnomaly)
            return false; // Not enough fuel

        if (!TryGetAnomalyCapsule(generator, out capsule))
            return false; // No capsule

        return true;
    }

    private bool CanStartAnomaly(Entity<AnomalyGeneratorComponent> generator)
    {
        if (_time.CurTime < generator.Comp.CooldownEndTime)
            return false; // Still on cooldown.

        if (HasComp<GeneratingAnomalyGeneratorComponent>(generator.Owner))
            return false; // Already started

        return true;
    }

    /// <summary>
    /// Attempts to generate an anomaly using the capsule contained within the generator. The type of anomaly and its location depend on the capsule used.
    /// </summary>
    private bool TryGenerateAnomaly(Entity<AnomalyGeneratorComponent> generator)
    {
        if (!CanGenerateAnomaly(generator, out var capsule))
            return false;
        var tableState = EnsureComp<RandomTableStateComponent>(generator.Owner);
        var ev = new AnomalyGeneratorAttemptEvent
        {
            Context = new AnomalyGenerationContext
            {
                GeneratorUid = generator.Owner,
                Capsule = capsule,
            }
        };
        _capsules.RelayEventToModules(capsule, ref ev);
        RaiseLocalEvent(ref ev);
        if (ev.Cancelled)
            return false;

        if (!_capsules.TryGetAnomalyPrototype(capsule, out var anomalyPrototype))
            return false;

        if (ev.Context.ForceEnvironmental && ev.Context.ForceInfectious)
            return false;

        if (!(ev.Context.ForceEnvironmental || ev.Context.ForceInfectious) && !anomalyPrototype.TryGetSpawnableProtoId(_random, out var spawnable))
            return false;

        if (ev.Context.ForceEnvironmental && !anomalyPrototype.TrySpawnEnvironmental(_random, out spawnable))
            return false;

        if (ev.Context.ForceInfectious && !anomalyPrototype.TrySpawnInfectious(_random, out spawnable))
            return false;

        if (ev.Context.TargetCoordinates is not { } coordinates && !TryGetCoordinatesOnEntitysGrid(generator.Owner, out coordinates))
            return false;

        if (!_material.TryChangeMaterialAmount(generator.Owner, generator.Comp.RequiredMaterial, -generator.Comp.MaterialPerAnomaly))
            return false;

        QueueDel(capsule.Owner); // Delete the used capsule
        var spawn = Spawn(spawnable.Id, coordinates); // spawnable is assigned, I promise.
        LogManager.GetSawmill(Sawmill).Info($"An anomaly ({ToPrettyString(spawn)}) was generated at these coordinates: {coordinates}");
        return true;
    }

    /// <summary>
    /// Spawns an anomaly at a random point on a target grid.
    /// </summary>
    public void SpawnAnomalyOnGrid(EntityUid gridUid, EntProtoId anomalyProtoId)
    {
        if (!TryGetCoordinatesOnGrid(gridUid, out var coordinates))
        {
            LogManager.GetSawmill(Sawmill).Warning($"Attempted to manually spawn anomaly but failed to find valid coordinates on grid {ToPrettyString(gridUid)}.");
            return;
        }

        var spawn = Spawn(anomalyProtoId, coordinates);
        LogManager.GetSawmill(Sawmill).Info($"An anomaly ({ToPrettyString(spawn)}) was generated at these coordinates: {coordinates}");
    }

    /// <summary>
    /// Spawns an anomaly at a random point on the same grid as the target entity.
    /// </summary>
    public void SpawnAnomalyOnEntityGrid(EntityUid targetEntityUid, EntProtoId anomalyProtoId)
    {
        if (!TryGetCoordinatesOnEntitysGrid(targetEntityUid, out var coordinates))
        {
            LogManager.GetSawmill(Sawmill).Warning($"Attempted to manually spawn anomaly but failed to find valid coordinates on entity {ToPrettyString(targetEntityUid)}'s grid.");
            return;
        }

        var spawn = Spawn(anomalyProtoId, coordinates);
        LogManager.GetSawmill(Sawmill).Info($"An anomaly ({ToPrettyString(spawn)}) was generated at these coordinates: {coordinates}");
    }

    /// <summary>
    /// Spawns an anomaly at a specific set of coordinates.
    /// </summary>
    public void SpawnAnomalyAtCoordinates(EntityCoordinates coordinates, EntProtoId anomalyProtoId)
    {
        var spawn = Spawn(anomalyProtoId, coordinates);
        LogManager.GetSawmill(Sawmill).Info($"An anomaly ({ToPrettyString(spawn)}) was generated at these coordinates: {coordinates}");
    }

    /// <summary>
    /// Attempts to get a random set of coordinates from the grid containing the target entity.
    /// </summary>
    private bool TryGetCoordinatesOnEntitysGrid(EntityUid targetUid, out EntityCoordinates coordinates)
    {
        coordinates = default!;
        var xform = Transform(targetUid);

        if (xform.GridUid is not { } gridUid) // Generator isn't on a grid. For some reason.
            return false;

        return TryGetCoordinatesOnGrid(gridUid, out coordinates);
    }

    /// <summary>
    /// Attempts to get a random set of coordinates from a specific grid entity.
    /// </summary>
    private bool TryGetCoordinatesOnGrid(EntityUid gridUid, out EntityCoordinates coordinates)
    {
        coordinates = default!;
        if (!TryComp<MapGridComponent>(gridUid, out var gridComp))
            return false;

        Entity<MapGridComponent> grid = (gridUid, gridComp);
        var gridBounds = gridComp.LocalAABB.Scale(_configuration.GetCVar(CCVars.AnomalyGenerationGridBoundsScale));
        var xform = Transform(grid.Owner);

        for (int i = 0; i < RandomCoordinateAttempts; i++)
        {
            var randomX = _random.Next((int)gridBounds.Left, (int)gridBounds.Right);
            var randomY = _random.Next((int)gridBounds.Bottom, (int)gridBounds.Top);

            var tile = new Vector2i(randomX, randomY);

            // No Air-Blocked Areas
            if (_atmos.IsTileSpace(grid.Owner, xform.MapUid, tile) ||
                _atmos.IsTileAirBlocked(grid, tile))
                continue;

            // Don't spawn inside solid things
            var physQuery = GetEntityQuery<PhysicsComponent>();
            var valid = true;
            foreach (var ent in _map.GetAnchoredEntities(grid, gridComp, tile))
            {
                if (!physQuery.TryGetComponent(ent, out var body))
                    continue;
                if (body.BodyType != BodyType.Static ||
                    !body.Hard ||
                    (body.CollisionLayer & (int)CollisionGroup.Impassable) == 0)
                    continue;

                valid = false;
                break;
            }
            if (!valid)
                continue;

            var pos = _map.GridTileToLocal(grid, gridComp, tile);
            var mapPos = _transform.ToMapCoordinates(pos);

            // Don't spawn in Anti-Anomaly Zones
            var antiAnomalyZonesQueue = AllEntityQuery<AntiAnomalyZoneComponent, TransformComponent>();
            while (antiAnomalyZonesQueue.MoveNext(out _, out var zone, out var anitXform))
            {
                if (anitXform.MapID != mapPos.MapId)
                    continue; // Not the same map.

                var antiCoordinates = _transform.GetWorldPosition(anitXform);
                var delta = antiCoordinates - mapPos.Position;
                if (delta.LengthSquared() < zone.ZoneRadius * zone.ZoneRadius)
                {
                    valid = false;
                    break;
                }
            }
            if (!valid)
                continue;

            coordinates = pos;
            return true;
        }
        LogManager.GetSawmill(Sawmill).Warning($"Anomaly generator ({ToPrettyString(grid.Owner)}) was unable to find a valid spawn location in {RandomCoordinateAttempts} attempts.");
        return false; // No valid point found.
    }
    #endregion

    #region Lifecycle
    /// <summary>
    /// Starts up the anomaly generator applied necessary components and playing sound effects.
    /// </summary>
    private void StartAnomalyGenerator(Entity<AnomalyGeneratorComponent> generator)
    {
        if (!CanGenerateAnomaly(generator) || !CanStartAnomaly(generator)) // Already generating
            return;

        var generatingComp = EnsureComp<GeneratingAnomalyGeneratorComponent>(generator.Owner);
        generatingComp.EndTime = _time.CurTime + generator.Comp.GenerationLength;
        generatingComp.AudioStream = _audio.PlayPvs(generator.Comp.GeneratingSound, generator.Owner, AudioParams.Default.WithLoop(true))?.Entity;
        generator.Comp.CooldownEndTime = _time.CurTime + generator.Comp.CooldownLength;
        _appearance.SetData(generator.Owner, AnomalyGeneratorVisuals.Generating, true);
        UpdateGeneratorUi(generator);
    }

    /// <summary>
    /// Actually runs all the generation code and effects. Taken pretty much wholesale from AnomalySystem.Generator.
    /// </summary>
    private void FinishAnomalyGenerator(Entity<AnomalyGeneratorComponent> generator)
    {
        RemComp<GeneratingAnomalyGeneratorComponent>(generator.Owner);
        _appearance.SetData(generator.Owner, AnomalyGeneratorVisuals.Generating, false);

        if (!TryGenerateAnomaly(generator))
        {
            return; // Should probably do *something* if it fails to generate...
        }

        _audio.PlayPvs(generator.Comp.GeneratingFinishedSound, generator.Owner);
        var message = Loc.GetString("anomaly-generator-announcement");
        _radio.SendRadioMessage(generator.Owner, message, _prototype.Index<RadioChannelPrototype>(generator.Comp.ScienceChannel), generator.Owner);
        UpdateGeneratorUi(generator);
    }

    private void CancelAnomalyGenerator(Entity<AnomalyGeneratorComponent> generator)
    {
        RemComp<GeneratingAnomalyGeneratorComponent>(generator.Owner);

        _appearance.SetData(generator.Owner, AnomalyGeneratorVisuals.Generating, false);
        UpdateGeneratorUi(generator);
    }

    #endregion

    /// <summary>
    /// Attempts to retrieve the anomaly capsule from the item slot.
    /// </summary>
    private bool TryGetAnomalyCapsule(Entity<AnomalyGeneratorComponent> generator, out Entity<AnomalyCapsuleComponent> capsule)
    {
        capsule = default!;

        if (!_slots.TryGetSlot(generator.Owner, generator.Comp.CapsuleContainer, out var slot) ||
            slot.Item is not { } capsuleUid ||
            !TryComp<AnomalyCapsuleComponent>(capsuleUid, out var capsuleComp))
            return false;

        capsule = (capsuleUid, capsuleComp);
        return true;
    }
}