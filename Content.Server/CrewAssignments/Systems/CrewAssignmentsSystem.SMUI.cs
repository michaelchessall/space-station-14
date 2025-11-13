using Content.Server.Cargo.Components;
using Content.Shared.Cargo;
using Content.Shared.Cargo.BUI;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Events;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Database;
using Content.Shared.Emag.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Labels.Components;
using Content.Shared.Paper;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Popups;
using Content.Shared.CrewAssignments.Components;
using Content.Shared.CrewAssignments.Systems;
using Content.Shared.CrewAccesses.Components;
using Content.Shared.Access;

namespace Content.Server.CrewAssignments.Systems;

public sealed partial class CrewAssignmentSystem
{
    
    private void InitializeConsole()
    {
        SubscribeLocalEvent<StationModificationConsoleComponent, StationModificationRemoveOwner>(OnRemoveOwner);
        SubscribeLocalEvent<StationModificationConsoleComponent, StationModificationAddOwner>(OnAddOwner);
        SubscribeLocalEvent<StationModificationConsoleComponent, StationModificationChangeName>(OnChangeName);
        SubscribeLocalEvent<StationModificationConsoleComponent, StationModificationAddAccess>(OnAddAccess);
        SubscribeLocalEvent<StationModificationConsoleComponent, StationModificationRemoveAccess>(OnDeleteAccess);
        SubscribeLocalEvent<StationModificationConsoleComponent, StationModificationCreateAssignment>(OnCreateAssignment);
        SubscribeLocalEvent<StationModificationConsoleComponent, StationModificationToggleAssignmentAccess>(OnToggleAccess);
        SubscribeLocalEvent<StationModificationConsoleComponent, StationModificationChangeAssignmentCLevel>(OnChangeCLevel);
        SubscribeLocalEvent<StationModificationConsoleComponent, StationModificationChangeAssignmentWage>(OnChangeWage);
        SubscribeLocalEvent<StationModificationConsoleComponent, StationModificationChangeAssignmentName>(OnChangeAName);
        SubscribeLocalEvent<StationModificationConsoleComponent, StationModificationDeleteAssignment>(OnDeleteAssignment);
        SubscribeLocalEvent<StationModificationConsoleComponent, StationModificationDefaultAccess>(OnDefaultAccess);
        SubscribeLocalEvent<StationModificationConsoleComponent, BoundUIOpenedEvent>(OnOrderUIOpened);
        SubscribeLocalEvent<StationModificationConsoleComponent, ComponentInit>(OnInit);
    }


    private void OnInit(EntityUid uid, StationModificationConsoleComponent orderConsole, ComponentInit args)
    {
        var station = _station.GetOwningStation(uid);
        UpdateOrderState(uid, station);
    }

    #region Interface

    private bool Validate(EntityUid uid, StationModificationConsoleComponent component, EntityUid player, out StationDataComponent? stationData)
    {
        var station = _station.GetOwningStation(uid);

        if (station == null)
        {
            stationData = null;
            return false;
        }

        // No station to deduct from.
        if (!TryComp(station, out StationDataComponent? sD))
        {
            ConsolePopup(player, "Station not found!");
            stationData = null;
            return false;
        }
        stationData = sD;
        if (stationData.Owners.Count > 0 && !stationData.IsOwner(Name(player)))
        {
            ConsolePopup(player, "Access denied.");
            return false;
        }

        return true;
    }

    private void OnRemoveOwner(EntityUid uid, StationModificationConsoleComponent component, StationModificationRemoveOwner args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        var station = _station.GetOwningStation(uid);
        if (station == null) return;

        // No station to deduct from.
        if (!Validate(uid, component, player, out var stationData)) return;
        if (args.Owner == Name(player))
        {
            ConsolePopup(args.Actor, "You cannot remove yourself.");
            return;
        }
        stationData!.RemoveOwner(args.Owner);
        Dirty((EntityUid)station, stationData);
        UpdateOrders(station.Value);

    }
    private void OnChangeAName(EntityUid uid, StationModificationConsoleComponent component, StationModificationChangeAssignmentName args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        var station = _station.GetOwningStation(uid);
        if (station == null) return;

        if (!Validate(uid, component, player, out var stationData)) return;
        if (args.Owner == null || args.Owner == "") return;
        if (args.Owner.Length > 24)
        {
            ConsolePopup(player, "Exceeded Maximum Length of 24 Characters!");
            return;
        }
        if (!TryComp(station, out CrewAssignmentsComponent? crewAssignments))
        {
            ConsolePopup(player, "No CrewAssignment Component!");
            return;
        }
        if (!crewAssignments.CrewAssignments.TryGetValue(args.AccessID, out var crewAssignment))
        {
            ConsolePopup(player, "Invalid Assignment!");
            return;
        }
        foreach (var pair in crewAssignments.CrewAssignments)
        {
            if (pair.Value.Name == args.Owner)
            {
                ConsolePopup(player, "An assignment with that name already exists!");
                return;
            }
        }
        crewAssignment.Name = args.Owner;
        Dirty((EntityUid)station, crewAssignments);
        UpdateOrders(station.Value);

    }
    private void OnCreateAssignment(EntityUid uid, StationModificationConsoleComponent component, StationModificationCreateAssignment args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        var station = _station.GetOwningStation(uid);
        if (station == null) return;

        if (!Validate(uid, component, player, out var stationData)) return;
        if (args.Owner == null || args.Owner == "") return;
        if (args.Owner.Length > 24)
        {
            ConsolePopup(player, "Exceeded Maximum Length of 24 Characters!");
            return;
        }
        if (!TryComp(station, out CrewAssignmentsComponent? crewAssignments))
        {
            ConsolePopup(player, "No CrewAssignment Component!");
            return;
        }
        foreach (var pair in crewAssignments.CrewAssignments)
        {
            if(pair.Value.Name == args.Owner)
            {
                ConsolePopup(player, "An assignment with that name already exists!");
                return;
            }
        }
        crewAssignments.CreateAssignment(args.Owner);
        Dirty((EntityUid)station, crewAssignments);
        UpdateOrders(station.Value);
    }

