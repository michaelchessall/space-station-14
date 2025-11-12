using Content.Server.Doors.Electronics;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CrewAccesses.Components;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Doors;
using Content.Shared.Doors.Electronics;
using Content.Shared.Interaction;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server.Doors.Electronics;

public sealed class DoorElectronicsSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DoorElectronicsComponent, DoorElectronicsUpdateConfigurationMessage>(OnChangeConfiguration);
        SubscribeLocalEvent<DoorElectronicsComponent, DoorElectronicsAccessToggleMessage>(OnAccessToggle);
        SubscribeLocalEvent<DoorElectronicsComponent, DoorElectronicsPersonalRemoveMessage>(OnPersonalAccessToggle);
        SubscribeLocalEvent<DoorElectronicsComponent, DoorElectronicsPersonalAddMessage>(OnPersonalAccessAdd);
        SubscribeLocalEvent<DoorElectronicsComponent, DoorElectronicsChangeModeMessage>(OnChangeMode);
        SubscribeLocalEvent<DoorElectronicsComponent, AccessReaderConfigurationChangedEvent>(OnAccessReaderChanged);
        SubscribeLocalEvent<DoorElectronicsComponent, BoundUIOpenedEvent>(OnBoundUIOpened);
    }

    public void UpdateUserInterface(EntityUid uid, DoorElectronicsComponent component)
    {
        var accesses = new List<string>();
        List<string>? possibleAccesses = null;
        var station = _station.GetOwningStation(uid);
        
        string stationName = "*None*";
        if (station != null)
        {
            if (TryComp<StationDataComponent>(station, out var sD) && sD != null && sD.StationName != null)
            {
                stationName = sD.StationName;
            }
            if (TryComp<CrewAccessesComponent>(station, out var crewAccesses))
            {
                possibleAccesses = new();
                foreach (var access in crewAccesses.CrewAccesses)
                {
                    possibleAccesses.Add(access.Key);
                }
            }
        }
        bool personalAccess = false;
        List<string>? personalAccessList = null;
        if (TryComp<AccessReaderComponent>(uid, out var accessReader))
        {
            foreach (var access in accessReader.AccessNames)
            {
                accesses.Add(access);
            }
            personalAccess = accessReader.PersonalAccessMode;
            personalAccessList = accessReader.PersonalAccessNames;
        }

        var state = new DoorElectronicsConfigurationState(accesses, possibleAccesses, stationName, personalAccess, personalAccessList);
        _uiSystem.SetUiState(uid, DoorElectronicsConfigurationUiKey.Key, state);
    }

    private void OnChangeConfiguration(
        EntityUid uid,
        DoorElectronicsComponent component,
        DoorElectronicsUpdateConfigurationMessage args)
    {
        var accessReader = EnsureComp<AccessReaderComponent>(uid);
        _accessReader.TrySetAccesses((uid, accessReader), args.AccessList);
    }

    private void OnPersonalAccessToggle(
        EntityUid uid,
        DoorElectronicsComponent component,
        DoorElectronicsPersonalRemoveMessage args)
    {
        var accessReader = EnsureComp<AccessReaderComponent>(uid);
        _accessReader.TryTogglePersonalAccess((uid, accessReader), args.Access);
        UpdateUserInterface(uid, component);
    }
    private void OnPersonalAccessAdd(
       EntityUid uid,
       DoorElectronicsComponent component,
       DoorElectronicsPersonalAddMessage args)
    {
        var accessReader = EnsureComp<AccessReaderComponent>(uid);
        _accessReader.TryAddPersonalAccess((uid, accessReader), args.Access);
        UpdateUserInterface(uid, component);
    }
    private void OnChangeMode(
       EntityUid uid,
       DoorElectronicsComponent component,
       DoorElectronicsChangeModeMessage args)
    {
        var accessReader = EnsureComp<AccessReaderComponent>(uid);
        _accessReader.TryChangeMode((uid, accessReader));
        UpdateUserInterface(uid, component);
    }
    private void OnAccessToggle(
        EntityUid uid,
        DoorElectronicsComponent component,
        DoorElectronicsAccessToggleMessage args)
    {
        var accessReader = EnsureComp<AccessReaderComponent>(uid);
        _accessReader.TryToggleAccess((uid, accessReader), args.Access);
        UpdateUserInterface(uid, component);
    }

    private void OnAccessReaderChanged(
        EntityUid uid,
        DoorElectronicsComponent component,
        AccessReaderConfigurationChangedEvent args)
    {
        UpdateUserInterface(uid, component);
    }

    private void OnBoundUIOpened(
        EntityUid uid,
        DoorElectronicsComponent component,
        BoundUIOpenedEvent args)
    {
        UpdateUserInterface(uid, component);
    }
}
