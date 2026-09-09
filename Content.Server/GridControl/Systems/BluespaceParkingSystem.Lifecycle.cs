using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.Persistence.Systems;
using Content.Server.Popups;
using Content.Server.Shuttles.Systems;
using Content.Shared.CCVar;
using Content.Shared.Coordinates;
using Content.Shared.Database;
using Content.Shared.GridControl.Components;
using Content.Shared.GridControl.Systems;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Content.Server.GridControl.Systems;

public sealed partial class BluespaceParkingSystem : SharedBluespaceParkingSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PersistenceSystem _persistence = default!;
    [Dependency] private readonly IResourceManager _resMan = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DockingSystem _dock = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;

    [GeneratedRegex("[^a-zA-Z0-9 -]")]
    private static partial Regex SafeGridNameRgx();

    private readonly SoundSpecifier _startupSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_begin.ogg")
    {
        Params = AudioParams.Default.WithVolume(-5f),
    };
    private readonly SoundSpecifier _unparkingSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_end.ogg")
    {
        Params = AudioParams.Default.WithVolume(-5f),
    };

    private EntityQuery<BSPAnchorKeyComponent> _anchorKeyQuery = default!;

    public void InitializeLifecycle()
    {
        _anchorKeyQuery = GetEntityQuery<BSPAnchorKeyComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var activeTargets = EntityQueryEnumerator<BSPParkingTargetComponent>();

        while (activeTargets.MoveNext(out var uid, out var comp))
        {
            if (TerminatingOrDeleted(uid) || !comp.Initialized) continue;
            if (!comp.Source.IsValid() || !_anchorKeyQuery.TryComp(comp.Source, out var key) || key == null || key.State != BSPState.Parking)
            {
                RemCompDeferred<BSPParkingTargetComponent>(uid);
                continue;
            }

            var remainingTime = key.RoutineStartTime.GetValueOrDefault() + TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.BluespaceParkingParkDelay)) - _timing.CurTime;
            if (!comp.RampingUp && remainingTime.TotalSeconds <= 8)
            {
                // Do some crazy nonsense
                comp.RampingUp = true;
                var audio = _audio.PlayPvs(_startupSound, uid);
                _audio.SetGridAudio(audio);
                comp.StartupStream = audio?.Entity;
            }
            else if (remainingTime.TotalSeconds <= 0)
            {
                Entity<BSPAnchorKeyComponent> ent = (comp.Source, key);
                if (!CommitPark(ent, (uid, comp)))
                    CancelRoutine(ent, "Could not park.");

            }

        }

        var activeUnparks = EntityQueryEnumerator<BSPAnchorKeyUnparkingComponent>();
        while (activeUnparks.MoveNext(out var uid, out var comp))
        {
            if (TerminatingOrDeleted(uid) || !comp.Initialized) continue;
            if (!_anchorKeyQuery.TryComp(uid, out var key) || key == null || key.State != BSPState.Unparking)
            {
                RemCompDeferred<BSPAnchorKeyUnparkingComponent>(uid);
                continue;
            }
            var remainingTime = key.RoutineStartTime.GetValueOrDefault() + TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.BluespaceParkingUnparkDelay)) - _timing.CurTime;
            if (remainingTime.TotalSeconds <= 0)
            {
                Entity<BSPAnchorKeyComponent> ent = (uid, key);
                if (!CommitUnpark(ent, comp))
                    CancelRoutine(ent, "Could not find a suitable location to unpark at.");
            }
        }
    }

    private void StartPark(Entity<BSPAnchorKeyComponent> entity)
    {
        var currentState = GetUIState(entity, entity.Comp);
        if (currentState == null) return;
        if (currentState.State != BSPState.Idle) return;
        if (!currentState.CanAct) return;

        // Start parking sequence.

        entity.Comp.State = BSPState.Parking;
        entity.Comp.CurrentTarget = _transform.GetGrid(entity.Owner);
        entity.Comp.RoutineStartTime = _timing.CurTime;
        var target = EnsureComp<BSPParkingTargetComponent>(entity.Comp.CurrentTarget!.Value);
        target.Source = entity;

        UpdateUserInterface(entity.Owner, entity.Comp);
        _adminLog.Add(LogType.BluespaceParking, LogImpact.Low, $"Started parking for the grid '{entity.Comp.SavedGridName}' ({entity.Comp.CurrentTarget}).");
    }

    private void StartUnpark(Entity<BSPAnchorKeyComponent> entity)
    {
        var currentState = GetUIState(entity, entity.Comp);
        if (currentState == null) return;
        if (currentState.State != BSPState.Parked) return;
        if (!currentState.CanAct) return;

        var curPos = _transform.GetWorldPosition(entity);
        var vector = entity.Comp.SavedParkedPosition.GetValueOrDefault() - curPos;
        if (vector.Length() > _cfg.GetCVar(CCVars.BluespaceUnparkMaxDistance))
            return;

        var position = _transform.GetWorldPosition(entity);
        var rotation = _transform.GetWorldRotation(entity);

        // Check if it can unpark here
        GetRecallSource(entity, position, rotation.Opposite(), out var mapId, out var origin, out var bounds, out var worldAngle);
        if (!TryGetUnparkPlacementLocation(mapId, origin, bounds, worldAngle, out _, out _, spawnDistance: 2, maxIterations: 4))
        {
            _popup.PopupEntity($"Cannot unpark here! Go somewhere less crowded.", entity, Shared.Popups.PopupType.MediumCaution);
            return;
        }

        entity.Comp.RoutineStartTime = _timing.CurTime;
        entity.Comp.State = BSPState.Unparking;
        var unparkingState = EnsureComp<BSPAnchorKeyUnparkingComponent>(entity);
        unparkingState.Origin = position;
        unparkingState.Rotation = rotation;

        UpdateUserInterface(entity.Owner, entity.Comp);
        _adminLog.Add(LogType.BluespaceParking, LogImpact.Low, $"Started unparking for the grid '{entity.Comp.SavedGridName}'.");

    }

    private void CancelRoutine(Entity<BSPAnchorKeyComponent> entity, string motive, bool updateUI = true, bool popupNotification = true)
    {
        if (!new BSPState[] { BSPState.Parking, BSPState.Unparking }.Contains(entity.Comp.State)) return;

        entity.Comp.RoutineStartTime = null;
        var currentState = entity.Comp.State;

        switch (currentState)
        {
            case BSPState.Parking:
                {
                    var target = entity.Comp.CurrentTarget;
                    if (
                        target.HasValue
                        && target.Value.IsValid()
                        && !TerminatingOrDeleted(target.Value)
                        && TryComp<BSPParkingTargetComponent>(target.Value, out var targetComp)
                    )
                    {
                        _audio.Stop(targetComp.StartupStream);
                        RemCompDeferred<BSPParkingTargetComponent>(target.Value);
                    }
                    entity.Comp.CurrentTarget = null;
                    entity.Comp.State = BSPState.Idle;
                    break;
                }
            case BSPState.Unparking:
                {
                    entity.Comp.State = BSPState.Parked;

                    if (TryComp<BSPAnchorKeyUnparkingComponent>(entity, out var comp))
                    {
                        RemCompDeferred<BSPAnchorKeyUnparkingComponent>(entity);
                    }
                    break;
                }
        }

        var notification = $"Action has been canceled.";
        if (!string.IsNullOrEmpty(motive))
            notification += $" ({motive})";

        if (popupNotification)
        {
            _adminLog.Add(LogType.BluespaceParking, LogImpact.Low, $"{currentState} was canceled for the grid '{entity.Comp.SavedGridName ?? (entity.Comp.CurrentTarget.HasValue ? Name(entity.Comp.CurrentTarget.Value) : "Unknown")}'. (Reason: {motive ?? "Unknown"})");
            _popup.PopupEntity(notification, entity, Shared.Popups.PopupType.MediumCaution);
        }

        if (updateUI)
            UpdateUserInterface(entity.Owner, entity.Comp);
    }

    private void CancelRoutine(Entity<BSPParkingTargetComponent> entity, string motive, bool updateUI = true, bool popupNotification = true)
    {
        if (TryComp<BSPAnchorKeyComponent>(entity.Comp.Source, out var key))
            CancelRoutine((entity.Comp.Source, key), motive, updateUI, popupNotification);
    }

    private bool CommitPark(Entity<BSPAnchorKeyComponent> key, Entity<BSPParkingTargetComponent> target)
    {
        if (TerminatingOrDeleted(key) || TerminatingOrDeleted(target)) return false;
        if (key.Comp.State != BSPState.Parking) return false;

        var gridUid = target.Owner;
        var gridXform = Comp<TransformComponent>(gridUid);

        if (!TryComp<MapGridComponent>(gridUid, out var mapGrid) || mapGrid == null) return false;

        var now = DateTime.Now;

        var filename = $"{now:yyyy-M-dd_HH.mm.ss.ffff}_{SafeGridNameRgx().Replace(Name(gridUid), "")}_{Guid.NewGuid()}.yml";
        var path = GetSaveResPath(filename);

        var positionRotation = _transform.GetWorldPositionRotation(gridXform);

        var owningFactionUid = _station.GetOwningStation(gridUid);
        var owningFactionData = owningFactionUid.GetValueOrDefault().IsValid() ? Comp<StationDataComponent>(owningFactionUid!.Value) : null;
        var owningFaction = owningFactionData?.UID;
        var personalOwner = _station.GetOwningStationPersonal(gridUid);
        var gridName = Name(gridUid);

        _dock.UndockDocks(gridUid);

        // Move audio away from the grid
        var audio = target.Comp.StartupStream;
        if (audio.HasValue && !TerminatingOrDeleted(audio.Value) && TryComp(audio.Value, out AudioComponent? startupAudio))
        {
            var newAudioPos = positionRotation.WorldPosition + positionRotation.WorldRotation.Opposite().ToVec() * mapGrid.LocalAABB.MaxDimension;
            var mapUid = _transform.GetMap(gridUid.ToCoordinates());

            var clippedAudio = _audio.PlayStatic(_startupSound, Filter.Broadcast(),
                new EntityCoordinates(mapUid.GetValueOrDefault(), newAudioPos), true, startupAudio.Params);

            _audio.SetPlaybackPosition(clippedAudio, (float)((startupAudio.PauseTime ?? _timing.CurTime) - startupAudio.AudioStart).TotalSeconds);
            if (clippedAudio != null)
                clippedAudio.Value.Component.Flags |= AudioFlags.NoOcclusion;
            _audio.Stop(target.Comp.StartupStream);
        }

        if (_persistence.SaveGrid(gridUid, path, out var errorMessage, dumpSpecialEntities: true, deleteGrid: true))
        {

            key.Comp.CurrentTarget = null;
            key.Comp.SavedClearOwnership = false;
            key.Comp.SavedTimestamp = now;
            key.Comp.SavedFilename = filename;
            key.Comp.SavedOwnerFaction = owningFaction;
            key.Comp.SavedOwnerPersonal = personalOwner;
            key.Comp.SavedParkedBounds = mapGrid.LocalAABB;
            key.Comp.SavedParkedPosition = positionRotation.WorldPosition;
            key.Comp.SavedParkedRotation = positionRotation.WorldRotation;
            key.Comp.SavedTileCount = _mapSystem.GetAllTiles(gridUid, mapGrid).Count();
            key.Comp.SavedGridName = gridName;
            key.Comp.State = BSPState.Parked;

            UpdateAppearance(key, key.Comp);

            var notification = $"{key.Comp.SavedGridName} has been parked in bluespace.";
            _popup.PopupEntity(notification, key);
            _chat.TrySendInGameICMessage(key, notification, Shared.Chat.InGameICChatType.Whisper, Shared.Chat.ChatTransmitRange.Normal, ignoreActionBlocker: true);
            _adminLog.Add(LogType.BluespaceParking, LogImpact.Low, $"The grid '{gridName}' ({gridUid}) has been successfuly parked.");

            if (TryComp(key, out MetaDataComponent? meta))
                _meta.SetEntityName(key, $"{meta.EntityPrototype!.Name} [{key.Comp.SavedGridName}]", meta);
        }
        else
        {
            _adminLog.Add(LogType.BluespaceParking, LogImpact.High, $"The grid '{gridName}' ({gridUid}) was not successfuly parked! (Reason: {errorMessage ?? "Unknown"})");
            return false;
        }

        UpdateUserInterface(key.Owner, key.Comp);
        return true;
    }

    private string EnsureSavesDirectory()
    {
        var saveDir = Path.Combine(_cfg.GetCVar(CCVars.BluespaceParkingDirectory)).Replace(Path.DirectorySeparatorChar, '/');
        _resMan.UserData.CreateDir(new ResPath(saveDir).ToRootedPath());
        return saveDir;
    }
    private string EnsureBackupsDirectory()
    {
        var backupsDir = Path.Combine(_cfg.GetCVar(CCVars.BluespaceParkingDirectory), "Backups").Replace(Path.DirectorySeparatorChar, '/');
        _resMan.UserData.CreateDir(new ResPath(backupsDir).ToRootedPath());
        return backupsDir;
    }

    private ResPath GetSaveResPath(string filename)
    {
        return new ResPath(Path.Combine(EnsureSavesDirectory(), filename).Replace(Path.DirectorySeparatorChar, '/'));
    }

    private ResPath GetBackupResPath(string filename)
    {
        return new ResPath(Path.Combine(EnsureBackupsDirectory(), filename).Replace(Path.DirectorySeparatorChar, '/'));
    }

    private void GetRecallSource(Entity<BSPAnchorKeyComponent> key, Vector2 originalOrigin, Angle originalRotation, out MapId mapId, out Vector2 origin, out Box2 bounds, out Angle worldAngle)
    {
        mapId = _transform.GetMapId(key.Owner);
        bounds = key.Comp.SavedParkedBounds.GetValueOrDefault();
        worldAngle = originalRotation;
        var offsetPoint = bounds.ClosestPoint(bounds.Center + worldAngle.Opposite().ToVec() * bounds.MaxDimension);
        var offset = (worldAngle + MathF.PI / 2).ToVec() * (-1f + (offsetPoint - bounds.Center).Length());
        origin = originalOrigin + offset;
    }

    private bool CommitUnpark(Entity<BSPAnchorKeyComponent> key, BSPAnchorKeyUnparkingComponent unparkingState)
    {
        if (TerminatingOrDeleted(key)) return false;
        if (key.Comp.State != BSPState.Unparking) return false;

        var filename = key.Comp.SavedFilename;
        var resPath = GetSaveResPath(filename!);
        if (!_resMan.UserData.Exists(resPath.ToRootedPath())) return false;

        GetRecallSource(key, unparkingState.Origin, unparkingState.Rotation.Opposite(), out var mapId, out var origin, out var bounds, out var worldAngle);
        if (!TryGetUnparkPlacementLocation(mapId, origin, bounds, worldAngle, out var coords, out var rot))
            return false;
        if (_persistence.LoadGrid(resPath, mapId, coords.Position, rot, out var errorMessage, out var mapGrid, dumpSpecialEntities: true) && mapGrid.HasValue)
        {
            // Move to backup directory!
            _resMan.UserData.Rename(resPath.ToRootedPath(), GetBackupResPath(filename!).ToRootedPath());

            // Clear ownership if set
            if (key.Comp.SavedClearOwnership)
            {
                var station = _station.GetOwningStation(mapGrid);
                if (station != null)
                {
                    _station.RemoveGridFromStation(station.Value, mapGrid.Value);
                }
                else
                {
                    var ownername = _station.GetOwningStationPersonal(mapGrid.Value);
                    if (ownername != null)
                    {
                        _station.RemoveGridFromPerson(mapGrid.Value);
                    }
                }
            }

            var gridPosition = _transform.GetWorldPosition(mapGrid.Value);
            var vector = gridPosition - _transform.GetWorldPosition(key);

            // Try Docking
            var mapCoordinates = new MapCoordinates(unparkingState.Origin, mapId);
            if (_mapManager.TryFindGridAt(mapCoordinates, out var otherGridUid, out var otherGrid))
            {
                Timer.Spawn(20, () =>
                {
                    var docked = false;
                    var otherDocks = _dock.GetDocks(otherGridUid);
                    var thisDocks = _dock.GetDocks(mapGrid.Value);

                    foreach (var thisDock in thisDocks)
                    {
                        foreach (var otherDock in otherDocks)
                        {

                            var (worldPosA, worldRotA) = _transform.GetWorldPositionRotation(thisDock);
                            var (worldPosB, worldRotB) = _transform.GetWorldPositionRotation(otherDock);
                            if (_dock.CanDock(thisDock, otherDock) || !docked && _dock.InAlignment(new MapCoordinates(worldPosA, mapId), worldRotA, new MapCoordinates(worldPosB, mapId), worldRotB, Angle.FromDegrees(45).Theta))
                            {
                                docked = true;
                                _dock.Dock(thisDock, otherDock);
                            }
                        }
                    }
                });
            }

            // Play sound
            var audio = _audio.PlayPvs(_unparkingSound, mapGrid.Value);
            _audio.SetGridAudio(audio);

            var notification = $"{key.Comp.SavedGridName} has been recalled. Position: ({(int)coords.Position.X}, {(int)coords.Position.Y}) - {vector.Length():F0}m to the {vector.GetDir()}";
            _popup.PopupEntity(notification, key);
            _chat.TrySendInGameICMessage(key, notification, Shared.Chat.InGameICChatType.Whisper, Shared.Chat.ChatTransmitRange.Normal, ignoreActionBlocker: true);

            if (TryComp(key, out MetaDataComponent? meta))
                _meta.SetEntityName(key, meta.EntityPrototype!.Name, meta);

            _adminLog.Add(LogType.BluespaceParking, LogImpact.Medium, $"The grid '{key.Comp.SavedGridName}' ({mapGrid}) was successfuly unparked.");

            key.Comp.SavedTimestamp = null;
            key.Comp.SavedFilename = null;
            key.Comp.SavedOwnerFaction = null;
            key.Comp.SavedOwnerPersonal = null;
            key.Comp.SavedParkedBounds = null;
            key.Comp.SavedParkedPosition = null;
            key.Comp.SavedParkedRotation = null;
            key.Comp.SavedTileCount = null;
            key.Comp.SavedGridName = null;
            key.Comp.State = BSPState.Idle;

            UpdateAppearance(key, key.Comp);
        }
        else
        {
            _adminLog.Add(LogType.BluespaceParking, LogImpact.Medium, $"The grid '{key.Comp.SavedGridName}' was not successfuly unparked. (Reason: {errorMessage ?? "Unknown"})");
            return false;
        }

        UpdateUserInterface(key.Owner, key.Comp);
        return true;
    }

    private bool TryGetUnparkPlacementLocation(MapId mapId, Vector2 origin, Box2 bounds, Angle worldAngle, out MapCoordinates coords, out Angle angle, float spawnDistance = 20f, int maxIterations = 20)
    {
        var finalCoords = new MapCoordinates(origin, mapId);
        angle = worldAngle;

        for (var i = 0; i < maxIterations; i++)
        {
            var box2 = Box2.CenteredAround(finalCoords.Position, bounds.Size);
            var box2Rot = new Box2Rotated(box2, angle, finalCoords.Position).Enlarged(-0.5f);

            // This doesn't stop it from spawning on top of random things in space
            if (_mapManager.FindGridsIntersecting(finalCoords.MapId, box2Rot).Any())
            {
                // Bump it further and further just in case.
                var fraction = (float)(i + 1) / maxIterations;
                var randomPos = origin +
                    (worldAngle + Math.PI / 2).ToVec() * (DockingSystem.DockRange + (spawnDistance * fraction));
                finalCoords = new MapCoordinates(randomPos, mapId);
                continue;
            }
            else if (i == 0)
            {
                var pos = origin + (worldAngle + Math.PI / 2).ToVec() * DockingSystem.DockRange;
                finalCoords = new MapCoordinates(pos, mapId);
            }
            coords = finalCoords;
            return true;
        }

        angle = Angle.Zero;
        coords = MapCoordinates.Nullspace;
        return false;
    }
}