    private void OnChangeCLevel(EntityUid uid, StationModificationConsoleComponent component, StationModificationChangeAssignmentCLevel args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        var station = _station.GetOwningStation(uid);
        if (station == null) return;

        if (!Validate(uid, component, player, out var stationData)) return;
        if (!TryComp(station, out CrewAssignmentsComponent? crewAssignments))
        {
            ConsolePopup(player, "No CrewAssignment Component!");
            return;
        }
        if (!crewAssignments.CrewAssignments.TryGetValue(args.AccessID, out var crewAssignment))
        {
            ConsolePopup(player, "Invalid Assignment!");
            return;
        }
        if (args.Level < 0) return;
        crewAssignment.Clevel = args.Level;
        Dirty((EntityUid)station, crewAssignments);
        UpdateOrders(station.Value);

    }

    private void OnDefaultAccess(EntityUid uid, StationModificationConsoleComponent component, StationModificationDefaultAccess args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        var station = _station.GetOwningStation(uid);
        if (station == null) return;

        if (!Validate(uid, component, player, out var stationData)) return;
        if (!TryComp(station, out CrewAccessesComponent? crewAccesses))
        {
            ConsolePopup(player, "No CrewAccesses Component!");
            stationData = null;
            return;
        }
        foreach (var accessLevel in _protoMan.EnumeratePrototypes<AccessLevelPrototype>())
        {
            if(!accessLevel.CanAddToIdCard || crewAccesses.CrewAccesses.ContainsKey(accessLevel.ID))
            {
                continue;
            }
            crewAccesses.CreateAccess(accessLevel.ID);
        }
        Dirty((EntityUid)station, crewAccesses);
        UpdateOrders(station.Value);
    }

    private void OnDeleteAssignment(EntityUid uid, StationModificationConsoleComponent component, StationModificationDeleteAssignment args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        var station = _station.GetOwningStation(uid);
        if (station == null) return;

        if (!Validate(uid, component, player, out var stationData)) return;
        if (!TryComp(station, out CrewAssignmentsComponent? crewAssignments))
        {
            ConsolePopup(player, "No CrewAssignment Component!");
            return;
        }
        if (!crewAssignments.CrewAssignments.TryGetValue(args.AccessID, out var crewAssignment))
        {
            ConsolePopup(player, "Invalid Assignment!");
            return;
        }
        crewAssignments.CrewAssignments.Remove(args.AccessID);
        Dirty((EntityUid)station, crewAssignments);
        UpdateOrders(station.Value);

    }
    private void OnChangeWage(EntityUid uid, StationModificationConsoleComponent component, StationModificationChangeAssignmentWage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        var station = _station.GetOwningStation(uid);
        if (station == null) return;

        if (!Validate(uid, component, player, out var stationData)) return;
        if (!TryComp(station, out CrewAssignmentsComponent? crewAssignments))
        {
            ConsolePopup(player, "No CrewAssignment Component!");
            return;
        }
        if (!crewAssignments.CrewAssignments.TryGetValue(args.AccessID, out var crewAssignment))
        {
            ConsolePopup(player, "Invalid Assignment!");
            return;
        }
        if (args.Wage < 0) return;
        crewAssignment.Wage = args.Wage;
        Dirty((EntityUid)station, crewAssignments);
        UpdateOrders(station.Value);

    }

