using Content.Server.Cargo.Components;
using Content.Server.CrewRecords.Systems;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.CrewAssignments.Components;
using Content.Shared.CrewAssignments.Prototypes;
using Content.Shared.CrewRecords.Components;
using Content.Shared.DoAfter;
using Content.Shared.GridControl.Components;
using Content.Shared.GridControl.Systems;
using Content.Shared.Interaction;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using System.Linq;
using static Content.Shared.GridControl.Components.GridConfigComponent;
using static Content.Shared.GridControl.Components.GridControlConsoleComponent;
using static Content.Shared.GridControl.Components.StationCreatorComponent;

namespace Content.Server.GridControl.Systems;

[UsedImplicitly]
public sealed class GridConfigSystem : SharedGridConfigSystem
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly CrewMetaRecordsSystem _crewMeta = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GridConfigComponent, ComponentStartup>(UpdateUserInterface);
        SubscribeLocalEvent<GridConfigComponent, EntInsertedIntoContainerMessage>(UpdateUserInterface);
        SubscribeLocalEvent<GridConfigComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<StationCreatorComponent, EntInsertedIntoContainerMessage>(UpdateUserInterface);
        SubscribeLocalEvent<StationCreatorComponent, EntRemovedFromContainerMessage>(OnRemoved);


        SubscribeLocalEvent<StationTaggerComponent, StationTaggerDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<StationTaggerComponent, AfterInteractEvent>(AfterInteractOn);
        SubscribeLocalEvent<StationTaggerComponent, ComponentStartup>(UpdateUserInterface);
        SubscribeLocalEvent<StationTaggerComponent, EntInsertedIntoContainerMessage>(UpdateUserInterface);
        SubscribeLocalEvent<StationTaggerComponent, EntRemovedFromContainerMessage>(OnRemoved);


        Subs.BuiEvents<GridConfigComponent>(GridConfigUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<GridConfigChangeName>(OnChangeName);
            subs.Event<GridConfigTargetSelect>(OnTargetSelect);
            subs.Event<GridConfigChangeMode>(OnChangeMode);
            subs.Event<GridConfigConnect>(OnConnect);
            subs.Event<GridConfigDisconnect>(OnDisconnect);
        });

        Subs.BuiEvents<StationTaggerComponent>(StationTaggerUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<StationTaggerTargetSelect>(OnTargetSelect);
            subs.Event<StationTaggerLink>(OnLink);
            subs.Event<StationTaggerUnlink>(OnUnlink);
        });

        Subs.BuiEvents<StationCreatorComponent>(StationCreatorUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<StationCreatorFinish>(OnStationCreate);
        });

        Subs.BuiEvents<GridControlConsoleComponent>(GridControlConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<GridControlOn>(OnGridControlOn);
            subs.Event<GridControlOff>(OnGridControlOff);
        });

    }

    private void OnUnlink(EntityUid uid, StationTaggerComponent component, EntityEventArgs args)
    {
        if (component.TargetAccessReaderId == EntityUid.Invalid) return;

        if (!_accessReader.GetMainAccessReader(component.TargetAccessReaderId, out var accessReaderEnt))
            return;
        if (accessReaderEnt == null) return;
        if (TryComp<StationTrackerComponent>(accessReaderEnt.Value.Owner, out var comp) && comp != null)
        {
            RemComp(accessReaderEnt.Value.Owner, comp);
        }
        UpdateUserInterface(uid, component, args);
    }
    private void OnLink(EntityUid uid, StationTaggerComponent component, EntityEventArgs args)
    {
        if (component.TargetAccessReaderId == EntityUid.Invalid) return;
        if (component.ConnectedStation == null || component.ConnectedStation == 0) return;
        if (TryComp<StationTrackerComponent>(component.TargetAccessReaderId, out var comp) && comp != null)
        {
            return;
        }
        if (!_accessReader.GetMainAccessReader(component.TargetAccessReaderId, out var accessReaderEnt))
            return;
        if (accessReaderEnt == null) return;

        if (component.ConnectedStation == null) return;
        var station = _station.GetStationByID(component.ConnectedStation.Value);
        if (station == null) return;
        var comp2 = EnsureComp<StationTrackerComponent>(accessReaderEnt.Value.Owner);
        if (comp2 == null) return;
        comp2.locked = false;
        _station.SetStation((accessReaderEnt.Value.Owner, comp2), station);
        comp2.locked = true;
        UpdateUserInterface(uid, component, args);
    }
    private void OnRemoved(EntityUid uid, GridConfigComponent component, EntityEventArgs args)
    {
        component.ConnectedStation = null;
        UpdateUserInterface(uid, component, args);
    }
    private void OnRemoved(EntityUid uid, StationCreatorComponent component, EntityEventArgs args)
    {
        component.ConnectedStation = null;
        UpdateUserInterface(uid, component, args);
    }
    private void OnRemoved(EntityUid uid, StationTaggerComponent component, EntityEventArgs args)
    {
        component.ConnectedStation = null;
        UpdateUserInterface(uid, component, args);
    }
    private void OnChangeMode(EntityUid uid, GridConfigComponent component, GridConfigChangeMode args)
    {
        component.PersonalMode = !component.PersonalMode;
        UpdateUserInterface(uid, component, args);
    }
    private void OnTargetSelect(EntityUid uid, GridConfigComponent component, GridConfigTargetSelect args)
    {
        component.ConnectedStation = args.Target;
        UpdateUserInterface(uid, component, args);
    }

    private void OnTargetSelect(EntityUid uid, StationTaggerComponent component, StationTaggerTargetSelect args)
    {
        component.ConnectedStation = args.Target;
        UpdateUserInterface(uid, component, args);
    }

    private bool ValidateFactionClaim(EntityUid uid, GridConfigComponent component, bool requireTarget = true)
    {
        if (component.ConnectedStation == null) return false;
        var targetGrid = _transform.GetGrid(uid);
        var targetStation = _station.GetStationByID(component.ConnectedStation.Value);
        bool tradeStationGrid = false;
        if (TryComp<TradeStationComponent>(targetGrid, out var tradeStation))
        {
            tradeStationGrid = true;
        }
        if (TryComp<StationDataComponent>(targetStation, out var owningSD) && owningSD != null)
        {
            _protoMan.Resolve(owningSD.Level, out var levelProto);
            if (levelProto != null)
            {
                if (tradeStationGrid)
                {
                    if (!levelProto.TradestationClaim)
                    {
                        return false;
                    }
                }
                if (targetGrid.HasValue && TryComp<MapGridComponent>(targetGrid, out var targetGridComp))
                {
                    var tiles = _mapSystem.GetAllTiles(targetGrid.Value, targetGridComp);
                    var currentTiles = _station.GetStationTileCount(targetStation.Value);
                    if (tiles.Count() + currentTiles > levelProto.TileLimit)
                        return false;
                }
                else
                    return false;
            }
        }
        return true;
    }
    private bool ValidatePersonalClaim(EntityUid uid, GridConfigComponent component, bool requireTarget = true)
    {
        var privilegedName = string.Empty;
        if (component.PrivilegedIdSlot.Item is { Valid: true } idCard)
        {
            if (TryComp<IdCardComponent>(idCard, out var id) && id.FullName != null)
            {
                privilegedName = id.FullName;
            }
        }
        if (privilegedName == string.Empty) return false;
        if (_crewMeta.MetaRecords == null) return false;
        if (!_crewMeta.MetaRecords.TryGetRecord(privilegedName, out var record) || record == null) return false;
        int limit = 0;
        _protoMan.Resolve(record.Level, out var levelProto);
        if (levelProto == null) return false;
        limit = levelProto.TileLimit;
        if (limit < 1) return false;
        var targetGrid = _transform.GetGrid(uid);
        bool tradeStationGrid = false;
        if (tradeStationGrid) return false;
        if (targetGrid.HasValue && TryComp<MapGridComponent>(targetGrid, out var targetGridComp))
        {
            var tiles = _mapSystem.GetAllTiles(targetGrid.Value, targetGridComp);
            var currentTiles = _station.GetPersonalTileCount(privilegedName);
            if (tiles.Count() + currentTiles > limit)
                return false;
        }
        else
            return false;

        return true;
    }
    private bool Validate(EntityUid uid, GridConfigComponent component, bool requireTarget = true)
    {
        EntityUid? owningStation = _station.GetOwningStation(uid);
        bool owningPersonal = false;
        string? owningPerson = null;
        var privilegedIdName = string.Empty;
        var privilegedName = string.Empty;
        bool idPresent = false;

        var targetGrid = _transform.GetGrid(uid);
        //bool tradeStationGrid = false;
        //if (TryComp<TradeStationComponent>(targetGrid, out var tradeStation))
        //{
        //    tradeStationGrid = true;
        //}
        if (component.PrivilegedIdSlot.Item is { Valid: true } idCard)
        {
            idPresent = true;
            privilegedIdName = Comp<MetaDataComponent>(idCard).EntityName;
            if (TryComp<IdCardComponent>(idCard, out var id) && id.FullName != null)
            {
                privilegedName = id.FullName;
            }

        }
        if (!idPresent) return false;
        if (owningStation == null)
        {
            owningPerson = _station.GetOwningStationPersonal(uid);
            if (owningPerson != null)
            {
                owningPersonal = true;
            }
        }
        else
        {
            if (TryComp<StationDataComponent>(owningStation, out var oSD) && oSD != null)
            {
                owningPerson = oSD.StationName;

            }
        }
        bool isOwner = false;
        bool isAuth = false;
        if (owningStation != null)
        {
            if (TryComp<StationDataComponent>(owningStation, out var owningSD) && owningSD != null)
            {

                if (owningSD.Owners.Contains(privilegedName))
                {
                    isOwner = true;
                    isAuth = true;
                }
                else
                {
                    if (TryComp<CrewRecordsComponent>(owningStation, out var owningCrew) && owningCrew != null)
                    {
                        if (owningCrew.TryGetRecord(privilegedName, out var crewRecord) && crewRecord != null)
                        {
                            if (TryComp<CrewAssignmentsComponent>(owningStation, out var crewAssignments) && crewAssignments != null)
                            {
                                if (crewAssignments.TryGetAssignment(crewRecord.AssignmentID, out var crewAssignment) && crewAssignment != null)
                                {
                                    if (crewAssignment.CanClaim)
                                    {
                                        isAuth = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        else if (owningPersonal)
        {
            if (owningPerson == privilegedName)
            {
                isOwner = true;
                isAuth = true;
            }
        }
        if (!owningPersonal && owningStation == null) isAuth = true;
        if (!requireTarget) return isAuth;
        if (isAuth)
        {
            isAuth = false;
            if (component.PersonalMode) return true;
            if (component.ConnectedStation == null) return false;
            var targetStation = _station.GetStationByID(component.ConnectedStation.Value);
            if (targetStation == null) return false;
            if (TryComp<StationDataComponent>(targetStation, out var owningSD) && owningSD != null)
            {
                if (owningSD.Owners.Contains(privilegedName))
                {
                    isAuth = true;
                }
                else
                {
                    if (TryComp<CrewRecordsComponent>(targetStation, out var owningCrew) && owningCrew != null)
                    {
                        if (owningCrew.TryGetRecord(privilegedName, out var crewRecord) && crewRecord != null)
                        {
                            if (TryComp<CrewAssignmentsComponent>(targetStation, out var crewAssignments) && crewAssignments != null)
                            {
                                if (crewAssignments.TryGetAssignment(crewRecord.AssignmentID, out var crewAssignment) && crewAssignment != null)
                                {
                                    if (crewAssignment.CanClaim)
                                    {
                                        isAuth = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        return isOwner || isAuth;
    }
    private void OnChangeName(EntityUid uid, GridConfigComponent component, GridConfigChangeName args)
    {
        if (Validate(uid, component, false))
        {
            var grid = _transform.GetGrid(uid);
            if (grid != null)
            {
                _metaData.SetEntityName(grid.Value, args.Name);
            }
        }
        UpdateUserInterface(uid, component, args);
    }
    private void OnConnect(EntityUid uid, GridConfigComponent component, GridConfigConnect args)
    {
        if (Validate(uid, component))
        {
            if (component.PersonalMode)
            {
                if (!ValidatePersonalClaim(uid, component, true))
                {
                    return;
                }
            }
            else
            {
                if (!ValidateFactionClaim(uid, component, true))
                {
                    return;
                }
            }
            var grid = _transform.GetGrid(uid);
            if (TryComp<TradeStationComponent>(grid, out _))
            {
                if (component.PersonalMode) return;
                if (component.ConnectedStation == null) return;
                var station = _station.GetStationByID(component.ConnectedStation.Value);
                if (station == null) return;
                var currGrid = _station.GetStationTradeStation(station.Value);
                if (currGrid != null && currGrid != grid)
                {
                    return;
                }
            }
            if (grid != null)
            {
                if (component.PersonalMode)
                {
                    if (component.PrivilegedIdSlot.Item is { Valid: true } idCard)
                    {
                        if (TryComp<IdCardComponent>(idCard, out var id) && id.FullName != null)
                        {
                            _station.AddGridToPerson(id.FullName, grid.Value);
                        }

                    }
                }
                else if (component.ConnectedStation != null)
                {
                    var station = _station.GetStationByID(component.ConnectedStation.Value);
                    if (station != null)
                    {
                        _station.AddGridToStation(station.Value, grid.Value);
                    }
                }
            }
        }
        UpdateUserInterface(uid, component, args);
    }

    private void OnStationCreate(EntityUid uid, StationCreatorComponent component, StationCreatorFinish args)
    {
        string? realName = null;
        EntityUid? idCard = null;
        if (component.PrivilegedIdSlot.Item is { Valid: true } idc)
        {
            idCard = idc;
            if (TryComp<IdCardComponent>(idCard, out var id) && id.FullName != null)
            {
                realName = id.FullName;
            }

        }
        if (realName == null || realName == "") return;
        if (args.StationName == null || args.StationName == "") return;
        StationConfig config = new();
        config.StationPrototype = "StandardNanotrasenStation";
        _station.InitializeNewStation(config, null, args.StationName, realName);
        if (idCard != null)
        {
            _popupSystem.PopupEntity($"The station {args.StationName} was created.", idCard.Value);
            _itemSlots.TryEjectToHands(idCard.Value, component.PrivilegedIdSlot, args.Actor);
        }
        TryQueueDel(uid);
    }

    private void OnDisconnect(EntityUid uid, GridConfigComponent component, GridConfigDisconnect args)
    {

        var grid = _transform.GetGrid(uid);
        if (grid != null)
        {
            if (IsGridControlled(grid.Value)) return;
            var station = _station.GetOwningStation(grid.Value);
            if (station != null)
            {
                _station.RemoveGridFromStation(station.Value, grid.Value);
            }
            else
            {
                var ownername = _station.GetOwningStationPersonal(grid.Value);
                if (ownername != null)
                {
                    _station.RemoveGridFromPerson(grid.Value);
                }
            }

        }
        UpdateUserInterface(uid, component, args);
    }

    private void AfterInteractOn(EntityUid uid, StationTaggerComponent component, AfterInteractEvent args)
    {
        if (args.Target == null || !TryComp(args.Target, out AccessReaderComponent? accessReader))
            return;

        if (!_interactionSystem.InRangeUnobstructed(args.User, (EntityUid)args.Target))
            return;

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.DoAfter, new StationTaggerDoAfterEvent(), uid, target: args.Target, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfterSystem.TryStartDoAfter(doAfterEventArgs);
    }


    private void OnDoAfter(EntityUid uid, StationTaggerComponent component, StationTaggerDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Args.Target != null)
        {
            component.TargetAccessReaderId = args.Args.Target.Value;
            _userInterface.OpenUi(uid, StationTaggerUiKey.Key, args.User);
            UpdateUserInterface(uid, component, args);
        }

        args.Handled = true;
    }


    private void UpdateUserInterface(EntityUid uid, GridConfigComponent component, EntityEventArgs args)
    {
        if (!component.Initialized)
            return;
        int currentTileCount = 0;
        EntityUid? station = null;
        if (component.ConnectedStation != null)
        {
            station = _station.GetStationByID(component.ConnectedStation.Value);
            if (station != null)
            {
                currentTileCount = _station.GetStationTileCount(station.Value);
            }
        }
        EntityUid? owningStation = _station.GetOwningStation(uid);
        string? owningPerson = null;
        if (owningStation == null)
        {
            owningPerson = _station.GetOwningStationPersonal(uid);
        }
        else
        {
            if (TryComp<StationDataComponent>(owningStation, out var oSD) && oSD != null)
            {
                owningPerson = oSD.StationName;
            }
        }
        var privilegedIdName = string.Empty;
        var privilegedName = string.Empty;
        bool idPresent = false;
        if (component.PrivilegedIdSlot.Item is { Valid: true } idCard)
        {
            idPresent = true;
            privilegedIdName = Comp<MetaDataComponent>(idCard).EntityName;
            if (TryComp<IdCardComponent>(idCard, out var id) && id.FullName != null)
            {
                privilegedName = id.FullName;
            }

        }
        bool isOwner = false;
        bool isAuth = false;
        string gridName = string.Empty;
        var grid = _transform.GetGrid(uid);
        if (grid != null)
        {
            gridName = Name(grid.Value);
        }

        if (privilegedName != "")
        {
            if (owningStation != null)
            {
                if (TryComp<StationDataComponent>(owningStation, out var owningSD) && owningSD != null)
                {
                    if (owningSD.Owners.Contains(privilegedName))
                    {
                        isOwner = true;
                        isAuth = true;
                    }
                    else
                    {
                        if (TryComp<CrewRecordsComponent>(owningStation, out var owningCrew) && owningCrew != null)
                        {
                            if (owningCrew.TryGetRecord(privilegedName, out var crewRecord) && crewRecord != null)
                            {
                                if (TryComp<CrewAssignmentsComponent>(owningStation, out var crewAssignments) && crewAssignments != null)
                                {
                                    if (crewAssignments.TryGetAssignment(crewRecord.AssignmentID, out var crewAssignment) && crewAssignment != null)
                                    {
                                        if (crewAssignment.CanClaim)
                                        {
                                            isAuth = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else if (owningPerson != null)
            {
                if (owningPerson == privilegedName)
                {
                    isOwner = true;
                    isAuth = true;
                }
            }
            else
            {
                if (grid != null)
                {
                    isOwner = true;
                    isAuth = true;
                }
            }

        }

        int tileLimit = 0;
        String? targetName = null;
        int? targetStation = null;
        Dictionary<int, string>? possibleStations = null;

        FactionLevelPrototype? factionLevel = null;
        if (TryComp<StationDataComponent>(station, out var sD) && sD != null)
        {
            targetName = sD.StationName;
            targetStation = sD.UID;
            _protoMan.Resolve(sD.Level, out var levelProto);
            factionLevel = levelProto;
            if (!component.PersonalMode)
            {
                tileLimit = factionLevel?.TileLimit ?? tileLimit;
            }

        }

        if (component.PersonalMode)
        {
            targetName = privilegedName;
            currentTileCount = _station.GetPersonalTileCount(targetName);
            if (_crewMeta.MetaRecords != null)
            {
                if (_crewMeta.MetaRecords.TryGetRecord(privilegedName, out var record) && record != null)
                {
                    _protoMan.Resolve(record.Level, out var levelProto);
                    if (levelProto != null)
                        tileLimit = levelProto.TileLimit;
                }
            }
        }
        else
        {
            var stations = _station.GetStations();
            possibleStations = new Dictionary<int, string>();
            foreach (var iStation in stations)
            {
                bool auth = false;
                var station_name = "";
                int stationID = 0;
                if (TryComp<StationDataComponent>(iStation, out var owningSD) && owningSD != null)
                {
                    stationID = owningSD.UID;
                    if (owningSD.StationName != null)
                        station_name = owningSD.StationName;
                    if (owningSD.Owners.Contains(privilegedName))
                    {
                        auth = true;
                    }
                    else
                    {
                        if (TryComp<CrewRecordsComponent>(iStation, out var owningCrew) && owningCrew != null)
                        {
                            if (owningCrew.TryGetRecord(privilegedName, out var crewRecord) && crewRecord != null)
                            {
                                if (TryComp<CrewAssignmentsComponent>(iStation, out var crewAssignments) && crewAssignments != null)
                                {
                                    if (crewAssignments.TryGetAssignment(crewRecord.AssignmentID, out var crewAssignment) && crewAssignment != null)
                                    {
                                        if (crewAssignment.CanClaim)
                                        {
                                            auth = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (auth)
                {
                    possibleStations.Add(stationID, station_name);
                }

            }
        }

        GridConfigBoundUserInterfaceState newState;
        string? errMsg = null;
        bool controlled = false;
        if (grid != null)
        {
            controlled = IsGridControlled(grid.Value);
        }
        int gridTileCount = 0;
        if (TryComp<MapGridComponent>(grid, out var targetGridComp))
            gridTileCount = _mapSystem.GetAllTiles(grid.Value, targetGridComp).Count();


        if (TryComp<TradeStationComponent>(grid, out var tradeStation) && tradeStation != null)
        {
            if (!component.PersonalMode && factionLevel != null && station.HasValue)
            {
                if (!factionLevel.TradestationClaim)
                {
                    errMsg = "This faction cannot claim a trade station yet.";
                }
                else if (_station.GetStationTradeStation(station.Value) != null && _station.GetOwningStation(grid) != station)
                {
                    errMsg = "This faction already controls a trade station.";
                }
            }
            else
            {
                errMsg = "Trade stations cannot be claimed by individuals";
            }
        }
        newState = new GridConfigBoundUserInterfaceState(
            idPresent, isOwner, isAuth, component.PersonalMode, controlled, possibleStations,
            targetName, owningPerson, gridName, privilegedIdName, targetStation,
            gridTileCount, currentTileCount, tileLimit, errMsg);

        _userInterface.SetUiState(uid, GridConfigUiKey.Key, newState);
    }

    private void UpdateUserInterface(EntityUid uid, StationCreatorComponent component, EntityEventArgs args)
    {
        if (!component.Initialized)
            return;
        var privilegedIdName = string.Empty;
        var privilegedName = string.Empty;
        bool idPresent = false;
        if (component.PrivilegedIdSlot.Item is { Valid: true } idCard)
        {
            idPresent = true;
            privilegedIdName = Comp<MetaDataComponent>(idCard).EntityName;
            if (TryComp<IdCardComponent>(idCard, out var id) && id.FullName != null)
            {
                privilegedName = id.FullName;
            }

        }

        StationCreatorBoundUserInterfaceState newState;

        newState = new StationCreatorBoundUserInterfaceState(idPresent, privilegedIdName, privilegedName);

        _userInterface.SetUiState(uid, StationCreatorUiKey.Key, newState);
    }

    private void UpdateUserInterface(EntityUid uid, StationTaggerComponent component, EntityEventArgs args)
    {
        if (!component.Initialized)
            return;
        EntityUid? station = null;
        if (component.ConnectedStation != null)
        {
            station = _station.GetStationByID(component.ConnectedStation.Value);
        }
        var privilegedIdName = string.Empty;
        var privilegedName = string.Empty;
        bool idPresent = false;
        if (component.PrivilegedIdSlot.Item is { Valid: true } idCard)
        {
            idPresent = true;
            privilegedIdName = Comp<MetaDataComponent>(idCard).EntityName;
            if (TryComp<IdCardComponent>(idCard, out var id) && id.FullName != null)
            {
                privilegedName = id.FullName;
            }

        }
        String? targetName = null;
        int? targetStation = null;
        Dictionary<int, string>? possibleStations = null;

        if (TryComp<StationDataComponent>(station, out var sD) && sD != null)
        {
            targetName = sD.StationName;
            targetStation = sD.UID;
        }
        var stations = _station.GetStations();
        possibleStations = new Dictionary<int, string>();
        foreach (var iStation in stations)
        {
            bool auth = false;
            var station_name = "";
            int stationID = 0;
            if (TryComp<StationDataComponent>(iStation, out var owningSD) && owningSD != null)
            {
                stationID = owningSD.UID;
                if (owningSD.StationName != null)
                    station_name = owningSD.StationName;
                if (owningSD.Owners.Contains(privilegedName))
                {
                    auth = true;
                }
                else
                {
                    if (TryComp<CrewRecordsComponent>(iStation, out var owningCrew) && owningCrew != null)
                    {
                        if (owningCrew.TryGetRecord(privilegedName, out var crewRecord) && crewRecord != null)
                        {
                            if (TryComp<CrewAssignmentsComponent>(iStation, out var crewAssignments) && crewAssignments != null)
                            {
                                if (crewAssignments.TryGetAssignment(crewRecord.AssignmentID, out var crewAssignment) && crewAssignment != null)
                                {
                                    if (crewAssignment.CanClaim)
                                    {
                                        auth = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            if (auth)
            {
                possibleStations.Add(stationID, station_name);
            }

        }

        var targetLabel = Loc.GetString("access-overrider-window-no-target");
        var targetLabelColor = Color.Red;
        bool allowed = false;
        string? taggedFaction = null;
        int taggedStationUID = 0;
        if (component.TargetAccessReaderId is { Valid: true } accessReader)
        {
            if (!_accessReader.GetMainAccessReader(component.TargetAccessReaderId, out var accessReaderEnt2))
                return;
            if (accessReaderEnt2 == null) return;

            if (TryComp<StationTrackerComponent>(accessReaderEnt2.Value.Owner, out var stationTracker) && stationTracker != null)
            {
                var taggedStation = stationTracker.Station;
                if (TryComp<StationDataComponent>(taggedStation, out var taggedSD) && taggedSD != null)
                {
                    taggedFaction = taggedSD.StationName;
                    taggedStationUID = taggedSD.UID;
                }
            }
            targetLabel = Loc.GetString("access-overrider-window-target-label") + " " + Comp<MetaDataComponent>(component.TargetAccessReaderId).EntityName;
            targetLabelColor = Color.White;
            allowed = PrivilegedIdIsAuthorized(uid, accessReaderEnt2.Value.Owner, component);

        }
        StationTaggerBoundUserInterfaceState newState;

        newState = new StationTaggerBoundUserInterfaceState(idPresent, allowed, privilegedIdName, targetLabel, targetLabelColor, taggedFaction, possibleStations, component.ConnectedStation, taggedStationUID);

        _userInterface.SetUiState(uid, StationTaggerUiKey.Key, newState);

    }


    private bool PrivilegedIdIsAuthorized(EntityUid uid, EntityUid? readerComponent, StationTaggerComponent? component = null)
    {
        if (!Resolve(uid, ref component) || readerComponent == null)
            return true;

        var privilegedId = component.PrivilegedIdSlot.Item;
        return privilegedId != null && _accessReader.IsAllowed(privilegedId.Value, readerComponent.Value);
    }

    private void UpdateUserInterface(EntityUid uid, GridControlConsoleComponent component, EntityEventArgs args)
    {
        if (!component.Initialized)
            return;

        GridControlConsoleBoundUserInterfaceState newState;

        newState = new GridControlConsoleBoundUserInterfaceState(component.Active);

        _userInterface.SetUiState(uid, GridControlConsoleUiKey.Key, newState);
    }

    private void OnGridControlOn(EntityUid uid, GridControlConsoleComponent component, GridControlOn args)
    {
        if (!component.Initialized)
            return;
        if (!_accessReader.IsAllowed(args.Actor, uid)) return;
        component.Active = true;
        UpdateUserInterface(uid, component, args);
    }
    private void OnGridControlOff(EntityUid uid, GridControlConsoleComponent component, GridControlOff args)
    {
        if (!component.Initialized)
            return;
        if (!_accessReader.IsAllowed(args.Actor, uid)) return;
        component.Active = false;
        UpdateUserInterface(uid, component, args);
    }

    public bool IsGridControlled(EntityUid entityUid)
    {
        var query = _entManager.AllEntityQueryEnumerator<GridControlConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Active)
            {
                var targetGrid = _transform.GetGrid(uid);
                if (targetGrid == entityUid)
                {
                    return true;
                }
            }
        }

        return false;
    }

}
