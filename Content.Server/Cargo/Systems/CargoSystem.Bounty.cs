using Content.Server.Cargo.Components;
using Content.Server.CrewAssignments.Systems;
using Content.Server.NameIdentifier;
using Content.Shared._Persistence14.Cargo;
using Content.Shared.Access.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.NameIdentifier;
using Content.Shared.Paper;
using Content.Shared.Stacks;
using Content.Shared.Station.Components;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Xml.Linq;

namespace Content.Server.Cargo.Systems;

public sealed partial class CargoSystem
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly NameIdentifierSystem _nameIdentifier = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSys = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _sharedSolutionContainer = default!;
    [Dependency] private readonly JobNetSystem _jobNet = default!;
    private static readonly ProtoId<NameIdentifierGroupPrototype> BountyNameIdentifierGroup = "Bounty";

    private EntityQuery<StackComponent> _stackQuery;
    private EntityQuery<ContainerManagerComponent> _containerQuery;
    private EntityQuery<CargoBountyLabelComponent> _bountyLabelQuery;

    private void InitializeBounty()
    {
        SubscribeLocalEvent<CargoBountyConsoleComponent, BoundUIOpenedEvent>(OnBountyConsoleOpened);
        SubscribeLocalEvent<CargoBountyConsoleComponent, BountyPrintLabelMessage>(OnPrintLabelMessage);
        SubscribeLocalEvent<CargoBountyConsoleComponent, BountySkipMessage>(OnSkipBountyMessage);
        SubscribeLocalEvent<CargoBountyConsoleComponent, CargoConsoleSelectTradeMessage>(OnSelectTrade);
        SubscribeLocalEvent<CargoBountyLabelComponent, PriceCalculationEvent>(OnGetBountyPrice);
        SubscribeLocalEvent<EntitySoldEvent>(OnSold);
        SubscribeLocalEvent<StationCargoBountyDatabaseComponent, MapInitEvent>(OnMapInit);

        _stackQuery = GetEntityQuery<StackComponent>();
        _containerQuery = GetEntityQuery<ContainerManagerComponent>();
        _bountyLabelQuery = GetEntityQuery<CargoBountyLabelComponent>();
    }

    public int GetSectorDevelopment()
    {
        int total = 0;
        var stations = new List<EntityUid>();
        var query = EntityQueryEnumerator<TradeStationComponent>();
        while (query.MoveNext(out var ent, out var comp))
        {
            var level = GetTradeStationLevel(ent, comp);
            total += level?.Level ?? 0;
        }

        return total;
    }
    private InfrastructureLevelPrototype? GetTradeStationLevel(EntityUid uid, TradeStationComponent tradeStation)
    {
        InfrastructureLevelPrototype? foundLevel = null;
        foreach (var level in tradeStation.Levels)
        {
            _protoMan.Resolve(level, out var levelProto);
            if (levelProto == null) continue;
            if (levelProto.RequiredXP <= tradeStation.ExperiencePoints)
            {
                foundLevel = levelProto;
            }
        }
        return foundLevel;
    }

    private void CompleteDealerObjective(EntityUid ui)
    {

    }
    private void UiUpdate(EntityUid uid, CargoBountyConsoleComponent component)
    {
        Dictionary<int, string> possibleTrades = new();
        GetAllTradeStations(ref possibleTrades, null, out _);
        if (component.SelectedTradeGrid != 0)
        {
            var trade = GetTradeStationByID(component.SelectedTradeGrid);
            if (trade != null)
            {
                if (TryComp<StationCargoBountyDatabaseComponent>(trade, out var bountyDb) && bountyDb != null)
                {
                    var untilNextSkip2 = bountyDb.NextResetTime - Timing.CurTime;
                    Dictionary<ProtoId<CargoBountyGroupPrototype>, List<CargoBountyData>> sortedBounties = new();
                    int exp = 0;
                    int dexp = 0;
                    int rexp = 0;
                    InfrastructureLevelPrototype? foundLevel = null;
                    var station = _station.GetOwningStation(trade);
                    string iLevelTitle = "Unclaimed Outpost";
                    string owner = "None";
                    int tax = 25;
                    if (station != null && TryComp<TradeStationComponent>(trade, out var tradeStation) && tradeStation != null)
                    {
                        if (TryComp<StationDataComponent>(station, out var sD) && sD != null)
                        {
                            if (sD.StationName != null)
                            {
                                owner = sD.StationName;
                            }
                            tax = sD.ExportTax;
                        }
                        exp = tradeStation.ExperiencePoints;
                        bool foundLast = false;
                        foreach (var level in tradeStation.Levels)
                        {
                            _protoMan.Resolve(level, out var levelProto);
                            if (levelProto == null) continue;
                            if (levelProto.RequiredXP <= tradeStation.ExperiencePoints)
                            {
                                dexp = levelProto.DemotionXP;
                                foundLast = true;
                                foundLevel = levelProto;
                                iLevelTitle = levelProto.Name;
                            }
                            else if (foundLast)
                            {
                                foundLast = false;
                                rexp = levelProto.RequiredXP;
                            }
                        }
                        if (foundLast == true)
                        {
                            rexp = dexp + 500;
                        }
                    }
                    foreach (var bounty in bountyDb.Bounties)
                    {
                        _protoMan.Resolve(bounty.Bounty, out var bountyProto);
                        if (bountyProto == null) continue;
                        _protoMan.Resolve(bountyProto.Group, out var bountyGroup);
                        if (bountyGroup == null) continue;
                        if (sortedBounties.ContainsKey(bountyProto.Group))
                        {
                            sortedBounties.TryGetValue(bountyProto.Group, out var cargoBountyDatas);
                            if (cargoBountyDatas != null)
                            {
                                cargoBountyDatas.Add(bounty);
                            }
                        }
                        else
                        {
                            sortedBounties.Add(bountyProto.Group, new List<CargoBountyData> { bounty });
                        }

                    }

                    _uiSystem.SetUiState(uid, CargoConsoleUiKey.Bounty, new CargoBountyConsoleState(sortedBounties, bountyDb.History, untilNextSkip2, possibleTrades, component.SelectedTradeGrid, tax, exp, dexp, rexp, iLevelTitle, owner));
                    return;
                }
            }
        }
        Dictionary<ProtoId<CargoBountyGroupPrototype>, List<CargoBountyData>> emptyBounty = new();
        List<CargoBountyHistoryData> emptyHistory = new();
        _uiSystem.SetUiState(uid, CargoConsoleUiKey.Bounty, new CargoBountyConsoleState(emptyBounty, emptyHistory, TimeSpan.Zero, possibleTrades, component.SelectedTradeGrid, 25, 0, 0, 0, "Unconnected", "Unconnected"));
    }

    private void OnBountyConsoleOpened(EntityUid uid, CargoBountyConsoleComponent component, BoundUIOpenedEvent args)
    {
        UiUpdate(uid, component);
    }

    private void OnPrintLabelMessage(EntityUid uid, CargoBountyConsoleComponent component, BountyPrintLabelMessage args)
    {
        if (Timing.CurTime < component.NextPrintTime)
            return;

        var station = GetTradeStationByID(component.SelectedTradeGrid);
        if (station == null) return;
        if (!TryGetBountyFromId(station.Value, args.BountyId, out var bounty))
            return;

        var label = Spawn(component.BountyLabelId, Transform(uid).Coordinates);
        component.NextPrintTime = Timing.CurTime + component.PrintDelay;
        SetupBountyLabel(label, station.Value, bounty);
        _audio.PlayPvs(component.PrintSound, uid);
    }

    private void OnSkipBountyMessage(EntityUid uid, CargoBountyConsoleComponent component, BountySkipMessage args)
    {
        if (GetTradeStationByID(component.SelectedTradeGrid) is not { } station || !TryComp<StationCargoBountyDatabaseComponent>(station, out var db))
            return;

        if (Timing.CurTime < db.NextSkipTime)
            return;

        if (!TryGetBountyFromId(station, args.BountyId, out var bounty))
            return;

        if (args.Actor is not { Valid: true } mob)
            return;

        if (TryComp<AccessReaderComponent>(uid, out var accessReaderComponent) &&
            !_accessReaderSystem.IsAllowed(mob, uid, accessReaderComponent))
        {
            if (Timing.CurTime >= component.NextDenySoundTime)
            {
                component.NextDenySoundTime = Timing.CurTime + component.DenySoundDelay;
                _audio.PlayPvs(component.DenySound, uid);
            }
            return;
        }

        if (!TryRemoveBounty(station, bounty, true, args.Actor))
            return;

        db.NextSkipTime = Timing.CurTime + db.SkipDelay;
        UiUpdate(uid, component);
        _audio.PlayPvs(component.SkipSound, uid);
    }

    public void SetupBountyLabel(EntityUid uid, EntityUid stationId, CargoBountyData bounty, PaperComponent? paper = null, CargoBountyLabelComponent? label = null)
    {
        if (!Resolve(uid, ref paper, ref label) || !_protoMan.Resolve<CargoBountyPrototype>(bounty.Bounty, out var prototype))
            return;

        label.Id = bounty.Id;
        label.AssociatedStationId = stationId;
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("bounty-manifest-header", ("id", bounty.Id)));
        msg.PushNewline();
        msg.AddMarkupOrThrow(Loc.GetString("bounty-manifest-list-start"));
        msg.PushNewline();

        foreach (var entry in prototype.Condition.GetManifestEntry(EntityManager, _protoMan))
        {
            msg.AddMarkupOrThrow($"- {entry}");
            msg.PushNewline();
        }

        msg.AddMarkupOrThrow(Loc.GetString("bounty-console-manifest-reward", ("reward", prototype.Reward)));
        msg.PushNewline();
        msg.AddText($"For Trade Station: {Name(stationId)}");
        _paperSystem.SetContent((uid, paper), msg.ToMarkup());
    }

    /// <summary>
    /// Bounties do not sell for any currency. The reward for a bounty is
    /// calculated after it is sold separately from the selling system.
    /// </summary>
    private void OnGetBountyPrice(EntityUid uid, CargoBountyLabelComponent component, ref PriceCalculationEvent args)
    {
        if (args.Handled || component.Calculating)
            return;

        // make sure this label was actually applied to a crate.
        if (!_container.TryGetContainingContainer((uid, null, null), out var container) || container.ID != LabelSystem.ContainerName)
            return;

        if (component.AssociatedStationId is not { } station || !TryComp<StationCargoBountyDatabaseComponent>(station, out var database))
            return;
        if (Transform(uid).GridUid is not { } gridUid)
        {
            return;
        }
        if (gridUid != component.AssociatedStationId)
        {
            return;
        }
        if (database.CheckedBounties.Contains(component.Id))
            return;
        CargoBountyData? bounty;
        if (component.DealerName != null)
        {
            var jobNet = _jobNet.GetJobNetByName(component.DealerName);
            if (jobNet == null)
            {
                return;
            }
            bounty = jobNet.DealerBounty;
        }

        else if (!TryGetBountyFromId(station, component.Id, out bounty, database))
            return;
        if (bounty == null) return;
        if (!_protoMan.Resolve(bounty.Bounty, out var bountyPrototype))
            return;

        var result = bountyPrototype.Condition.CheckCondition(container.Owner, EntityManager);

        args.Handled = true;
        component.Calculating = true;
        args.Price = Math.Max(bountyPrototype.Reward * Math.Min(result, 1 - bounty.PercentCompleted) - _pricing.GetPrice(container.Owner), 0f);
        component.Calculating = false;
    }

    public FixedPoint2 GetContainerAmount(EntityUid uid, ProtoId<ReagentPrototype> chem)
    {
        FixedPoint2 total = 0;

        if (!TryComp<ContainerManagerComponent>(uid, out var containers))
            return total;

        foreach (var container in containers.Containers.Values)
        {
            foreach (var ent in container.ContainedEntities)
            {
                total += _sharedSolutionContainer.GetTotalPrototypeQuantity(ent, chem);
            }
        }
        return total;
    }
    public void CompleteBounty(EntityUid station, CargoBountyData bounty, EntityUid? actor, string? dealerName)
    {
        if (!_protoMan.Resolve(bounty.Bounty, out var proto))
            return;

        if (dealerName != null)
        {

            var jobNet = _jobNet.GetJobNetByName(dealerName);
            if (jobNet == null)
            {
                return;
            }

            // TODO: Get rid of this component.Owner call... somehow...
            _jobNet.CompleteDealerBounty(jobNet.Owner, jobNet);
        }

        if (TryComp<TradeStationComponent>(station, out var tradeStation))
        {
            tradeStation.ExperiencePoints += proto.SuccessXP;
            if (tradeStation.ExperiencePoints < 0)
                tradeStation.ExperiencePoints = 0;
        }

        TryRemoveBounty(station, bounty, false, actor);
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"Bounty \"{bounty.Bounty}\" (id:{bounty.Id}) was completed");
    }

    private void OnSold(ref EntitySoldEvent args)
    {
        foreach (var sold in args.Sold)
        {
            if (!TryGetBountyLabel(sold, out _, out var component) ||
                component.AssociatedStationId is not { } station ||
                !TryGetBounty(sold, component, out var bounty) ||
                !_protoMan.Resolve(bounty.Bounty, out var bountyProto))
                continue;

            var result = bountyProto.Condition.CheckCondition(sold, EntityManager);
            bounty.PercentCompleted += result;
            if (bounty.PercentCompleted >= 1f)
                CompleteBounty(station, bounty, args.Station, component.DealerName);

            _adminLogger.Add(LogType.Action, LogImpact.Low, $"Bounty \"{bounty.Bounty}\" (id: {bounty.Id}) was submitted (%{bounty.PercentCompleted:P2} complete)");
        }
    }

    private bool TryGetBountyLabel(EntityUid uid,
        [NotNullWhen(true)] out EntityUid? labelEnt,
        [NotNullWhen(true)] out CargoBountyLabelComponent? labelComp)
    {
        labelEnt = null;
        labelComp = null;
        if (!_containerQuery.TryGetComponent(uid, out var containerMan))
            return false;

        // make sure this label was actually applied to a crate.
        if (!_container.TryGetContainer(uid, LabelSystem.ContainerName, out var container, containerMan))
            return false;

        if (container.ContainedEntities.FirstOrNull() is not { } label ||
            !_bountyLabelQuery.TryGetComponent(label, out var component))
            return false;

        labelEnt = label;
        labelComp = component;
        return true;
    }

    private void OnMapInit(EntityUid uid, StationCargoBountyDatabaseComponent component, MapInitEvent args)
    {
        FillBountyDatabase(uid, component);
    }

    /// <summary>
    /// Fills up the bounty database with random bounties.
    /// </summary>
    public void FillBountyDatabase(EntityUid uid, StationCargoBountyDatabaseComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        Dictionary<ProtoId<CargoBountyGroupPrototype>, int> groups = component.Groups;
        var station = _station.GetOwningStation(uid);
        if (station != null && TryComp<TradeStationComponent>(uid, out var tradeStation) && tradeStation != null)
        {
            InfrastructureLevelPrototype? proto = GetTradeStationLevel(uid, tradeStation);
            if (proto != null)
            {
                groups = proto.Groups;
            }
        }
        foreach (var kv in groups)
        {
            var proto = kv.Key;
            var count = kv.Value;
            _protoMan.Resolve(proto, out var prototype);
            if (prototype == null) continue;
            for (var i = 0; i < count; i++)
            {
                if (!TryAddBounty(uid, component, kv.Key))
                    break;
            }
        }
        component.NextResetTime = Timing.CurTime + component.ResetDelay;
        UpdateBountyConsoles();
    }

    public void RerollBountyDatabase(Entity<StationCargoBountyDatabaseComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp))
            return;

        entity.Comp.Bounties.Clear();
        FillBountyDatabase(entity);
    }

    #region Bounty Complete API Options
    public bool IsBountyComplete(EntityUid containerUid, CargoBountyPrototype prototype, bool absolute = false)
    {
        var result = prototype.Condition.CheckCondition(containerUid, EntityManager);

        if (absolute)
            return result >= 1f;
        return result > 0f;
    }

    public bool IsBountyComplete(EntityUid containerUid, ProtoId<CargoBountyPrototype> prototype, bool absolute = false)
        => IsBountyComplete(containerUid, _protoMan.Index(prototype), absolute);

    public bool IsBountyComplete(EntityUid containerUid, CargoBountyData bountyData, bool absolute = false)
        => IsBountyComplete(containerUid, bountyData.Bounty, absolute);

    public bool IsBountyComplete(EntityUid containerUid, bool absolute = false)
    {
        if (!TryGetBountyLabel(containerUid, out var lableUid, out var labelComp) ||
            labelComp.AssociatedStationId is not { } station ||
            !TryGetBounty(station, labelComp, out var bounty))
            return false;

        return IsBountyComplete(containerUid, bounty, absolute);
    }
    #endregion

    [PublicAPI]
    public bool TryAddBounty(EntityUid uid, StationCargoBountyDatabaseComponent? component = null, ProtoId<CargoBountyGroupPrototype>? group = null)
    {
        if (!Resolve(uid, ref component))
            return false;
        if (group == null)
        {
            group = component.Group;
        }
        // todo: consider making the cargo bounties weighted.
        var allBounties = _protoMan.EnumeratePrototypes<CargoBountyPrototype>()
            .Where(p => p.Group == group.Value)
            .ToList();
        var filteredBounties = new List<CargoBountyPrototype>();
        foreach (var proto in allBounties)
        {
            if (component.Bounties.Any(b => b.Bounty == proto.ID))
                continue;
            filteredBounties.Add(proto);
        }

        var pool = filteredBounties.Count == 0 ? allBounties : filteredBounties;
        if (pool.Count < 1) return false;
        var bounty = _random.Pick(pool);
        return TryAddBounty(uid, bounty, component);
    }

    [PublicAPI]
    public bool TryAddBounty(EntityUid uid, string bountyId, StationCargoBountyDatabaseComponent? component = null)
    {
        if (!_protoMan.TryIndex<CargoBountyPrototype>(bountyId, out var bounty))
        {
            return false;
        }

        return TryAddBounty(uid, bounty, component);
    }

    public bool TryAddBounty(EntityUid uid, CargoBountyPrototype bounty, StationCargoBountyDatabaseComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        //       if (component.Bounties.Count >= component.MaxBounties)
        //           return false;

        _nameIdentifier.GenerateUniqueName(uid, BountyNameIdentifierGroup, out var randomVal);
        var newBounty = new CargoBountyData(bounty, randomVal);
        // This bounty id already exists! Probably because NameIdentifierSystem ran out of ids.
        if (component.Bounties.Any(b => b.Id == newBounty.Id))
        {
            Log.Error("Failed to add bounty {ID} because another one with the same ID already existed!", newBounty.Id);
            return false;
        }
        component.Bounties.Add(new CargoBountyData(bounty, randomVal));
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"Added bounty \"{bounty.ID}\" (id:{component.TotalBounties}) to station {ToPrettyString(uid)}");
        component.TotalBounties++;
        return true;
    }

    [PublicAPI]
    public bool TryRemoveBounty(Entity<StationCargoBountyDatabaseComponent?> ent,
        string dataId,
        bool skipped,
        EntityUid? actor = null)
    {
        if (!TryGetBountyFromId(ent.Owner, dataId, out var data, ent.Comp))
            return false;

        return TryRemoveBounty(ent, data, skipped, actor);
    }

    public bool TryRemoveBounty(Entity<StationCargoBountyDatabaseComponent?> ent,
        CargoBountyData data,
        bool skipped,
        EntityUid? actor = null)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        for (var i = 0; i < ent.Comp.Bounties.Count; i++)
        {
            if (ent.Comp.Bounties[i].Id == data.Id)
            {
                string? actorName = null;
                if (actor != null)
                {
                    var getIdentityEvent = new TryGetIdentityShortInfoEvent(ent.Owner, actor.Value);
                    RaiseLocalEvent(getIdentityEvent);
                    actorName = getIdentityEvent.Title;
                }

                ent.Comp.History.Add(new CargoBountyHistoryData(data,
                    skipped
                        ? CargoBountyHistoryData.BountyResult.Skipped
                        : CargoBountyHistoryData.BountyResult.Completed,
                    Timing.CurTime,
                    actorName));
                ent.Comp.Bounties.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public bool TryGetBounty(EntityUid uid, CargoBountyLabelComponent label, out CargoBountyData bounty)
    {
        bounty = default!;
        if (TryGetBountyFromJobNet(uid, label, out bounty))
            return true;

        if (label.AssociatedStationId is not { } station)
            return false;

        if (TryGetBountyFromId(station, label.Id, out var nullable))
        {
            bounty = nullable;
            return true;
        }
        return false;
    }

    public bool TryGetBountyFromJobNet(EntityUid uid, CargoBountyLabelComponent label, out CargoBountyData bounty)
    {
        bounty = default!;
        if (label.DealerName is not { } dealerName)
            return false;

        var jobNet = _jobNet.GetJobNetByName(dealerName);
        if (jobNet == null || jobNet.DealerBounty is not { } dealerBounty)
            return false;

        bounty = dealerBounty;
        return true;
    }

    public bool TryGetBountyFromId(
        EntityUid uid,
        string id,
        [NotNullWhen(true)] out CargoBountyData? bounty,
        StationCargoBountyDatabaseComponent? component = null)
    {
        bounty = null;
        if (!Resolve(uid, ref component))
            return false;

        foreach (var bountyData in component.Bounties)
        {
            if (bountyData.Id != id)
                continue;
            bounty = bountyData;
            break;
        }

        return bounty != null;
    }

    public void UpdateBountyConsoles()
    {
        var query = EntityQueryEnumerator<CargoBountyConsoleComponent, UserInterfaceComponent>();
        while (query.MoveNext(out var uid, out var comp, out var ui))
        {
            UiUpdate(uid, comp);
        }
    }

    private void UpdateBounty()
    {
        var query = EntityQueryEnumerator<StationCargoBountyDatabaseComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextResetTime <= Timing.CurTime)
            {
                BountyRefresh(uid, comp);
            }
            comp.CheckedBounties.Clear();
        }

    }

    private void BountyRefresh(EntityUid uid, StationCargoBountyDatabaseComponent comp)
    {
        var station = _station.GetOwningStation(uid);
        if (station != null)
        {
            TryComp<TradeStationComponent>(uid, out var tradeStation);
            if (tradeStation != null)
            {
                foreach (var bounty in comp.Bounties)
                {
                    _protoMan.Resolve(bounty.Bounty, out var bountyProto);
                    if (bountyProto == null) continue;
                    tradeStation.ExperiencePoints = Math.Max(tradeStation.ExperiencePoints - bountyProto.FailureXP, 0);
                }
                InfrastructureLevelPrototype? levelPrototype = GetTradeStationLevel(uid, tradeStation);
                if (levelPrototype != null)
                {
                    if (levelPrototype.Income > 0)
                    {
                        TryComp<StationBankAccountComponent>(station, out var owningBank);
                        UpdateBankAccount((station.Value, owningBank), levelPrototype.Income, "Cargo");
                    }
                    else if (levelPrototype.Income < 0)
                    {
                        TryComp<StationBankAccountComponent>(station, out var bank);
                        var accountBalance = GetBalanceFromAccount((station.Value, bank), "Cargo");
                        var cost = -levelPrototype.Income;
                        if (cost > accountBalance) // Not enough balance
                        {
                            tradeStation.ExperiencePoints = 0;
                            _station.RemoveGridFromStation(station.Value, uid);
                        }
                        else
                        {
                            UpdateBankAccount((station.Value, bank), levelPrototype.Income, "Cargo");
                        }
                    }
                }
            }

        }


        RerollBountyDatabase((uid, comp));
    }
}