    private void OnToggleAccess(EntityUid uid, StationModificationConsoleComponent component, StationModificationToggleAssignmentAccess args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        var station = _station.GetOwningStation(uid);
        if (station == null) return;

        if (!Validate(uid, component, player, out var stationData)) return;
        if (!TryComp(station, out CrewAssignmentsComponent? crewAssignments))
        {
            ConsolePopup(player, "No CrewAssignment Component!");
            return;
        }
        if (!crewAssignments.CrewAssignments.TryGetValue(args.AccessID, out var crewAssignment))
        {
            ConsolePopup(player, "Invalid Assignment!");
            return;
        }
        if(args.ToggleState)
        {
            if (crewAssignment.AccessIDs.Contains(args.Access))
            {
                return;
            }
            else
            {
                crewAssignment.AccessIDs.Add(args.Access);
            }
        }
        else
        {
            if (crewAssignment.AccessIDs.Contains(args.Access))
            {
                crewAssignment.AccessIDs.Remove(args.Access);
            }
            else
            {
                return;
            }
        }
        Dirty((EntityUid)station, crewAssignments);
        UpdateOrders(station.Value);

    }
    private void OnChangeName(EntityUid uid, StationModificationConsoleComponent component, StationModificationChangeName args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        var station = _station.GetOwningStation(uid);
        if (station == null) return;

        // No station to deduct from.
        if (!Validate(uid, component, player, out var stationData)) return;
        if (args.Owner == null || args.Owner == "") return;
        if(args.Owner.Length > 24)
        {
            ConsolePopup(player, "Exceeded Maximum Length of 24 Characters!");
            return;
        }
        _station2.RenameStation(station.Value, args.Owner);
        UpdateOrders(station.Value);

    }
    private void OnAddOwner(EntityUid uid, StationModificationConsoleComponent component, StationModificationAddOwner args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        var station = _station.GetOwningStation(uid);
        if (station == null) return;

        // No station to deduct from.
        if (!Validate(uid, component, player, out var stationData)) return;
        if (args.Owner == null || args.Owner == "") return;
        if (stationData!.IsOwner(args.Owner))
        {
            ConsolePopup(args.Actor, "That owner already exists.");
            return;
        }
        if (args.Owner == null || args.Owner == "") return;
        stationData.AddOwner(args.Owner);
        Dirty((EntityUid)station, stationData);
        UpdateOrders(station.Value);
    }
    private void OnAddAccess(EntityUid uid, StationModificationConsoleComponent component, StationModificationAddAccess args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        var station = _station.GetOwningStation(uid);
        if (station == null) return;

        // No station to deduct from.
        if (!Validate(uid, component, player, out var stationData)) return;
        if (args.Owner == null || args.Owner == "") return;
        // No station to deduct from.
        if (!TryComp(station, out CrewAccessesComponent? crewAccesses))
        {
            ConsolePopup(player, "No CrewAccesses Component!");
            stationData = null;
            return;
        }
        if (crewAccesses.CrewAccesses.ContainsKey(args.Owner))
        {
            ConsolePopup(args.Actor, "That access already exists.");
            return;
        }
        if (args.Owner == null || args.Owner == "") return;
        if (args.Owner.Length > 24)
        {
            ConsolePopup(player, "Exceeded Maximum Length of 24 Characters!");
            return;
        }
        crewAccesses.CreateAccess(args.Owner);
        Dirty((EntityUid)station, crewAccesses);
        UpdateOrders(station.Value);
    }

    private void OnDeleteAccess(EntityUid uid, StationModificationConsoleComponent component, StationModificationRemoveAccess args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        var station = _station.GetOwningStation(uid);
        if (station == null) return;

        if (!Validate(uid, component, player, out var stationData)) return;
        if (args.Owner == null || args.Owner == "") return;
        if (!TryComp(station, out CrewAccessesComponent? crewAccesses))
        {
            ConsolePopup(player, "No CrewAccesses Component!");
            stationData = null;
            return;
        }
        if (args.Owner == null || args.Owner == "") return;
        crewAccesses.RemoveAccess(args.Owner);
        Dirty((EntityUid)station, crewAccesses);
        UpdateOrders(station.Value);
    }



    private void OnOrderUIOpened(EntityUid uid, StationModificationConsoleComponent component, BoundUIOpenedEvent args)
    {
        var station = _station.GetOwningStation(uid);
        UpdateOrderState(uid, station);
    }

    #endregion

    private void UpdateOrderState(EntityUid consoleUid, EntityUid? station)
    {
        if (!TryComp<StationDataComponent>(station, out var data))
            return;
        if (!TryComp<StationModificationConsoleComponent>(consoleUid, out var console))
            return;
        if (!TryComp<CrewAccessesComponent>(station, out var cadata))
            return;
        if (!TryComp<CrewAssignmentsComponent>(station, out var casdata))
            return;
        if (_uiSystem.HasUi(consoleUid, StationModUiKey.StationMod))
        {
            _uiSystem.SetUiState(consoleUid,
                StationModUiKey.StationMod,
                new StationModificationInterfaceState(
                MetaData(station!.Value).EntityName,
                GetNetEntity(station.Value),
                data.Owners,
                cadata.CrewAccesses,
                casdata.CrewAssignments
            ));
        }
    }

    private void ConsolePopup(EntityUid actor, string text)
    {
        _popup.PopupCursor(text, actor);
    }


    private void UpdateOrders(EntityUid dbUid)
    {
        // Order added so all consoles need updating.
        var orderQuery = AllEntityQuery<StationModificationConsoleComponent>();

        while (orderQuery.MoveNext(out var uid, out var _))
        {
            var station = _station.GetOwningStation(uid);
            if (station != dbUid)
                continue;

            UpdateOrderState(uid, station);
        }
    }


}
