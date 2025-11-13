using Content.Server.Popups;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.CrewAccesses.Components;
using Content.Shared.CrewAssignments.Components;
using Content.Shared.CrewRecords.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using System.Linq;
using static Content.Shared.Access.Components.AccessOverriderComponent;

namespace Content.Server.Access.Systems;

[UsedImplicitly]
public sealed class AccessOverriderSystem : SharedAccessOverriderSystem
{
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AccessOverriderComponent, ComponentStartup>(UpdateUserInterface);
        SubscribeLocalEvent<AccessOverriderComponent, EntInsertedIntoContainerMessage>(UpdateUserInterface);
        SubscribeLocalEvent<AccessOverriderComponent, EntRemovedFromContainerMessage>(UpdateUserInterface);
        SubscribeLocalEvent<AccessOverriderComponent, AfterInteractEvent>(AfterInteractOn);
        SubscribeLocalEvent<AccessOverriderComponent, AccessOverriderDoAfterEvent>(OnDoAfter);

        Subs.BuiEvents<AccessOverriderComponent>(AccessOverriderUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<BoundUIClosedEvent>(OnClose);
            subs.Event<WriteToTargetAccessReaderIdMessage>(OnWriteToTargetAccessReaderIdMessage);
            subs.Event<AccessReaderAccessToggledMessage>(OnAccessToggleMessage);
            subs.Event<AccessReaderPersonalAccessToggledMessage>(OnPersonalAccessToggleMessage);
            subs.Event<AccessReaderPersonalAddMessage>(OnPersonalAddMessage);
            subs.Event<AccessReaderChangeModeMessage>(OnChangeMode);
        });
    }

    private void AfterInteractOn(EntityUid uid, AccessOverriderComponent component, AfterInteractEvent args)
    {
        if (args.Target == null || !TryComp(args.Target, out AccessReaderComponent? accessReader))
            return;

        if (!_interactionSystem.InRangeUnobstructed(args.User, (EntityUid) args.Target))
            return;

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.DoAfter, new AccessOverriderDoAfterEvent(), uid, target: args.Target, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfterSystem.TryStartDoAfter(doAfterEventArgs);
    }

    private void OnDoAfter(EntityUid uid, AccessOverriderComponent component, AccessOverriderDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Args.Target != null)
        {
            component.TargetAccessReaderId = args.Args.Target.Value;
            _userInterface.OpenUi(uid, AccessOverriderUiKey.Key, args.User);
            UpdateUserInterface(uid, component, args);
        }

        args.Handled = true;
    }

    private void OnClose(EntityUid uid, AccessOverriderComponent component, BoundUIClosedEvent args)
    {
        if (args.UiKey.Equals(AccessOverriderUiKey.Key))
        {
            component.TargetAccessReaderId = new();
        }
    }

    private void OnAccessToggleMessage(EntityUid uid, AccessOverriderComponent component, AccessReaderAccessToggledMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        TryToggleAccess(uid, args.Access, player, component);

        UpdateUserInterface(uid, component, args);
    }

    private void OnChangeMode(EntityUid uid, AccessOverriderComponent component, AccessReaderChangeModeMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        TryChangeMode(uid, player, component);

        UpdateUserInterface(uid, component, args);
    }
    private void OnPersonalAddMessage(EntityUid uid, AccessOverriderComponent component, AccessReaderPersonalAddMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        TryAddPersonalAccess(uid, args.Access, player, component);

        UpdateUserInterface(uid, component, args);
    }
    private void OnPersonalAccessToggleMessage(EntityUid uid, AccessOverriderComponent component, AccessReaderPersonalAccessToggledMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        TryTogglePersonalAccess(uid, args.Access, player, component);

        UpdateUserInterface(uid, component, args);
    }
    private void OnWriteToTargetAccessReaderIdMessage(EntityUid uid, AccessOverriderComponent component, WriteToTargetAccessReaderIdMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        TryWriteToTargetAccessReaderId(uid, args.AccessList, player, component);

        UpdateUserInterface(uid, component, args);
    }

    private void UpdateUserInterface(EntityUid uid, AccessOverriderComponent component, EntityEventArgs args)
    {
        if (!component.Initialized)
            return;

        var privilegedIdName = string.Empty;
        var targetLabel = Loc.GetString("access-overrider-window-no-target");
        var targetLabelColor = Color.Red;

        List<string>? allAccesses = null;
        List<string>? currentAccesses = null;
        List<string>? missingAccesses = null;
        List<string>? possibleAccesses = null;
        List<string>? personalAccesses = null;
        bool personalAccessMode = false;
        var station = _station.GetOwningStation(uid);

        string stationName = "*None*";

        Entity<AccessReaderComponent>? accessReaderEnt = null;
        if (component.TargetAccessReaderId is { Valid: true } accessReader)
        {

            targetLabel = Loc.GetString("access-overrider-window-target-label") + " " + Comp<MetaDataComponent>(component.TargetAccessReaderId).EntityName;
            targetLabelColor = Color.White;

            if (!_accessReader.GetMainAccessReader(accessReader, out accessReaderEnt))
                return;
            currentAccesses = accessReaderEnt.Value.Comp.AccessNames;
            personalAccesses = accessReaderEnt.Value.Comp.PersonalAccessNames;
            personalAccessMode = accessReaderEnt.Value.Comp.PersonalAccessMode;
            if (station != null)
            {
                if (TryComp<StationDataComponent>(station, out var sD) && sD != null && sD.StationName != null)
                {
                    stationName = sD.StationName;
                }
                if (TryComp<CrewAccessesComponent>(station, out var crewAccesses))
                {
                    allAccesses = new();
                    foreach (var access in crewAccesses.CrewAccesses)
                    {
                        allAccesses.Add(access.Key);
                    }
                }
                if (component.PrivilegedIdSlot.Item is { Valid: true } idCard)
                {
                    privilegedIdName = Comp<MetaDataComponent>(idCard).EntityName;
                    if (TryComp<IdCardComponent>(idCard, out var idCardEnt))
                    {
                        var realName = "";
                        if(idCardEnt.FullName != null) realName = idCardEnt.FullName;
                        if (TryComp<CrewRecordsComponent>(station, out var crewRecords))
                        {
                            if(crewRecords.TryGetRecord(realName, out var crewRecord) && crewRecord != null)
                            {
                                if (TryComp<CrewAssignmentsComponent>(station, out var crewAssignments))
                                {
                                    if(crewAssignments.TryGetAssignment(crewRecord.AssignmentID, out var crewAssignment) && crewAssignment != null)
                                    {
                                        possibleAccesses = crewAssignment.AccessIDs;
                                    }
                                }
                                    
                            }
                        }

                    }
                }

            }
            if (allAccesses != null)
            {
                if (possibleAccesses == null)
                {
                    missingAccesses = currentAccesses;
                }
                else
                {
                    missingAccesses = new List<string>();
                    foreach (var access in allAccesses)
                    {
                        if (!possibleAccesses.Contains(access) && currentAccesses.Contains(access))
                        {
                            missingAccesses.Add(access);
                        }
                    }
                }
            }
        }

        
        AccessOverriderBoundUserInterfaceState newState;
        bool allowed = true;
        if (accessReaderEnt != null)
        {
            allowed = PrivilegedIdIsAuthorized(uid, accessReaderEnt, component);
        }
        newState = new AccessOverriderBoundUserInterfaceState(
            component.PrivilegedIdSlot.HasItem,
            allowed,
            privilegedIdName,
            targetLabel,
            targetLabelColor,
            currentAccesses,
            possibleAccesses,
            stationName,
            personalAccessMode,
            personalAccesses,
            missingAccesses,
            allAccesses);

        _userInterface.SetUiState(uid, AccessOverriderUiKey.Key, newState);
    }

    private List<ProtoId<AccessLevelPrototype>> ConvertAccessHashSetsToList(List<HashSet<ProtoId<AccessLevelPrototype>>> accessHashsets)
    {
        var accessList = new List<ProtoId<AccessLevelPrototype>>();

        if (accessHashsets.Count <= 0)
            return accessList;

        foreach (var hashSet in accessHashsets)
        {
            accessList.AddRange(hashSet);
        }

        return accessList;
    }

    /// <summary>
    /// Called whenever an access button is pressed, adding or removing that access requirement from the target access reader.
    /// </summary>
    private void TryWriteToTargetAccessReaderId(EntityUid uid,
        List<ProtoId<AccessLevelPrototype>> newAccessList,
        EntityUid player,
        AccessOverriderComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.TargetAccessReaderId is not { Valid: true })
            return;

