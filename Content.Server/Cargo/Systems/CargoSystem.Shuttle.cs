using Content.Server.Cargo.Components;
using Content.Server.Hands.Systems;
using Content.Shared.Cargo;
using Content.Shared.Cargo.BUI;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Events;
using Content.Shared.CCVar;
using Content.Shared.Coordinates;
using Content.Shared.Invoices.Components;
using Content.Shared.Stacks;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using System.Linq;

namespace Content.Server.Cargo.Systems;

public sealed partial class CargoSystem
{
    private const string CreditProtoID = "Credit";

    /*
* Handles cargo shuttle / trade mechanics.
*/

    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    private static readonly SoundPathSpecifier ApproveSound = new("/Audio/Effects/Cargo/ping.ogg");
    private bool _lockboxCutEnabled;

    private void InitializeShuttle()
    {
        SubscribeLocalEvent<TradeStationComponent, GridSplitEvent>(OnTradeSplit);

        SubscribeLocalEvent<CargoPalletConsoleComponent, CargoPalletStationSelectMessage>(OnStationSelect);
        SubscribeLocalEvent<CargoPalletConsoleComponent, CargoPalletSellMessage>(OnPalletSale);
        SubscribeLocalEvent<CargoPalletConsoleComponent, CargoPalletAppraiseMessage>(OnPalletAppraise);
        SubscribeLocalEvent<CargoPalletConsoleComponent, CargoPalletChangeMoneyMode>(OnChangeMoneyMode);
        SubscribeLocalEvent<CargoPalletConsoleComponent, BoundUIOpenedEvent>(OnPalletUIOpen);

        _cfg.OnValueChanged(CCVars.LockboxCutEnabled, (enabled) => { _lockboxCutEnabled = enabled; }, true);
    }

    #region Console
    private void UpdatePalletConsoleInterface(EntityUid uid, CargoPalletConsoleComponent comp, EntityUid? player)
    {
        List<EntityUid> possibleStations = new();
        Dictionary<int, string> formattedStations = new();
        string selectedName = "No Faction Selected";
        var selectedStation = _station.GetStationByID(comp.SelectedStation);
        if (selectedStation != null)
        {
            if (TryComp<StationDataComponent>(selectedStation, out var sD) && sD != null)
            {
                if (sD.StationName != null) selectedName = sD.StationName;
            }
        }
        int taxingStation = 0;
        var name = "Unknown";
        if (player != null)
        {
            possibleStations = _station.GetStationsAvailableTo(Name(player.Value));
        }
        foreach (var station in possibleStations)
        {
            if (TryComp<StationDataComponent>(station, out var data) && data != null)
            {
                if (data.StationName != null)
                {
                    formattedStations.Add(data.UID, data.StationName);
                }
            }
        }
        if (Transform(uid).GridUid is not { } gridUid)
        {
            _uiSystem.SetUiState(uid,
                CargoPalletConsoleUiKey.Sale,
                new CargoPalletConsoleInterfaceState(0, 0, false, comp.CashMode, 0, 0, name, formattedStations, comp.SelectedStation, selectedName));
            return;
        }
        if (!TryComp<TradeStationComponent>(gridUid, out var tS) || tS == null)
        {
            _uiSystem.SetUiState(uid,
                CargoPalletConsoleUiKey.Sale,
                new CargoPalletConsoleInterfaceState(0, 0, false, comp.CashMode, 0, 0, name, formattedStations, comp.SelectedStation, selectedName));
            return;
        }
        var tax = 25;
        var owningStation = _station.GetOwningStation(uid, null, true);
        if (owningStation != null)
        {
            if (TryComp<StationDataComponent>(owningStation, out var sD) && sD != null)
            {
                tax = sD.ExportTax;
                taxingStation = sD.UID;
                if (sD.StationName != null)
                {
                    name = sD.StationName;
                }
            }
        }
        GetPalletGoods(gridUid, out var toSell, out var goods);
        var totalAmount = goods.Sum(t => t.Item3);


        _uiSystem.SetUiState(uid,
            CargoPalletConsoleUiKey.Sale,
            new CargoPalletConsoleInterfaceState((int)totalAmount, toSell.Count, true, comp.CashMode, tax, taxingStation, name, formattedStations, comp.SelectedStation, selectedName));
    }

    private void OnPalletUIOpen(EntityUid uid, CargoPalletConsoleComponent component, BoundUIOpenedEvent args)
    {
        UpdatePalletConsoleInterface(uid, component, args.Actor);
    }

    /// <summary>
    /// Ok so this is just the same thing as opening the UI, its a refresh button.
    /// I know this would probably feel better if it were like predicted and dynamic as pallet contents change
    /// However.
    /// I dont want it to explode if cargo uses a conveyor to move 8000 pineapple slices or whatever, they are
    /// known for their entity spam i wouldnt put it past them
    /// </summary>

    private void OnPalletAppraise(EntityUid uid, CargoPalletConsoleComponent component, CargoPalletAppraiseMessage args)
    {
        UpdatePalletConsoleInterface(uid, component, args.Actor);
    }

