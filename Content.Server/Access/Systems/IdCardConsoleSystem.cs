using Content.Server.Chat.Systems;
using Content.Server.Containers;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Chat;
using Content.Shared.Construction;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.CrewAssignments.Components;
using Content.Shared.CrewRecords.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Roles;
using Content.Shared.Station.Components;
using Content.Shared.StationRecords;
using Content.Shared.Throwing;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static Content.Shared.Access.Components.IdCardConsoleComponent;

namespace Content.Server.Access.Systems;

[UsedImplicitly]
public sealed class IdCardConsoleSystem : SharedIdCardConsoleSystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly StationRecordsSystem _record = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly AccessSystem _access = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IdCardConsoleComponent, WriteToTargetIdMessage>(OnWriteToTargetIdMessage);
        SubscribeLocalEvent<IdCardConsoleComponent, SearchRecord>(OnSearchRecord);
        SubscribeLocalEvent<IdCardConsoleComponent, ChangeAssignment>(OnChangeAssignment);
        // one day, maybe bound user interfaces can be shared too.
        SubscribeLocalEvent<IdCardConsoleComponent, ComponentStartup>(UpdateUserInterface);
        SubscribeLocalEvent<IdCardConsoleComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<IdCardConsoleComponent, EntRemovedFromContainerMessage>(UpdateUserInterface);
        SubscribeLocalEvent<IdCardConsoleComponent, DamageChangedEvent>(OnDamageChanged);

        // Intercept the event before anyone can do anything with it!
        SubscribeLocalEvent<IdCardConsoleComponent, MachineDeconstructedEvent>(OnMachineDeconstructed,
            before: [typeof(EmptyOnMachineDeconstructSystem), typeof(ItemSlotsSystem)]);
    }

    private CrewRecord? TryEnsureRecord(EntityUid uid, string recordName)
    {
        var station = _station.GetOwningStation(uid);
        if (station == null) return null;
        if (!TryComp(station, out CrewRecordsComponent? stationData))
        {
            stationData = null;
            return null;
        }
        if (stationData == null) return null;
        stationData.TryEnsureRecord(recordName, out var record, EntityManager);
        return record;
    }

    private void OnEntInserted(EntityUid uid, IdCardConsoleComponent component, EntInsertedIntoContainerMessage args)
    {
        if(component.TargetIdSlot.Item == args.Entity)
        {
            if (component.TargetIdSlot.Item is { Valid: true } targetId) // targetID lsot occupied
            {
                var idComponent = Comp<IdCardComponent>(targetId);
                if(idComponent!= null && idComponent.FullName != null)
                    component.SelectedRecord = TryEnsureRecord(uid, idComponent.FullName);
            }
        }

        UpdateUserInterface(uid, component, args);
    }
    private void OnSearchRecord(EntityUid uid, IdCardConsoleComponent component, SearchRecord args)
    {
        if (args.Actor is not { Valid: true } player)
            return;
        if (component.SelectedRecord == null || component.SelectedRecord.Name != args.FullName)
        {
            component.SelectedRecord = TryEnsureRecord(uid, args.FullName);
        }

        UpdateUserInterface(uid, component, args);
    }
    private void OnChangeAssignment(EntityUid uid, IdCardConsoleComponent component, ChangeAssignment args)
    {
        if (args.Actor is not { Valid: true } player)
            return;
        if (component.SelectedRecord == null || component.PrivRecord == null)
        {
            return;
        }
        var station = _station.GetOwningStation(uid);
        if (station == null) return;
        if (!TryComp(station, out CrewAssignmentsComponent? cW))
        {
            return;
        }

        var possibleAssignments = cW.CrewAssignments;
        CrewAssignment? currentTargetAssignment;
        possibleAssignments.TryGetValue(component.SelectedRecord.AssignmentID, out currentTargetAssignment);
        CrewAssignment? currentPrivAssignment;
        possibleAssignments.TryGetValue(component.PrivRecord.AssignmentID, out currentPrivAssignment);
        CrewAssignment? newTargetAssignment;
        possibleAssignments.TryGetValue(args.ID, out newTargetAssignment);
        if (newTargetAssignment == null) return;
        var owner = false;
        if (TryComp(station, out StationDataComponent? sD))
        {
            if (component.PrivRecord.Name != null && sD.Owners.Contains(component.PrivRecord.Name)) owner = true;
        }
        else
        {
            return;
        }
        if (!owner && (currentTargetAssignment != null && (currentPrivAssignment == null || currentTargetAssignment.Clevel >= currentPrivAssignment.Clevel)))
        {
            return;
        }
        if (!owner && (currentPrivAssignment == null || newTargetAssignment.Clevel >= currentPrivAssignment.Clevel))
        {
            return;
        }
        component.SelectedRecord.AssignmentID = args.ID;
        if (TryComp(station, out CrewRecordsComponent? stationData))
        {
            Dirty((EntityUid)station, stationData);
        }
        var query = EntityQueryEnumerator<IdCardComponent>();
        while (query.MoveNext(out var carde, out var card))
        {
            if (card.FullName == component.SelectedRecord.Name && card.stationID == sD.UID)
            {
                _idCard.TryChangeJobTitle(carde, newTargetAssignment.Name, card, player);
            }
        }
        UpdateUserInterface(uid, component, args);
    }
    private void OnWriteToTargetIdMessage(EntityUid uid, IdCardConsoleComponent component, WriteToTargetIdMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        TryWriteToTargetId(uid, args.FullName, args.JobTitle, args.AccessList, args.JobPrototype, player, component);

        UpdateUserInterface(uid, component, args);
    }

    private void UpdateUserInterface(EntityUid uid, IdCardConsoleComponent component, EntityEventArgs args)
    {
        if (!component.Initialized)
            return;
        IdCardConsoleBoundUserInterfaceState newState;
        var station = _station.GetOwningStation(uid);
        if (station == null) return;
        if (!TryComp(station, out CrewAssignmentsComponent? stationData))
        {
            stationData = null;
            return;
        }
        var possibleAssignments = stationData.CrewAssignments;

        var privilegedIdName = string.Empty;
        var privFullName = string.Empty;
        var targetIdName = string.Empty;
        CrewAssignment? assignment = null;
        CrewAssignment? privassignment = null;
        var owner = false;

        if (component.TargetIdSlot.Item is { Valid: true } targetId) // targetID lsot occupied
        {
            var targetIdComponent = Comp<IdCardComponent>(targetId);
            var targetAccessComponent = Comp<AccessComponent>(targetId);
            targetIdName = Comp<MetaDataComponent>(targetId).EntityName;
        }
        if (component.PrivilegedIdSlot.Item is { Valid: true } privId) // targetID lsot occupied
        {
            privilegedIdName = Comp<MetaDataComponent>(privId).EntityName;
            var privIdComponent = Comp<IdCardComponent>(privId);
            if (component.PrivRecord == null || component.PrivRecord.Name != privIdComponent.FullName)
            {
                if (privIdComponent != null && privIdComponent.FullName != null)
                {
                    component.PrivRecord = TryEnsureRecord(uid, privIdComponent.FullName);
                }
                
            }
            if (component.PrivRecord != null)
            {
                possibleAssignments.TryGetValue(component.PrivRecord.AssignmentID, out privassignment);
            }
            if (TryComp(station, out StationDataComponent? sD))
            {
                if (privIdComponent != null && privIdComponent.FullName != null && sD.Owners.Contains(privIdComponent.FullName)) owner = true;
            }
        }
        if (component.SelectedRecord == null)
        {


            newState = new IdCardConsoleBoundUserInterfaceState(
                component.PrivilegedIdSlot.HasItem,
                owner || PrivilegedIdIsAuthorized(uid, component, out _),
                component.TargetIdSlot.HasItem,
                "",
                targetIdName,
                privilegedIdName,
                privFullName,
                assignment,
                privassignment,
                possibleAssignments,
                owner);
                
                
        }
        else
        {
            
            possibleAssignments.TryGetValue(component.SelectedRecord.AssignmentID, out assignment);

            newState = new IdCardConsoleBoundUserInterfaceState(
                component.PrivilegedIdSlot.HasItem,
                owner || PrivilegedIdIsAuthorized(uid, component, out _),
                component.TargetIdSlot.HasItem,
                component.SelectedRecord.Name,
                targetIdName,
                privilegedIdName,
                privFullName,
                assignment,
                privassignment,
                possibleAssignments,
                owner);

        }

        _userInterface.SetUiState(uid, IdCardConsoleUiKey.Key, newState);
    }

    /// <summary>
    /// Called whenever an access button is pressed, adding or removing that access from the target ID card.
    /// Writes data passed from the UI into the ID stored in <see cref="IdCardConsoleComponent.TargetIdSlot"/>, if present.
    /// </summary>
    private void TryWriteToTargetId(EntityUid uid,
        string newFullName,
        string newJobTitle,
        List<ProtoId<AccessLevelPrototype>> newAccessList,
        ProtoId<JobPrototype> newJobProto,
        EntityUid player,
        IdCardConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.TargetIdSlot.Item is not { Valid: true } targetId || !PrivilegedIdIsAuthorized(uid, component, out var privilegedId))
            return;

        _idCard.TryChangeFullName(targetId, newFullName, player: player);
        _idCard.TryChangeJobTitle(targetId, newJobTitle, player: player);

        if (_prototype.Resolve(newJobProto, out var job)
            && _prototype.Resolve(job.Icon, out var jobIcon))
        {
            _idCard.TryChangeJobIcon(targetId, jobIcon, player: player);
            _idCard.TryChangeJobDepartment(targetId, job);
        }

        UpdateStationRecord(uid, targetId, newFullName, newJobTitle, job);
        if ((!TryComp<StationRecordKeyStorageComponent>(targetId, out var keyStorage)
            || keyStorage.Key is not { } key
            || !_record.TryGetRecord<GeneralStationRecord>(key, out _))
            && newJobProto != string.Empty)
        {
            Comp<IdCardComponent>(targetId).JobPrototype = newJobProto;
        }

        

        var oldTags = _access.TryGetTags(targetId)?.ToList() ?? new List<ProtoId<AccessLevelPrototype>>();

        if (oldTags.SequenceEqual(newAccessList))
            return;

        // I hate that C# doesn't have an option for this and don't desire to write this out the hard way.
        // var difference = newAccessList.Difference(oldTags);
        var difference = newAccessList.Union(oldTags).Except(newAccessList.Intersect(oldTags)).ToHashSet();
        var privilegedPerms = _accessReader.FindAccessTags(privilegedId.Value);
        if (!difference.IsSubsetOf(privilegedPerms))
        {
            _sawmill.Warning($"User {ToPrettyString(uid)} tried to modify permissions they could not give/take!");
            return;
        }

        var addedTags = newAccessList.Except(oldTags).Select(tag => "+" + tag).ToList();
        var removedTags = oldTags.Except(newAccessList).Select(tag => "-" + tag).ToList();
        _access.TrySetTags(targetId, newAccessList);

        /*TODO: ECS SharedIdCardConsoleComponent and then log on card ejection, together with the save.
        This current implementation is pretty shit as it logs 27 entries (27 lines) if someone decides to give themselves AA*/
        _adminLogger.Add(LogType.Action,
            $"{player} has modified {targetId} with the following accesses: [{string.Join(", ", addedTags.Union(removedTags))}] [{string.Join(", ", newAccessList)}]");
    }

    /// <summary>
    /// Returns true if there is an ID in <see cref="IdCardConsoleComponent.PrivilegedIdSlot"/> and said ID satisfies the requirements of <see cref="AccessReaderComponent"/>.
    /// </summary>
    private bool PrivilegedIdIsAuthorized(EntityUid uid, IdCardConsoleComponent component, [NotNullWhen(true)] out EntityUid? id)
    {
        id = null;
        if (component.PrivilegedIdSlot.Item == null)
            return false;

        id = component.PrivilegedIdSlot.Item;
        if (!TryComp<AccessReaderComponent>(uid, out var reader))
            return true;

        return _accessReader.IsAllowed(id.Value, uid, reader);
    }

    private void UpdateStationRecord(EntityUid uid, EntityUid targetId, string newFullName, ProtoId<AccessLevelPrototype> newJobTitle, JobPrototype? newJobProto)
    {
        if (!TryComp<StationRecordKeyStorageComponent>(targetId, out var keyStorage)
            || keyStorage.Key is not { } key
            || !_record.TryGetRecord<GeneralStationRecord>(key, out var record))
        {
            return;
        }

        record.Name = newFullName;
        record.JobTitle = newJobTitle;

        if (newJobProto != null)
        {
            record.JobPrototype = newJobProto.ID;
            record.JobIcon = newJobProto.Icon;
        }

        _record.Synchronize(key);
    }

    private void OnMachineDeconstructed(Entity<IdCardConsoleComponent> entity, ref MachineDeconstructedEvent args)
    {
        TryDropAndThrowIds(entity.AsNullable());
    }

    private void OnDamageChanged(Entity<IdCardConsoleComponent> entity, ref DamageChangedEvent args)
    {
        if (TryDropAndThrowIds(entity.AsNullable()))
            _chat.TrySendInGameICMessage(entity, Loc.GetString("id-card-console-damaged"), InGameICChatType.Speak, true);
    }

    #region PublicAPI

    /// <summary>
    ///     Tries to drop any IDs stored in the console, and then tries to throw them away.
    ///     Returns true if anything was ejected and false otherwise.
    /// </summary>
    public bool TryDropAndThrowIds(Entity<IdCardConsoleComponent?, ItemSlotsComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2))
            return false;

        var didEject = false;

        foreach (var slot in ent.Comp2.Slots.Values)
        {
            if (slot.Item == null || slot.ContainerSlot == null)
                continue;

            var item = slot.Item.Value;
            if (_container.Remove(item, slot.ContainerSlot))
            {
                _throwing.TryThrow(item, _random.NextVector2(), baseThrowSpeed: 5f);
                didEject = true;
            }
        }

        return didEject;
    }

    #endregion
}