//        if (!PrivilegedIdIsAuthorized(uid, component))
//            return;

        if (!_interactionSystem.InRangeUnobstructed(player, component.TargetAccessReaderId))
        {
            _popupSystem.PopupEntity(Loc.GetString("access-overrider-out-of-range"), player, player);

            return;
        }

        if (newAccessList.Count > 0 && !newAccessList.TrueForAll(x => component.AccessLevels.Contains(x)))
        {
            _sawmill.Warning($"User {ToPrettyString(uid)} tried to write unknown access tag.");
            return;
        }

        if (!_accessReader.GetMainAccessReader(component.TargetAccessReaderId, out var accessReaderEnt))
            return;

        var oldTags = ConvertAccessHashSetsToList(accessReaderEnt.Value.Comp.AccessLists);
        var privilegedId = component.PrivilegedIdSlot.Item;

        if (oldTags.SequenceEqual(newAccessList))
            return;

        var difference = newAccessList.Union(oldTags).Except(newAccessList.Intersect(oldTags)).ToHashSet();
        var privilegedPerms = _accessReader.FindAccessTags(privilegedId!.Value).ToHashSet();

        if (!difference.IsSubsetOf(privilegedPerms))
        {
            _sawmill.Warning($"User {ToPrettyString(uid)} tried to modify permissions they could not give/take!");

            return;
        }

        if (!oldTags.ToHashSet().IsSubsetOf(privilegedPerms))
        {
            _sawmill.Warning($"User {ToPrettyString(uid)} tried to modify permissions when they do not have sufficient access!");
            _popupSystem.PopupEntity(Loc.GetString("access-overrider-cannot-modify-access"), player, player);
            _audioSystem.PlayPvs(component.DenialSound, uid);

            return;
        }

        var addedTags = newAccessList.Except(oldTags).Select(tag => "+" + tag).ToList();
        var removedTags = oldTags.Except(newAccessList).Select(tag => "-" + tag).ToList();

        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(player):player} has modified {ToPrettyString(accessReaderEnt.Value):entity} with the following allowed access level holders: [{string.Join(", ", addedTags.Union(removedTags))}] [{string.Join(", ", newAccessList)}]");

        _accessReader.TrySetAccesses(accessReaderEnt.Value, newAccessList);

        var ev = new OnAccessOverriderAccessUpdatedEvent(player);
        RaiseLocalEvent(component.TargetAccessReaderId, ref ev);
    }

    private void TryToggleAccess(EntityUid uid,
       string newAccess,
       EntityUid player,
       AccessOverriderComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.TargetAccessReaderId is not { Valid: true })
            return;

        

        if (!_interactionSystem.InRangeUnobstructed(player, component.TargetAccessReaderId))
        {
            _popupSystem.PopupEntity(Loc.GetString("access-overrider-out-of-range"), player, player);

            return;
        }

        if (!_accessReader.GetMainAccessReader(component.TargetAccessReaderId, out var accessReaderEnt))
            return;


        if (!PrivilegedIdIsAuthorized(uid, accessReaderEnt.Value, component))
            return;

        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(player):player} has modified {ToPrettyString(accessReaderEnt.Value):entity} with the following allowed access level holders: {newAccess}");

        _accessReader.TryToggleAccess(accessReaderEnt.Value,newAccess);

        var ev = new OnAccessOverriderAccessUpdatedEvent(player);
        RaiseLocalEvent(component.TargetAccessReaderId, ref ev);
    }

    private void TryTogglePersonalAccess(EntityUid uid,
       string newAccess,
       EntityUid player,
       AccessOverriderComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.TargetAccessReaderId is not { Valid: true })
            return;

        

        if (!_interactionSystem.InRangeUnobstructed(player, component.TargetAccessReaderId))
        {
            _popupSystem.PopupEntity(Loc.GetString("access-overrider-out-of-range"), player, player);

            return;
        }

        if (!_accessReader.GetMainAccessReader(component.TargetAccessReaderId, out var accessReaderEnt))
            return;

        if (!PrivilegedIdIsAuthorized(uid, accessReaderEnt.Value, component))
            return;


        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(player):player} has modified {ToPrettyString(accessReaderEnt.Value):entity} with the following allowed access level holders: {newAccess}");

        _accessReader.TryTogglePersonalAccess(accessReaderEnt.Value, newAccess);

        var ev = new OnAccessOverriderAccessUpdatedEvent(player);
        RaiseLocalEvent(component.TargetAccessReaderId, ref ev);
    }

    private void TryChangeMode(EntityUid uid,
     EntityUid player,
     AccessOverriderComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.TargetAccessReaderId is not { Valid: true })
            return;



        if (!_interactionSystem.InRangeUnobstructed(player, component.TargetAccessReaderId))
        {
            _popupSystem.PopupEntity(Loc.GetString("access-overrider-out-of-range"), player, player);

            return;
        }

        if (!_accessReader.GetMainAccessReader(component.TargetAccessReaderId, out var accessReaderEnt))
            return;

        if (!PrivilegedIdIsAuthorized(uid, accessReaderEnt.Value, component))
            return;

        _accessReader.TryChangeMode(accessReaderEnt.Value);

        var ev = new OnAccessOverriderAccessUpdatedEvent(player);
        RaiseLocalEvent(component.TargetAccessReaderId, ref ev);
    }

    private void TryAddPersonalAccess(EntityUid uid,
      string newAccess,
      EntityUid player,
      AccessOverriderComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.TargetAccessReaderId is not { Valid: true })
            return;


        if (!_interactionSystem.InRangeUnobstructed(player, component.TargetAccessReaderId))
        {
            _popupSystem.PopupEntity(Loc.GetString("access-overrider-out-of-range"), player, player);

            return;
        }

        if (!_accessReader.GetMainAccessReader(component.TargetAccessReaderId, out var accessReaderEnt))
            return;

        if (!PrivilegedIdIsAuthorized(uid, accessReaderEnt.Value, component))
            return;

        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(player):player} has modified {ToPrettyString(accessReaderEnt.Value):entity} with the following allowed access level holders: {newAccess}");

        _accessReader.TryAddPersonalAccess(accessReaderEnt.Value, newAccess);

        var ev = new OnAccessOverriderAccessUpdatedEvent(player);
        RaiseLocalEvent(component.TargetAccessReaderId, ref ev);
    }


    /// <summary>
    /// Returns true if there is an ID in <see cref="AccessOverriderComponent.PrivilegedIdSlot"/> and said ID satisfies the requirements of <see cref="AccessReaderComponent"/>.
    /// </summary>
    /// <remarks>
    /// Other code relies on the fact this returns false if privileged Id is null. Don't break that invariant.
    /// </remarks>
    private bool PrivilegedIdIsAuthorized(EntityUid uid, EntityUid? readerComponent, AccessOverriderComponent? component = null)
    {
        if (!Resolve(uid, ref component) || readerComponent == null)
            return true;
        
        var privilegedId = component.PrivilegedIdSlot.Item;
        return privilegedId != null && _accessReader.IsAllowed(privilegedId.Value, readerComponent.Value);
    }
}
