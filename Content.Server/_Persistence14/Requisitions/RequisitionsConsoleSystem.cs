using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Content.Server.Construction;
using Content.Server.Lathe;
using Content.Server.Materials;
using Content.Shared._Persistence14.Requisitions;
using Content.Shared.Construction.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Invoices.Components;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Materials.OreSilo;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Research.Prototypes;
using Content.Shared.SmartFridge;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Persistence14.Requisitions;

public sealed class RequisitionsConsoleSystem : SharedRequisitionsConsoleSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly LatheSystem _lathe = default!;
    [Dependency] private readonly MaterialStorageSystem _materialStorage = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly FlatpackSystem _flatpack = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    private EntityQuery<LatheComponent> _latheQuery;
    private EntityQuery<SmartFridgeComponent> _fridgeQuery;

    // While set, UpdateUi does nothing; a checkout sets it and pushes state once at the end.
    private bool _suppressUi;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RequisitionsConsoleComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<RequisitionsConsoleComponent, RequisitionCheckoutMessage>(OnCheckout);
        SubscribeLocalEvent<RequisitionsConsoleComponent, RequisitionCancelMessage>(OnCancel);
        SubscribeLocalEvent<RequisitionsConsoleComponent, RequisitionPreviewInvoiceMessage>(OnPreviewInvoice);
        SubscribeLocalEvent<RequisitionsConsoleComponent, RequisitionEjectFlatpacksMessage>(OnEjectFlatpacks);
        SubscribeLocalEvent<RequisitionsConsoleComponent, MaterialAmountChangedEvent>(OnMaterialChanged);
        SubscribeLocalEvent<RequisitionsConsoleComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<RequisitionsConsoleComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
        SubscribeLocalEvent<RequisitionsLatheJobComponent, LatheItemProducedEvent>(OnLatheItemProduced);
        // Run after the lathe's own power handler so its abort/refund has already returned materials to it.
        SubscribeLocalEvent<RequisitionsLatheJobComponent, PowerChangedEvent>(OnJobLathePowerChanged, after: new[] { typeof(LatheSystem) });
        SubscribeLocalEvent<RequisitionsLatheJobComponent, ComponentShutdown>(OnJobLatheShutdown);

        _latheQuery = GetEntityQuery<LatheComponent>();
        _fridgeQuery = GetEntityQuery<SmartFridgeComponent>();
    }

    // The whole console is access-restricted: unauthorised players can't even open it.
    private void OnOpenAttempt(Entity<RequisitionsConsoleComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || HasConfigAccess(ent, args.User))
            return;

        args.Cancel();
        if (!args.Silent)
            _popup.PopupEntity(Loc.GetString("requisitions-access-denied"), ent.Owner, args.User);
    }

    // Materials inserted/removed (e.g. a customer contributing sheets) — refresh the open UI live. This
    // reuses the cached lathe catalogue (only the cheap stock/contributed figures actually change here).
    private void OnMaterialChanged(Entity<RequisitionsConsoleComponent> ent, ref MaterialAmountChangedEvent args)
    {
        UpdateUi(ent);
    }

    // Server drops its cached lathe catalogue whenever the linked machine set changes.
    protected override void OnCatalogueSourcesChanged(Entity<RequisitionsConsoleComponent> ent)
    {
        ent.Comp.LatheCatalogueCache = null;
    }

    #region Checkout

    private void OnCheckout(Entity<RequisitionsConsoleComponent> ent, ref RequisitionCheckoutMessage args)
    {
        RefreshLinkState(ent);

        if (args.Items.Count == 0)
            return;

        // One checkout at a time: refuse while a previous order is still printing.
        if (ent.Comp.OutstandingJobs > 0)
        {
            _popup.PopupEntity(Loc.GetString("requisitions-processing"), ent.Owner, args.Actor);
            return;
        }

        // Suppress UI pushes for the duration of the checkout; push once at the end.
        _suppressUi = true;
        try
        {
        // Inserted sheets the customer is contributing: they discount the bill and physically feed the print.
        var pool = _materialStorage.GetStoredMaterials(ent.Owner, localOnly: true)
            .ToDictionary(kv => kv.Key.Id, kv => kv.Value);

        var invoiceMode = args.PrintInvoice;
        var detailedInvoice = ent.Comp.DetailedInvoice;
        var inv = new InvoiceAccum();
        var producedAny = false;
        var anyFailed = false;

        foreach (var item in args.Items)
        {
            // A single bad line must never abort the whole order.
            try
            {
                // Fridge items aren't printed — the requested units are simply ejected from the linked fridge.
                if (item.Source == RequisitionItemSource.Fridge)
                {
                    var fname = item.Id;
                    var fqty = Math.Max(1, item.Quantity);
                    var ejected = EjectFridgeItems(ent, fname, fqty);
                    if (ejected <= 0)
                    {
                        if (invoiceMode)
                            AppendInvoiceFailure(inv.Items, fname, Loc.GetString("requisitions-fail-out-of-stock"));
                        anyFailed = true;
                        continue;
                    }
                    if (ejected < fqty)
                        anyFailed = true;

                    var fprice = ent.Comp.FridgeItemPrices.GetValueOrDefault(fname, ent.Comp.FridgeFallbackPrice);
                    BillLine(ent.Comp, inv, invoiceMode, detailedInvoice, item.Id, fname,
                        RequisitionItemSource.Fridge, flatpack: false, perUnit: null, fridgeUnitPrice: fprice,
                        queued: ejected, qty: fqty, coverUsed: null);
                    producedAny = true;
                    continue;
                }

                if (!Proto.TryIndex<LatheRecipePrototype>(item.Id, out var recipe))
                {
                    if (invoiceMode)
                        AppendInvoiceFailure(inv.Items, item.Id, Loc.GetString("requisitions-fail-unknown"));
                    anyFailed = true;
                    continue;
                }

                var qty = Math.Max(1, item.Quantity);
                var flatpack = item.Flatpack && ent.Comp.FlatpackerLinked && IsFlatpackable(recipe);
                var mult = flatpack ? ent.Comp.FlatpackMaterialMultiplier : 1f;

                // Per-unit raw material need (before the customer's contribution).
                var perUnit = new Dictionary<string, int>();
                foreach (var (mat, baseAmount) in recipe.Materials)
                    perUnit[mat.Id] = (int) MathF.Ceiling(baseAmount * mult);

                // Dispatch one unit at a time and only bill for what actually queues. A machine can accept fewer
                // than requested — it runs out of materials partway, hits its per-request limit, or a researched
                // recipe has fewer prints left — and the unqueued units must never reach the invoice.
                string? failReason;
                var coverUsed = new Dictionary<string, int>();
                var queued = flatpack
                    ? TryDispatchFlatpack(ent, recipe, qty, perUnit, pool, coverUsed, out failReason)
                    : TryDispatchLathe(ent, recipe, qty, perUnit, pool, coverUsed, out failReason);

                if (queued <= 0)
                {
                    if (invoiceMode)
                        AppendInvoiceFailure(inv.Items, Lathe.GetRecipeName(recipe), failReason ?? Loc.GetString("requisitions-fail-no-materials"));
                    anyFailed = true;
                    continue;
                }

                if (queued < qty)
                    anyFailed = true; // only part of this line could be produced

                BillLine(ent.Comp, inv, invoiceMode, detailedInvoice, item.Id, Lathe.GetRecipeName(recipe),
                    RequisitionItemSource.Lathe, flatpack: flatpack, perUnit: perUnit, fridgeUnitPrice: 0,
                    queued: queued, qty: qty, coverUsed: coverUsed);
                producedAny = true;
            }
            catch (Exception e)
            {
                Log.Error($"[Requisitions] checkout of '{item.Id}' threw, continuing with the rest: {e}");
                if (invoiceMode)
                    AppendInvoiceFailure(inv.Items, item.Id, Loc.GetString("requisitions-fail-error"));
                anyFailed = true;
            }
        }

        if (!producedAny)
        {
            _popup.PopupEntity(Loc.GetString("requisitions-checkout-failed"), ent.Owner, args.Actor);
            return;
        }

        // Return any inserted sheets the order didn't consume.
        foreach (var leftover in _materialStorage.EjectAllMaterial(ent.Owner))
            _hands.PickupOrDrop(args.Actor, leftover);

        // When asked, print a payable invoice for the order — paid later via bank into the owning faction.
        // A checkout without an invoice simply prints the items at no charge.
        if (invoiceMode)
        {
            // The operator can set a manual final price; otherwise bill the computed cost of what actually printed.
            var invoiceTotal = args.OverridePrice is { } ov ? Math.Max(0, ov) : inv.RunningCost;
            var body = BuildInvoiceBody(args.InvoiceTitle, inv.Items, inv.Mats, inv.Fees, inv.MatBilled, inv.MatWorth, invoiceTotal, detailedInvoice);
            SpawnInvoice(ent, args.Actor, args.InvoiceTitle, invoiceTotal, body);
        }

        // A slotted invoice (if any) was used to place this order — eject it now. A freshly printed invoice
        // above is a separate entity and unaffected.
        EjectSlottedInvoice(ent, args.Actor);

        _popup.PopupEntity(
            Loc.GetString(anyFailed ? "requisitions-checkout-partial" : "requisitions-checkout-done"),
            ent.Owner, args.Actor);
        }
        finally
        {
            _suppressUi = false;
        }

        UpdateUi(ent, args.Actor);
    }

    // Prints the invoice this cart would produce, without dispatching any prints or ejecting anything.
    // Every requested unit is treated as producible and the customer's contributed sheets are ignored, so the
    // quote reflects the full cart. The resulting paper can be slotted back to reload the cart.
    private void OnPreviewInvoice(Entity<RequisitionsConsoleComponent> ent, ref RequisitionPreviewInvoiceMessage args)
    {
        RefreshLinkState(ent);

        if (args.Items.Count == 0)
            return;

        var detailedInvoice = ent.Comp.DetailedInvoice;
        var inv = new InvoiceAccum();
        var anyFailed = false;

        foreach (var item in args.Items)
        {
            try
            {
                if (item.Source == RequisitionItemSource.Fridge)
                {
                    var fname = item.Id;
                    var fqty = Math.Max(1, item.Quantity);
                    var fprice = ent.Comp.FridgeItemPrices.GetValueOrDefault(fname, ent.Comp.FridgeFallbackPrice);
                    BillLine(ent.Comp, inv, invoiceMode: true, detailedInvoice, item.Id, fname,
                        RequisitionItemSource.Fridge, flatpack: false, perUnit: null, fridgeUnitPrice: fprice,
                        queued: fqty, qty: fqty, coverUsed: null);
                    continue;
                }

                if (!Proto.TryIndex<LatheRecipePrototype>(item.Id, out var recipe))
                {
                    AppendInvoiceFailure(inv.Items, item.Id, Loc.GetString("requisitions-fail-unknown"));
                    anyFailed = true;
                    continue;
                }

                var qty = Math.Max(1, item.Quantity);
                var flatpack = item.Flatpack && ent.Comp.FlatpackerLinked && IsFlatpackable(recipe);
                var mult = flatpack ? ent.Comp.FlatpackMaterialMultiplier : 1f;

                var perUnit = new Dictionary<string, int>();
                foreach (var (mat, baseAmount) in recipe.Materials)
                    perUnit[mat.Id] = (int) MathF.Ceiling(baseAmount * mult);

                BillLine(ent.Comp, inv, invoiceMode: true, detailedInvoice, item.Id, Lathe.GetRecipeName(recipe),
                    RequisitionItemSource.Lathe, flatpack: flatpack, perUnit: perUnit, fridgeUnitPrice: 0,
                    queued: qty, qty: qty, coverUsed: null);
            }
            catch (Exception e)
            {
                Log.Error($"[Requisitions] invoice preview of '{item.Id}' threw, continuing: {e}");
                AppendInvoiceFailure(inv.Items, item.Id, Loc.GetString("requisitions-fail-error"));
                anyFailed = true;
            }
        }

        var invoiceTotal = args.OverridePrice is { } ov ? Math.Max(0, ov) : inv.RunningCost;
        var body = BuildInvoiceBody(args.InvoiceTitle, inv.Items, inv.Mats, inv.Fees, inv.MatBilled, inv.MatWorth, invoiceTotal, detailedInvoice);
        SpawnInvoice(ent, args.Actor, args.InvoiceTitle, invoiceTotal, body);

        _popup.PopupEntity(Loc.GetString(anyFailed ? "requisitions-preview-partial" : "requisitions-preview-done"), ent.Owner, args.Actor);
    }

    // Accumulators for building an invoice's body and running totals across all its lines.
    private sealed class InvoiceAccum
    {
        public readonly StringBuilder Items = new();
        public readonly Dictionary<string, (int Raw, int Covered, int Billed)> Mats = new();
        public readonly Dictionary<string, (string Name, bool Percent, int Rate, int Count, int Total)> Fees = new();
        public int MatBilled;
        public int MatWorth;
        public int RunningCost;
    }

    // Costs queued units of one line — a lathe recipe (material-priced) or a fridge item
    // (manually priced, no materials) — adding to inv's running total and, when
    // invoiceMode, appending its invoice section. coverUsed is the
    // customer's contributed materials applied to this line (null for a fridge item or a preview quote).
    private void BillLine(RequisitionsConsoleComponent comp, InvoiceAccum inv, bool invoiceMode, bool detailed,
        string id, string name, RequisitionItemSource source, bool flatpack,
        Dictionary<string, int>? perUnit, int fridgeUnitPrice, int queued, int qty, Dictionary<string, int>? coverUsed)
    {
        var itemCost = 0;
        var worth = 0;
        var matLines = new List<(string Mat, int Raw, int Covered, int Billed)>();

        if (source == RequisitionItemSource.Fridge)
        {
            // Fridge items have no material breakdown; their whole worth is the manual unit price.
            worth = fridgeUnitPrice * queued;
            itemCost = worth;
        }
        else if (perUnit != null)
        {
            foreach (var (mat, need) in perUnit)
            {
                var raw = need * queued;
                var covered = coverUsed?.GetValueOrDefault(mat) ?? 0;
                var billed = SheetCost(comp, mat, raw - covered);
                itemCost += billed;
                worth += SheetCost(comp, mat, raw);
                matLines.Add((mat, raw, covered, billed));
            }
        }

        var fees = FeesFor(comp, id, source, flatpack);
        var feeLines = new List<(RequisitionFee Fee, int Amount)>();
        foreach (var fee in fees)
        {
            var amt = fee.AmountFor(worth, queued);
            itemCost += amt;
            feeLines.Add((fee, amt));
        }

        inv.RunningCost += itemCost;

        if (!invoiceMode)
            return;

        // The item header line is shared by detailed and trimmed invoices; the marker on the label is what makes
        // a printed invoice re-parseable back into a cart (see TryParseInvoiceCart).
        var label = InvoiceItemLabel(name, queued, qty, flatpack);
        inv.Items.Append($"[bold][color=#9a6a12]{label}[/color][/bold]   ${itemCost}\n");

        if (detailed)
        {
            foreach (var (mat, raw, covered, billed) in matLines)
            {
                inv.MatBilled += billed;
                inv.MatWorth += SheetCost(comp, mat, raw);
                var disc = covered > 0 ? $"  (−{SheetLabelServer(mat, covered)})" : "";
                inv.Items.Append($"    {SheetLabelServer(mat, raw)}{disc}   ${billed}\n");
                var prevMat = inv.Mats.GetValueOrDefault(mat);
                inv.Mats[mat] = (prevMat.Raw + raw, prevMat.Covered + covered, prevMat.Billed + billed);
            }
            foreach (var (fee, amt) in feeLines)
            {
                var rate = fee.Type == RequisitionFeeType.Percent ? $"{fee.Price}%" : $"${fee.Price}";
                inv.Items.Append($"    {fee.Name} ({rate})   ${amt}\n");
                var prev = inv.Fees.GetValueOrDefault(fee.Id);
                inv.Fees[fee.Id] = (fee.Name, fee.Type == RequisitionFeeType.Percent, fee.Price, prev.Count + queued, prev.Total + amt);
            }
            inv.Items.Append('\n');
        }
        else
        {
            // Trimmed invoice: still separate each item with a blank line, like the failure lines do.
            inv.Items.Append('\n');
        }
    }

    // An invoice item label: display name, an explicit count when more than one, and a "(Flatpacked)" marker.
    // TryParseInvoiceCart reads this format back.
    private static string InvoiceItemLabel(string name, int queued, int qty, bool flatpack)
    {
        var flat = flatpack ? " (Flatpacked)" : "";
        if (queued < qty)
            return $"{name} (×{queued} of {qty}){flat}";
        if (queued > 1)
            return $"{name} (×{queued}){flat}";
        return $"{name}{flat}";
    }

    // Assembles the invoice body markup. A detailed invoice has a header, per-item detail, then a totals
    // section (per-material list, material-cost summary, fees, grand total). A non-detailed one is just the title,
    // one "name — cost" line per item (plus any failures), and the grand total.
    private string BuildInvoiceBody(string title, StringBuilder items,
        Dictionary<string, (int Raw, int Covered, int Billed)> mats,
        Dictionary<string, (string Name, bool Percent, int Rate, int Count, int Total)> fees,
        int matBilled, int matWorth, int total, bool detailed)
    {
        if (string.IsNullOrWhiteSpace(title))
            title = Loc.GetString("requisitions-invoice-default-title");

        // Colours are tuned for the light "paper" background the invoice renders on: deep, saturated tones.
        // Title = deep indigo, section headers = dark teal, materials = neutral, the material subtotal = brown,
        // fees = violet, grand total = green, failures (elsewhere) = dark red.
        var sb = new StringBuilder();
        sb.Append($"[head=2][color=#2a3f6a]{title}[/color][/head]\n\n");

        // Trimmed invoice: just the per-item lines and the grand total.
        if (!detailed)
        {
            sb.Append(items);
            sb.Append('\n');
            sb.Append($"[bold][color=#1f7a33]{Loc.GetString("requisitions-summary-total")}: ${total}[/color][/bold]");
            return sb.ToString();
        }

        sb.Append($"[head=3][color=#1f6f5c]{Loc.GetString("requisitions-invoice-items")}[/color][/head]\n");
        sb.Append(items);
        sb.Append($"[head=3][color=#1f6f5c]{Loc.GetString("requisitions-invoice-total-header")}[/color][/head]\n");

        // Per-material totals across the whole order, like the console's stock/breakdown lines.
        foreach (var (mat, tally) in mats.OrderBy(kv => kv.Key))
        {
            var disc = tally.Covered > 0 ? $"  [color=#2f7a3a](−{SheetLabelServer(mat, tally.Covered)})[/color]" : "";
            sb.Append($"{SheetLabelServer(mat, tally.Raw)}{disc}   ${tally.Billed}\n");
        }

        sb.Append($"[bold][color=#7a4a12]{Loc.GetString("requisitions-summary-material")}[/color][/bold]: ${matBilled}");
        if (matWorth > matBilled)
            sb.Append($"  [color=#2f7a3a](−${matWorth - matBilled} {Loc.GetString("requisitions-invoice-your-materials")})[/color]");
        sb.Append('\n');

        foreach (var (_, f) in fees)
        {
            var rate = f.Percent ? $"{f.Rate}%" : $"${f.Rate}";
            sb.Append($"[color=#5e3a8c]{f.Name} ({rate}) ({f.Count})[/color]   ${f.Total}\n");
        }

        sb.Append($"[bold][color=#1f7a33]{Loc.GetString("requisitions-summary-total")}: ${total}[/color][/bold]");
        return sb.ToString();
    }

    // Appends a bold-red "failed" line (with a reason) for an item the order couldn't fulfil.
    private void AppendInvoiceFailure(StringBuilder body, string name, string reason)
    {
        body.Append($"[bold][color=#b32020]{name} — {Loc.GetString("requisitions-invoice-failed")}[/color][/bold]\n");
        body.Append($"    [color=#b32020]{reason}[/color]\n\n");
    }

    // Spawns a payable invoice item targeted at the faction the console is tagged to and hands it over.
    private void SpawnInvoice(Entity<RequisitionsConsoleComponent> console, EntityUid actor, string title, int cost, string body)
    {
        var invoice = Spawn("Invoice", Transform(console).Coordinates);

        if (TryComp<InvoiceComponent>(invoice, out var comp))
        {
            comp.InvoiceCost = cost;
            comp.InvoiceReason = body;

            // Pay into the faction the console was tagged to with the station/faction tagger (a StationTracker
            // pointing at that station). Fall back to whatever station owns the console's grid when untagged.
            EntityUid? station = null;
            if (TryComp<StationTrackerComponent>(console.Owner, out var tracker) && tracker.Station is { } tagged)
                station = tagged;
            station ??= _station.GetOwningStation(console.Owner, null, true);

            if (station != null && TryComp<StationDataComponent>(station, out var sd))
                comp.TargetStation = sd.UID;
        }

        if (string.IsNullOrWhiteSpace(title))
            title = Loc.GetString("requisitions-invoice-default-title");
        _metaData.SetEntityName(invoice, $"invoice ${cost} {title}");
        _hands.PickupOrDrop(actor, invoice);
    }

    #region Fridge dispensing

    // Ejects up to count entities named itemName from the linked smart
    // fridges into the world, spreading across fridges. Returns how many were actually ejected; the fridge's own
    // container hooks keep its entry/stock bookkeeping in sync.
    private int EjectFridgeItems(Entity<RequisitionsConsoleComponent> ent, string itemName, int count)
    {
        var ejected = 0;
        foreach (var machine in ent.Comp.LinkedMachines)
        {
            if (ejected >= count)
                break;
            if (!_fridgeQuery.TryComp(machine, out var fridge))
                continue;
            if (!fridge.ContainedEntries.TryGetValue(new SmartFridgeEntry(itemName), out var nets))
                continue;

            // Snapshot before removing: TryRemoveFromContainer fires the fridge's own removal hook, which mutates
            // this very set.
            foreach (var net in nets.ToList())
            {
                if (ejected >= count)
                    break;
                if (_container.TryRemoveFromContainer(GetEntity(net)))
                    ejected++;
            }
        }

        return ejected;
    }

    #endregion

    #region Invoice slot (load cart from a slotted invoice)

    private void OnEntInserted(Entity<RequisitionsConsoleComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != RequisitionsConsoleComponent.InvoiceSlotId)
            return;

        // Parse the slotted invoice into a cart. On failure, leave the cart untouched and show a popup.
        if (TryComp<InvoiceComponent>(args.Entity, out var invoice)
            && TryParseInvoiceCart(ent, invoice.InvoiceReason, out var cart))
        {
            ent.Comp.LoadedOrder = cart;
            ent.Comp.LoadedOrderPrice = invoice.InvoiceCost;
            ent.Comp.LoadedOrderToken++;
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("requisitions-invoice-unreadable"), ent.Owner);
        }

        UpdateUi(ent);
    }

    private void OnEntRemoved(Entity<RequisitionsConsoleComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != RequisitionsConsoleComponent.InvoiceSlotId)
            return;

        ent.Comp.LoadedOrder = new();
        UpdateUi(ent);
    }

    // Ejects the invoice sitting in the console's invoice slot (if any) into the actor's hands.
    private void EjectSlottedInvoice(Entity<RequisitionsConsoleComponent> ent, EntityUid user)
    {
        _itemSlots.TryEject(ent.Owner, RequisitionsConsoleComponent.InvoiceSlotId, user, out _);
    }

    // Whether an invoice is currently sitting in the console's invoice slot.
    private bool HasSlottedInvoice(EntityUid console)
    {
        return _itemSlots.TryGetSlot(console, RequisitionsConsoleComponent.InvoiceSlotId, out var slot)
               && slot.ContainerSlot?.ContainedEntity != null;
    }

    // Matches an invoice item header line, e.g. "[bold][color=#9a6a12]Steel (×2) (Flatpacked)[/color][/bold]   $120".
    private static readonly Regex InvoiceItemLine =
        new(@"^\[bold\]\[color=#9a6a12\](?<label>.*?)\[/color\]\[/bold\]\s+\$-?\d+\s*$", RegexOptions.Compiled);
    private static readonly Regex InvoiceQty =
        new(@"\(×(?<n>\d+)(?: of \d+)?\)$", RegexOptions.Compiled);

    // Reconstructs a cart from a printed invoice body by matching each item line's display name against the
    // current catalogue. Unmatched lines are skipped. Returns true if any line was recovered.
    private bool TryParseInvoiceCart(Entity<RequisitionsConsoleComponent> ent, string body, out List<RequisitionCartItem> cart)
    {
        cart = new();
        if (string.IsNullOrWhiteSpace(body))
            return false;

        // Best-effort: match each printed item line's display name back to a current catalogue entry to recover its
        // id + source. Unmatched lines are skipped.
        var byName = new Dictionary<string, RequisitionCatalogueEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in BuildCatalogue(ent))
            byName.TryAdd(entry.Name, entry);

        foreach (var lineRaw in body.Split('\n'))
        {
            var m = InvoiceItemLine.Match(lineRaw.Trim());
            if (!m.Success)
                continue;

            var label = m.Groups["label"].Value;

            // Peel markers off the label right-to-left: "(Flatpacked)" then "(×n[ of m])".
            var flatpack = false;
            if (label.EndsWith("(Flatpacked)", StringComparison.Ordinal))
            {
                flatpack = true;
                label = label[..^"(Flatpacked)".Length].TrimEnd();
            }

            var qty = 1;
            var qm = InvoiceQty.Match(label);
            if (qm.Success)
            {
                qty = int.Parse(qm.Groups["n"].Value);
                label = label[..qm.Index].TrimEnd();
            }

            if (!byName.TryGetValue(label, out var entry))
                continue; // unknown item — skip

            cart.Add(new RequisitionCartItem { Id = entry.Id, Source = entry.Source, Quantity = qty, Flatpack = flatpack });
        }

        return cart.Count > 0;
    }

    #endregion

    private string SheetLabelServer(string matId, int rawAmount)
    {
        if (!Proto.TryIndex<MaterialPrototype>(matId, out var m))
            return $"{matId} {rawAmount}";

        var volume = _materialStorage.GetSheetVolume(m);
        if (volume <= 0)
            volume = 1;

        // Amount first, e.g. "6 Steel sheets".
        return $"{MathF.Round(rawAmount / (float) volume, 2)} {Loc.GetString(m.Name)} {Loc.GetString(m.Unit)}";
    }

    // Queues up to qty units of the recipe, one at a time, across the linked lathes that can
    // print it — so an order spreads over machines and a machine that runs out of materials falls through to
    // another. Returns how many units were actually queued (a machine can accept fewer than requested),
    // and accumulates the customer's contribution that was applied into coverUsed. Only the
    // queued units get billed, so a unit that can't be produced never reaches the invoice.
    private int TryDispatchLathe(Entity<RequisitionsConsoleComponent> ent, LatheRecipePrototype recipe, int qty,
        Dictionary<string, int> perUnit, Dictionary<string, int> pool, Dictionary<string, int> coverUsed,
        out string? reason, EntityUid? flatpacker = null)
    {
        reason = null;
        var queued = 0;

        // The lathes that can print this recipe (resolved once for the whole line).
        var capable = new List<EntityUid>();
        foreach (var machine in ent.Comp.LinkedMachines)
        {
            if (_latheQuery.TryComp(machine, out var lathe) && _lathe.GetAvailableRecipes(machine, lathe).ContainsKey(recipe.ID))
                capable.Add(machine);
        }

        var hadCandidate = capable.Count > 0;

        for (var u = 0; u < qty; u++)
        {
            // What the customer's remaining contribution covers for this single unit.
            var unitCover = new Dictionary<string, int>();
            foreach (var (mat, need) in perUnit)
            {
                var c = Math.Min(pool.GetValueOrDefault(mat), need);
                if (c > 0)
                    unitCover[mat] = c;
            }

            var placed = false;
            foreach (var machine in capable)
            {
                var applied = MoveCover(ent.Owner, machine, unitCover, into: true);

                if (_lathe.TryAddToQueue(machine, recipe, 1))
                {
                    // Start staggered rather than immediately: starting many machines on the same tick spikes
                    // their combined power draw and browns out the APC. See ScheduleStart / Update.
                    ScheduleStart(machine);

                    // Record a job per printed unit so the console tracks progress and (for flatpack orders)
                    // routes each finished board to a flatpacker.
                    var jobs = EnsureComp<RequisitionsLatheJobComponent>(machine);
                    jobs.Jobs.Add(new RequisitionJob
                    {
                        Recipe = recipe.ID,
                        Console = ent.Owner,
                        Cover = applied.Count > 0 ? new Dictionary<string, int>(applied) : null,
                        Flatpacker = flatpacker,
                    });

                    // Spend and bill only the contribution that actually moved into the machine.
                    foreach (var (mat, c) in applied)
                    {
                        pool[mat] = pool.GetValueOrDefault(mat) - c;
                        coverUsed[mat] = coverUsed.GetValueOrDefault(mat) + c;
                    }

                    ent.Comp.OutstandingJobs++; // locks the console until this print finishes
                    queued++;
                    placed = true;
                    break;
                }

                MoveCover(ent.Owner, machine, applied, into: false); // revert exactly what moved, try the next machine
            }

            if (!placed)
                break; // no linked machine could take another unit — stop here
        }

        // No per-unit UpdateUi here — OnCheckout pushes once at the end (and it is suppressed during dispatch).
        if (queued <= 0)
            reason = Loc.GetString(hadCandidate ? "requisitions-fail-no-materials" : "requisitions-fail-no-machine");

        return queued;
    }

    // Prints boards on a lathe like any other item; a transfer job on the lathe moves each finished board into a
    // flatpacker once it's done printing. See OnLatheItemProduced. Returns the number of units
    // actually queued.
    private int TryDispatchFlatpack(Entity<RequisitionsConsoleComponent> ent, LatheRecipePrototype recipe, int qty,
        Dictionary<string, int> perUnit, Dictionary<string, int> pool, Dictionary<string, int> coverUsed, out string? reason)
    {
        if (recipe.Result is null)
        {
            reason = Loc.GetString("requisitions-fail-no-machine");
            return 0;
        }

        EntityUid? flatpacker = null;
        foreach (var machine in ent.Comp.LinkedMachines)
        {
            if (HasComp<FlatpackCreatorComponent>(machine))
            {
                flatpacker = machine;
                break;
            }
        }

        if (flatpacker == null)
        {
            reason = Loc.GetString("requisitions-fail-no-flatpacker");
            return 0;
        }

        return TryDispatchLathe(ent, recipe, qty, perUnit, pool, coverUsed, out reason, flatpacker);
    }

    // Moves the covered contribution between the console and a target machine. For into it moves only as much
    // as the machine can hold; for the revert it moves back what was passed in. Returns the amount actually
    // moved per material, which is what the caller bills and refunds.
    private Dictionary<string, int> MoveCover(EntityUid console, EntityUid machine, Dictionary<string, int> cover, bool into)
    {
        var applied = new Dictionary<string, int>();
        foreach (var (mat, c) in cover)
        {
            if (c <= 0)
                continue;

            if (into)
            {
                if (!_materialStorage.CanChangeMaterialAmount(machine, mat, c))
                    continue; // machine can't hold it — leave the sheets in the customer's pool, bill normally

                _materialStorage.TryChangeMaterialAmount(console, mat, -c, localOnly: true);
                _materialStorage.TryChangeMaterialAmount(machine, mat, c);
            }
            else
            {
                _materialStorage.TryChangeMaterialAmount(machine, mat, -c);
                _materialStorage.TryChangeMaterialAmount(console, mat, c, localOnly: true);
            }

            applied[mat] = c;
        }

        return applied;
    }

    #endregion

    #region Flatpack transfer

    // A requisition item finished printing: mark one job done. Flatpack boards are moved into the console's
    // internal storage (from where they're fed to a flatpacker); everything else is delivered at the lathe.
    private void OnLatheItemProduced(Entity<RequisitionsLatheJobComponent> ent, ref LatheItemProducedEvent args)
    {
        var recipeId = args.Recipe.ID;
        var result = args.Result;

        var idx = ent.Comp.Jobs.FindIndex(j => j.Recipe == recipeId);
        if (idx < 0)
            return;

        var job = ent.Comp.Jobs[idx];
        ent.Comp.Jobs.RemoveAt(idx);
        if (ent.Comp.Jobs.Count == 0)
            RemCompDeferred<RequisitionsLatheJobComponent>(ent);

        if (!TryComp<RequisitionsConsoleComponent>(job.Console, out var console))
            return; // console gone: a non-flatpack item just stays at the lathe

        // Drop the in-progress count and invalidate the lathe catalogue (its remaining-print count changed).
        console.OutstandingJobs = Math.Max(0, console.OutstandingJobs - 1);
        console.LatheCatalogueCache = null;

        // Flatpack items: move the actual printed board into the console's internal storage and try to feed a
        // flatpacker from it. Storing the real board (rather than a proto in memory) means nothing is lost if
        // packing stalls or the round restarts.
        if (job.Flatpacker != null && !TerminatingOrDeleted(result))
        {
            var container = _container.EnsureContainer<Container>(job.Console, console.FlatpackStorageId);
            if (_container.Insert(result, container))
            {
                console.NextFlatpackTry = TimeSpan.Zero;
                TryFeedFlatpackers((job.Console, console));
            }
        }

        UpdateUi((job.Console, console));
    }

    // The lathe lost power and aborts its in-progress print, which our per-item dispatch does not resume. Return
    // each outstanding job's contributed materials to the customer and clear the console's in-progress count.
    private void OnJobLathePowerChanged(Entity<RequisitionsLatheJobComponent> ent, ref PowerChangedEvent args)
    {
        if (args.Powered)
            return;

        ReleaseJobs(ent);
        ent.Comp.Jobs.Clear();
        RemCompDeferred<RequisitionsLatheJobComponent>(ent);
    }

    // The lathe (and its jobs) are being deleted mid-print — release whatever's still outstanding.
    private void OnJobLatheShutdown(Entity<RequisitionsLatheJobComponent> ent, ref ComponentShutdown args)
    {
        ReleaseJobs(ent);
        ent.Comp.Jobs.Clear();
    }

    // For every outstanding job on a lathe, returns the customer's contributed materials (the aborted lathe was
    // handed them back) and clears the console's in-progress count so it doesn't stay locked.
    private void ReleaseJobs(Entity<RequisitionsLatheJobComponent> ent)
    {
        foreach (var job in ent.Comp.Jobs)
        {
            if (!TryComp<RequisitionsConsoleComponent>(job.Console, out var console))
                continue;

            if (job.Cover is { } cover)
            {
                var coords = Transform(job.Console).Coordinates;
                foreach (var (mat, amount) in cover)
                {
                    var take = Math.Min(amount, _materialStorage.GetMaterialAmount(ent.Owner, mat));
                    if (take <= 0)
                        continue;

                    _materialStorage.SpawnMultipleFromMaterial(take, mat, coords, out var overflow);
                    _materialStorage.TryChangeMaterialAmount(ent.Owner, mat, -(take - overflow));
                }
            }

            console.OutstandingJobs = Math.Max(0, console.OutstandingJobs - 1);
            UpdateUi((job.Console, console));
        }
    }

    // Feeds one stored board from the console's internal storage into an idle linked flatpacker. No spawning or
    // deleting: the real board is reparented into the flatpacker, or left in storage to retry later.
    private void TryFeedFlatpackers(Entity<RequisitionsConsoleComponent> console)
    {
        if (_timing.CurTime < console.Comp.NextFlatpackTry)
            return;

        if (!_container.TryGetContainer(console, console.Comp.FlatpackStorageId, out var container) || container.ContainedEntities.Count == 0)
            return;

        foreach (var machine in console.Comp.LinkedMachines)
        {
            if (!TryComp<FlatpackCreatorComponent>(machine, out var creator) || !_flatpack.IsIdle((machine, creator)))
                continue;

            var board = container.ContainedEntities[0];

            // TryPackBoard reparents the board into the flatpacker on success; on failure it drops it in the
            // world, so we pull it back into storage and back off.
            if (_flatpack.TryPackBoard((machine, creator), board))
                return;

            _container.Insert(board, container);
            console.Comp.NextFlatpackTry = _timing.CurTime + TimeSpan.FromSeconds(2);
            return;
        }

        // Nothing idle right now — check again shortly.
        console.Comp.NextFlatpackTry = _timing.CurTime + TimeSpan.FromSeconds(1);
    }

    private static readonly TimeSpan StartStagger = TimeSpan.FromSeconds(0.2);
    private readonly Queue<EntityUid> _pendingStarts = new();
    private readonly HashSet<EntityUid> _pendingStartSet = new();
    private TimeSpan _nextStart;

    // Queues a lathe to be kicked into production, spaced out from other starts (see Update).
    private void ScheduleStart(EntityUid lathe)
    {
        if (_pendingStartSet.Add(lathe))
            _pendingStarts.Enqueue(lathe);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // Start at most one queued lathe every StartStagger seconds so a big order that involves different lathes
        // doesn't flip every machine into its high-power working state on the same tick and brown out the APC.
        if (_pendingStarts.Count > 0 && now >= _nextStart)
        {
            var lathe = _pendingStarts.Dequeue();
            _pendingStartSet.Remove(lathe);
            if (!TerminatingOrDeleted(lathe))
                _lathe.TryStartProducing(lathe);
            _nextStart = now + StartStagger;
        }

        var query = EntityQueryEnumerator<RequisitionsConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // Skip idle consoles: only bother when there is actually a board waiting to be flatpacked.
            if (!_container.TryGetContainer(uid, comp.FlatpackStorageId, out var store) || store.ContainedEntities.Count == 0)
                continue;

            TryFeedFlatpackers((uid, comp));
        }
    }

    // Price of a raw material amount, charged per sheet. Uses the shared costing math so the server bill
    // matches the client preview exactly.
    private int SheetCost(RequisitionsConsoleComponent comp, string materialId, int rawAmount)
    {
        if (rawAmount <= 0 || !Proto.TryIndex<MaterialPrototype>(materialId, out var mat))
            return 0;

        return RequisitionCosting.SheetCost(rawAmount, _materialStorage.GetSheetVolume(mat), GetPrice(comp, materialId));
    }

    // The customer changed their mind — return the sheets they inserted, and eject any slotted invoice.
    private void OnCancel(Entity<RequisitionsConsoleComponent> ent, ref RequisitionCancelMessage args)
    {
        _materialStorage.EjectAllMaterial(ent.Owner);
        EjectSlottedInvoice(ent, args.Actor);
        UpdateUi(ent, args.Actor);
    }

    // Ejects boards stuck in the internal flatpack storage back into the world (access-gated).
    private void OnEjectFlatpacks(Entity<RequisitionsConsoleComponent> ent, ref RequisitionEjectFlatpacksMessage args)
    {
        if (!HasConfigAccess(ent, args.Actor))
        {
            _popup.PopupEntity(Loc.GetString("requisitions-access-denied"), ent.Owner, args.Actor);
            return;
        }

        if (_container.TryGetContainer(ent, ent.Comp.FlatpackStorageId, out var container))
            _container.EmptyContainer(container);

        UpdateUi(ent);
    }

    #endregion

    #region UI state

    protected override void UpdateUi(Entity<RequisitionsConsoleComponent> ent, EntityUid? actor = null)
    {
        if (_suppressUi)
            return;

        if (!_ui.IsUiOpen(ent.Owner, RequisitionsConsoleUiKey.Key))
            return;

        var state = new RequisitionsConsoleState
        {
            Catalogue = BuildCatalogue(ent),
            Stock = BuildStock(ent),
            Contributed = _materialStorage.GetStoredMaterials(ent.Owner, localOnly: true).ToDictionary(kv => kv.Key.Id, kv => kv.Value),
            MaterialPrices = ent.Comp.MaterialPrices.ToDictionary(kv => kv.Key.Id, kv => kv.Value),
            MaterialFallbackPrice = ent.Comp.FallbackMaterialPrice,
            Fees = ent.Comp.Fees,
            FlatpackerLinked = ent.Comp.FlatpackerLinked,
            FlatpackMultiplier = ent.Comp.FlatpackMaterialMultiplier,
            Processing = ent.Comp.OutstandingJobs > 0,
            PendingFlatpacks = _container.TryGetContainer(ent, ent.Comp.FlatpackStorageId, out var fpStore) ? fpStore.ContainedEntities.Count : 0,
            DetailedInvoice = ent.Comp.DetailedInvoice,
            FridgeItemPrices = new Dictionary<string, int>(ent.Comp.FridgeItemPrices),
            LoadedOrderToken = ent.Comp.LoadedOrderToken,
            LoadedOrder = new List<RequisitionCartItem>(ent.Comp.LoadedOrder),
            LoadedOrderPrice = ent.Comp.LoadedOrderPrice,
            InvoiceSlotted = HasSlottedInvoice(ent.Owner),
        };

        // Staff-only console, gated at open time — anyone viewing is authorised, so always provide the link list.
        state.Linkable = BuildLinkable(ent);

        _ui.SetUiState(ent.Owner, RequisitionsConsoleUiKey.Key, state);
    }

    // The full catalogue: the cached lathe half (rebuilt only when the recipe set changes) plus a fresh
    // fridge half (cheap, and must reflect live stock).
    private List<RequisitionCatalogueEntry> BuildCatalogue(Entity<RequisitionsConsoleComponent> ent)
    {
        ent.Comp.LatheCatalogueCache ??= BuildLatheCatalogue(ent);

        var result = new List<RequisitionCatalogueEntry>(ent.Comp.LatheCatalogueCache);
        result.AddRange(BuildFridgeEntries(ent));
        return result;
    }

    private List<RequisitionCatalogueEntry> BuildLatheCatalogue(Entity<RequisitionsConsoleComponent> ent)
    {
        // Squash by an identity of "same result + same materials" so genuinely identical items (e.g. four
        // aprons offered by different recipes/machines) collapse to one line, while variants that cost
        // differently stay separate.
        var merged = new Dictionary<string, RequisitionCatalogueEntry>();

        foreach (var machine in ent.Comp.LinkedMachines)
        {
            if (!_latheQuery.TryComp(machine, out var lathe))
                continue;

            foreach (var (recipeId, count) in _lathe.GetAvailableRecipes(machine, lathe))
            {
                if (!Proto.TryIndex(recipeId, out var recipe))
                    continue;

                var materials = recipe.Materials.OrderBy(kv => kv.Key.Id).Select(kv => $"{kv.Key.Id}:{kv.Value}");
                var signature = $"{recipe.Result?.Id}|{string.Join(",", materials)}";

                // A negative count is the "unlimited" sentinel for static recipes; a non-negative one is the
                // remaining research prints.
                var unlimited = count < 0;

                if (!merged.TryGetValue(signature, out var entry))
                {
                    entry = new RequisitionCatalogueEntry
                    {
                        Id = recipeId,
                        Source = RequisitionItemSource.Lathe,
                        Name = Lathe.GetRecipeName(recipe),
                        Result = recipe.Result?.Id,
                        Materials = recipe.Materials.ToDictionary(kv => kv.Key.Id, kv => kv.Value),
                        Flatpackable = ent.Comp.FlatpackerLinked && IsFlatpackable(recipe),
                        PrintsRemaining = unlimited ? null : count,
                    };
                    merged[signature] = entry;
                }
                else if (unlimited)
                {
                    entry.PrintsRemaining = null; // available unlimited somewhere → not limited
                }
                else if (entry.PrintsRemaining is { } current)
                {
                    entry.PrintsRemaining = Math.Max(current, count); // most prints any linked source can do
                }

                entry.SourceCount++;
            }
        }

        return merged.Values.OrderBy(e => e.Name).ToList();
    }

    // One catalogue line per distinct item name across the linked smart fridges. Fridge items carry no material
    // cost (they're manually priced) and are capped at the number currently stocked (RequisitionCatalogueEntry.Available).
    private List<RequisitionCatalogueEntry> BuildFridgeEntries(Entity<RequisitionsConsoleComponent> ent)
    {
        var merged = new Dictionary<string, RequisitionCatalogueEntry>();

        foreach (var machine in ent.Comp.LinkedMachines)
        {
            if (!_fridgeQuery.TryComp(machine, out var fridge))
                continue;

            foreach (var (entry, nets) in fridge.ContainedEntries)
            {
                if (nets.Count == 0)
                    continue;

                var name = entry.Name;
                if (!merged.TryGetValue(name, out var cat))
                {
                    // First stored entity supplies the sprite; fall back to no icon if its prototype won't resolve.
                    string? result = null;
                    foreach (var net in nets)
                    {
                        if (MetaData(GetEntity(net)).EntityPrototype is { } proto)
                        {
                            result = proto.ID;
                            break;
                        }
                    }

                    cat = new RequisitionCatalogueEntry
                    {
                        Id = name,
                        Source = RequisitionItemSource.Fridge,
                        Name = name,
                        Result = result,
                        Available = 0,
                        FridgeUnitPrice = ent.Comp.FridgeItemPrices.GetValueOrDefault(name, ent.Comp.FridgeFallbackPrice),
                    };
                    merged[name] = cat;
                }

                cat.Available = (cat.Available ?? 0) + nets.Count;
            }
        }

        return merged.Values.OrderBy(e => e.Name).ToList();
    }

    private bool IsFlatpackable(LatheRecipePrototype recipe)
    {
        // The flatpacker packs machine/computer boards; a recipe is flatpackable if its result is one.
        if (recipe.Result is not { } result || !Proto.TryIndex<EntityPrototype>(result, out var proto))
            return false;

        return proto.Components.ContainsKey("MachineBoard") || proto.Components.ContainsKey("ComputerBoard");
    }

    private Dictionary<string, int> BuildStock(Entity<RequisitionsConsoleComponent> ent)
    {
        // Only report the stock of linked ore silos. Summing individual lathes is misleading (one loaded lathe
        // among ten would read as department-wide stock), so with no silo linked the stock panel is simply empty.
        var stock = new Dictionary<string, int>();
        foreach (var machine in ent.Comp.LinkedMachines)
        {
            if (!HasComp<OreSiloComponent>(machine))
                continue;

            foreach (var (mat, amount) in _materialStorage.GetStoredMaterials(machine, localOnly: true))
                stock[mat.Id] = stock.GetValueOrDefault(mat.Id) + amount;
        }

        return stock;
    }

    private List<RequisitionLinkEntry> BuildLinkable(Entity<RequisitionsConsoleComponent> ent)
    {
        var result = new List<RequisitionLinkEntry>();
        var seen = new HashSet<EntityUid>();

        var coords = Transform(ent).Coordinates;

        void Add(EntityUid machine)
        {
            if (!seen.Add(machine))
                return;

            result.Add(new RequisitionLinkEntry
            {
                Machine = GetNetEntity(machine),
                Label = MetaData(machine).EntityName,
                Linked = ent.Comp.LinkedMachines.Contains(machine),
                InRange = CanLink(ent.Owner, machine),
                Flatpacker = HasComp<FlatpackCreatorComponent>(machine),
            });
        }

        // Every entity in range, then keep the linkable ones.
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(coords, ent.Comp.Range, nearby);
        foreach (var machine in nearby)
        {
            if (IsLinkable(machine))
                Add(machine);
        }

        // Include already-linked machines even if they've drifted out of range.
        foreach (var machine in ent.Comp.LinkedMachines)
            Add(machine);

        return result;
    }

    #endregion

    // The fees of a given source that apply to an item id (a lathe recipe id or a fridge item name).
    private IEnumerable<RequisitionFee> FeesFor(RequisitionsConsoleComponent comp, string id, RequisitionItemSource source, bool flatpack)
    {
        foreach (var fee in comp.Fees)
        {
            if (fee.Source != source)
                continue;

            switch (fee.Scope)
            {
                case RequisitionFeeScope.Flatpack when flatpack:
                case RequisitionFeeScope.All:
                    yield return fee;
                    break;
                case RequisitionFeeScope.Specific when fee.Targets.Contains(id):
                    yield return fee;
                    break;
            }
        }
    }

    private int GetPrice(RequisitionsConsoleComponent comp, string material)
    {
        return comp.MaterialPrices.TryGetValue(material, out var price) ? price : comp.FallbackMaterialPrice;
    }
}