    private void OnChangeMoneyMode(EntityUid uid, CargoPalletConsoleComponent component, CargoPalletChangeMoneyMode args)
    {
        if (component.CashMode == CargoSaleMode.Cash)
        {
            component.CashMode = CargoSaleMode.Deposit;
        }
        else if (component.CashMode == CargoSaleMode.Deposit)
        {
            component.CashMode = CargoSaleMode.Payslip;
        }
        else if (component.CashMode == CargoSaleMode.Payslip)
        {
            component.CashMode = CargoSaleMode.Cash;
        }
        UpdatePalletConsoleInterface(uid, component, args.Actor);
    }


    #endregion

    private void OnTradeSplit(EntityUid uid, TradeStationComponent component, ref GridSplitEvent args)
    {
        return;
        // If the trade station gets bombed it's still a trade station.
        foreach (var gridUid in args.NewGrids)
        {
            EnsureComp<TradeStationComponent>(gridUid);
        }
    }

    #region Shuttle
    /// GetCargoPallets(gridUid, BuySellType.Sell) to return only Sell pads
    /// GetCargoPallets(gridUid, BuySellType.Buy) to return only Buy pads
    private List<(EntityUid Entity, CargoPalletComponent Component, TransformComponent PalletXform)> GetCargoPallets(EntityUid gridUid, BuySellType requestType = BuySellType.All)
    {
        _pads.Clear();

        var query = AllEntityQuery<CargoPalletComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var comp, out var compXform))
        {
            if (compXform.ParentUid != gridUid ||
                !compXform.Anchored)
            {
                continue;
            }

            if ((requestType & comp.PalletType) == 0)
            {
                continue;
            }

            _pads.Add((uid, comp, compXform));

        }

        return _pads;
    }

    private List<(EntityUid Entity, CargoPalletComponent Component, TransformComponent Transform)>
        GetFreeCargoPallets(EntityUid gridUid,
            List<(EntityUid Entity, CargoPalletComponent Component, TransformComponent Transform)> pallets)
    {
        _setEnts.Clear();

        List<(EntityUid Entity, CargoPalletComponent Component, TransformComponent Transform)> outList = new();

        foreach (var pallet in pallets)
        {
            var aabb = _lookup.GetAABBNoContainer(pallet.Entity, pallet.Transform.LocalPosition, pallet.Transform.LocalRotation);

            if (_lookup.AnyLocalEntitiesIntersecting(gridUid, aabb, LookupFlags.Dynamic))
                continue;

            outList.Add(pallet);
        }

        return outList;
    }

    #endregion

    #region Station

    private bool SellPallets(EntityUid gridUid, EntityUid? station, out HashSet<(EntityUid, OverrideSellComponent?, double)> goods)
    {
        GetPalletGoods(gridUid, out var toSell, out goods);

        if (toSell.Count == 0)
            return false;

        var ev = new EntitySoldEvent(toSell, station);
        RaiseLocalEvent(ref ev);

        foreach (var ent in toSell)
        {
            Del(ent);
        }

        return true;
    }

    private void GetPalletGoods(EntityUid gridUid, out HashSet<EntityUid> toSell, out HashSet<(EntityUid, OverrideSellComponent?, double)> goods)
    {
        goods = new HashSet<(EntityUid, OverrideSellComponent?, double)>();
        toSell = new HashSet<EntityUid>();

        foreach (var (palletUid, _, _) in GetCargoPallets(gridUid, BuySellType.Sell))
        {
            // Containers should already get the sell price of their children so can skip those.
            _setEnts.Clear();

            _lookup.GetEntitiesIntersecting(
                palletUid,
                _setEnts,
                LookupFlags.Dynamic | LookupFlags.Sundries);

            foreach (var ent in _setEnts)
            {
                // Dont sell:
                // - anything already being sold
                // - anything anchored (e.g. light fixtures)
                // - anything blacklisted (e.g. players).
                if (toSell.Contains(ent) ||
                    _xformQuery.TryGetComponent(ent, out var xform) &&
                    (xform.Anchored || !CanSell(ent, xform)))
                {
                    continue;
                }

                if (_blacklistQuery.HasComponent(ent))
                    continue;

                var price = _pricing.GetPrice(ent);
                if (price == 0)
                    continue;
                toSell.Add(ent);
                goods.Add((ent, CompOrNull<OverrideSellComponent>(ent), price));
            }
        }
    }

    private bool CanSell(EntityUid uid, TransformComponent xform)
    {
        if (_mobQuery.HasComponent(uid))
            return false;

        if (IsBountyComplete(uid))
            return true;

        if (_pricing.GetPrice(uid) > 0)
            return true;

        return false;
    }

    private void OnStationSelect(EntityUid uid, CargoPalletConsoleComponent component, CargoPalletStationSelectMessage args)
    {
        component.SelectedStation = args.Target;
        UpdatePalletConsoleInterface(uid, component, args.Actor);
    }

    private int GetTaxRate(EntityUid uid, CargoPalletConsoleComponent component, bool personal = false)
    {
        if (_station.GetOwningStation(uid, null, true) is not { } station ||
            !TryComp<StationBankAccountComponent>(station, out var bankAccount))
        {
            return 25;
        }
        TryComp<StationDataComponent>(station, out var sD);
        if (sD == null)
        {
            return 25;
        }
        if (!personal && sD.UID == component.SelectedStation)
        {
            return 0;
        }
        return sD.ExportTax;
    }
    private void OnPalletSale(EntityUid uid, CargoPalletConsoleComponent component, CargoPalletSellMessage args)
    {
        var xform = Transform(uid);

        var station = _station.GetStationByID(component.SelectedStation);
        var taxingStation = _station.GetOwningStation(uid, null, true);

        if (xform.GridUid is not { } gridUid)
        {
            UpdatePalletConsoleInterface(uid, component, args.Actor);
            return;
        }
        if (component.CashMode == CargoSaleMode.Deposit && station == null) return;

        if (!SellPallets(gridUid, station, out var goods))
            return;
        if (component.CashMode == CargoSaleMode.Cash)
        {
            var tax = GetTaxRate(uid, component);
            var player = args.Actor;
            //spawn the cash stack of whatever cash type the ATM is configured to.
            double total = 0;
            foreach (var (_, sellComponent, value) in goods)
            {
                total += value;
            }
            float taxmult = (float)tax / 100f;
            var taxpaid = (float)total * taxmult;
            var taxPaidInt = (int)Math.Round(taxpaid);
            total -= taxPaidInt;

            var stackPrototype = _protoMan.Index<StackPrototype>(CreditProtoID);
            var cashStack = _stack.SpawnAtPosition((int)Math.Round(total), stackPrototype, player.ToCoordinates());
            if (!_hands.TryPickupAnyHand(player, cashStack))
                _transform.SetLocalRotation(cashStack, Angle.Zero); // Orient these to grid north instead of map north
            if (taxPaidInt > 0)
            {
                if (taxingStation != null)
                {
                    if (TryComp<StationBankAccountComponent>(taxingStation, out var taxBankAccount) && taxBankAccount != null)
                    {
                        UpdateBankAccount((taxingStation.Value, taxBankAccount), taxPaidInt, "Cargo");
                    }
                }
            }
        }
        else if (component.CashMode == CargoSaleMode.Payslip)
        {
            var tax = GetTaxRate(uid, component);
            var player = args.Actor;
            //spawn the cash stack of whatever cash type the ATM is configured to.
            double total = 0;
            foreach (var (_, sellComponent, value) in goods)
            {
                total += value;
            }
            float taxmult = (float)tax / 100f;
            var taxpaid = (float)total * taxmult;
            var taxPaidInt = (int)Math.Round(taxpaid);
            total -= taxPaidInt;
            var invoice = _entityManager.SpawnAtPosition("Invoice", player.ToCoordinates());
            if (!_hands.TryPickupAnyHand(player, invoice))
                _transform.SetLocalRotation(invoice, Angle.Zero); // Orient these to grid north instead of map north
            if (TryComp<InvoiceComponent>(invoice, out var invoiceComp) && invoiceComp != null)
            {
                invoiceComp.TargetStation = 0;
                invoiceComp.InvoiceCost = (int)Math.Round(total);
                invoiceComp.InvoiceReason = $"Paid for exporting cargo.";
                invoiceComp.PayslipMode = true;
                _metaSystem.SetEntityName(invoice, $"payslip ${(int)Math.Round(total)} Cargo Export");
                Dirty(invoice, invoiceComp);
            }



            if (taxPaidInt > 0)
            {
                if (taxingStation != null)
                {
                    if (TryComp<StationBankAccountComponent>(taxingStation, out var taxBankAccount) && taxBankAccount != null)
                    {
                        UpdateBankAccount((taxingStation.Value, taxBankAccount), taxPaidInt, "Cargo");
                    }
                }
            }
        }
        else
        {
            if (station == null) return;
            var tax = GetTaxRate(uid, component);
            var player = args.Actor;
            //spawn the cash stack of whatever cash type the ATM is configured to.
            double total = 0;
            foreach (var (_, sellComponent, value) in goods)
            {
                total += value;
            }
            float taxmult = (float)tax / 100f;
            var taxpaid = (float)total * taxmult;
            var taxPaidInt = (int)Math.Round(taxpaid);
            total -= taxPaidInt;
            UpdateBankAccount(station.Value, (int)total, "Cargo");
            if (taxPaidInt > 0)
            {
                if (taxingStation != null)
                {
                    if (TryComp<StationBankAccountComponent>(taxingStation, out var taxBankAccount) && taxBankAccount != null)
                    {
                        UpdateBankAccount((taxingStation.Value, taxBankAccount), taxPaidInt, "Cargo");
                    }
                }
            }
        }

        _audio.PlayPvs(ApproveSound, uid);
        UpdatePalletConsoleInterface(uid, component, args.Actor);
    }

    #endregion
}

/// <summary>
/// Event broadcast raised by-ref before it is sold and
/// deleted but after the price has been calculated.
/// </summary>
[ByRefEvent]
public readonly record struct EntitySoldEvent(HashSet<EntityUid> Sold, EntityUid? Station);
