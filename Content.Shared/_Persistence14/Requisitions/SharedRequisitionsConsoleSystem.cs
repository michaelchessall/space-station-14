using System.Linq;
using Content.Shared.Access.Systems;
using Content.Shared.Construction.Components;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Materials.OreSilo;
using Content.Shared.Power.EntitySystems;
using Content.Shared.SmartFridge;
using Robust.Shared.Prototypes;

namespace Content.Shared._Persistence14.Requisitions;

// Shared logic for the requisitions console: linking to nearby printing machines and operator configuration
// of prices/fees. Money handling, print dispatch and UI-state building are server-only and live in the
// server subclass, which overrides UpdateUi.
public abstract class SharedRequisitionsConsoleSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] protected readonly SharedLatheSystem Lathe = default!;
    [Dependency] protected readonly IPrototypeManager Proto = default!;

    private EntityQuery<LatheComponent> _latheQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RequisitionsConsoleComponent, ToggleRequisitionLinkMessage>(OnToggleLink);
        SubscribeLocalEvent<RequisitionsConsoleComponent, RequisitionSetMaterialPriceMessage>(OnSetMaterialPrice);
        SubscribeLocalEvent<RequisitionsConsoleComponent, RequisitionSetFeeMessage>(OnSetFee);
        SubscribeLocalEvent<RequisitionsConsoleComponent, RequisitionRemoveFeeMessage>(OnRemoveFee);
        SubscribeLocalEvent<RequisitionsConsoleComponent, RequisitionSetDetailedInvoiceMessage>(OnSetDetailedInvoice);
        SubscribeLocalEvent<RequisitionsConsoleComponent, RequisitionSetFridgePriceMessage>(OnSetFridgePrice);
        SubscribeLocalEvent<RequisitionsConsoleComponent, ComponentShutdown>(OnShutdown);

        Subs.BuiEvents<RequisitionsConsoleComponent>(RequisitionsConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnBoundUiOpened);
        });

        _latheQuery = GetEntityQuery<LatheComponent>();
    }

    #region Linking

    private void OnToggleLink(Entity<RequisitionsConsoleComponent> ent, ref ToggleRequisitionLinkMessage args)
    {
        if (!HasConfigAccess(ent, args.Actor))
            return;

        var machine = GetEntity(args.Machine);

        if (ent.Comp.LinkedMachines.Contains(machine))
            ent.Comp.LinkedMachines.Remove(machine);
        else if (CanLink(ent.Owner, machine))
            ent.Comp.LinkedMachines.Add(machine);
        else
            return;

        RefreshLinkState(ent);
        UpdateUi(ent, args.Actor);
    }

    private void OnShutdown(Entity<RequisitionsConsoleComponent> ent, ref ComponentShutdown args)
    {
        // Nothing to clean up on the linked machines themselves; they are unaware of the console.
    }

    // A machine is linkable if it can print (lathe) or flatpack, is powered, on the same grid and in range.
    public bool CanLink(EntityUid console, EntityUid machine)
    {
        if (!IsLinkable(machine))
            return false;

        if (!_power.IsPowered(console) || !_power.IsPowered(machine))
            return false;

        if (_transform.GetGrid(console) != _transform.GetGrid(machine))
            return false;

        if (!TryComp<RequisitionsConsoleComponent>(console, out var comp))
            return false;

        return _transform.InRange(console, machine, comp.Range);
    }

    public bool IsLinkable(EntityUid machine)
    {
        // Lathes print, flatpackers pack, an ore silo supplies the shared "department stock" readout, and a
        // smart fridge contributes its stored items to the catalogue.
        return _latheQuery.HasComp(machine)
               || HasComp<FlatpackCreatorComponent>(machine)
               || HasComp<OreSiloComponent>(machine)
               || HasComp<SmartFridgeComponent>(machine);
    }

    // Prunes dead links, recomputes whether a flatpacker is present, seeds default prices for any newly
    // available material, and ensures the automatic flatpack fee exists while a flatpacker is linked.
    protected void RefreshLinkState(Entity<RequisitionsConsoleComponent> ent)
    {
        ent.Comp.LinkedMachines.RemoveWhere(m => TerminatingOrDeleted(m) || !IsLinkable(m));

        ent.Comp.FlatpackerLinked = false;
        foreach (var machine in ent.Comp.LinkedMachines)
        {
            if (HasComp<FlatpackCreatorComponent>(machine))
                ent.Comp.FlatpackerLinked = true;
        }

        SyncMaterialPrices(ent);
        EnsureFlatpackFee(ent);
        OnCatalogueSourcesChanged(ent);
    }

    // Hook fired when the set of linked machines (hence the available recipes) changes. The server overrides this
    // to drop its cached lathe catalogue. No-op on the client.
    protected virtual void OnCatalogueSourcesChanged(Entity<RequisitionsConsoleComponent> ent)
    {
    }

    // Keeps the priced-materials list exactly in step with what the linked machines can make: newly available
    // materials are seeded from RequisitionsConsoleComponent.DefaultMaterialPrices (falling back
    // to the flat default), and materials no longer used by any linked lathe are dropped — so unlinking a lathe
    // wipes any raw materials only it needed. This is the "scoped to linked recipes" behaviour.
    private void SyncMaterialPrices(Entity<RequisitionsConsoleComponent> ent)
    {
        var priceable = GetPriceableMaterials(ent);

        // Drop prices for materials nothing linked uses anymore.
        var stale = ent.Comp.MaterialPrices.Keys.Where(k => !priceable.Contains(k)).ToList();
        foreach (var mat in stale)
            ent.Comp.MaterialPrices.Remove(mat);

        // Seed a default price for anything newly priceable.
        foreach (var material in priceable)
        {
            if (ent.Comp.MaterialPrices.ContainsKey(material))
                continue;

            ent.Comp.MaterialPrices[material] = ent.Comp.DefaultMaterialPrices.TryGetValue(material, out var price)
                ? price
                : ent.Comp.FallbackMaterialPrice;
        }
    }

    // Every material id that appears in any recipe of any linked lathe.
    public HashSet<ProtoId<MaterialPrototype>> GetPriceableMaterials(Entity<RequisitionsConsoleComponent> ent)
    {
        var result = new HashSet<ProtoId<MaterialPrototype>>();
        foreach (var machine in ent.Comp.LinkedMachines)
        {
            if (!_latheQuery.TryComp(machine, out var lathe))
                continue;

            foreach (var recipeId in Lathe.GetAllPossibleRecipes(lathe))
            {
                if (!Proto.TryIndex(recipeId, out var recipe))
                    continue;

                foreach (var material in recipe.Materials.Keys)
                    result.Add(material);
            }
        }

        return result;
    }

    private void EnsureFlatpackFee(Entity<RequisitionsConsoleComponent> ent)
    {
        var exists = ent.Comp.Fees.Any(f => f.Id == ent.Comp.FlatpackFeeId);

        if (ent.Comp.FlatpackerLinked && !exists)
        {
            ent.Comp.Fees.Add(new RequisitionFee
            {
                Id = ent.Comp.FlatpackFeeId,
                Name = Loc.GetString("requisitions-fee-flatpack"),
                Price = 0,
                Source = RequisitionItemSource.Lathe,
                Scope = RequisitionFeeScope.Flatpack,
            });
        }
    }

    #endregion

    #region Operator configuration

    private void OnSetMaterialPrice(Entity<RequisitionsConsoleComponent> ent, ref RequisitionSetMaterialPriceMessage args)
    {
        if (!HasConfigAccess(ent, args.Actor))
            return;

        if (args.Price < 0)
            ent.Comp.MaterialPrices.Remove(args.Material);
        else
            ent.Comp.MaterialPrices[args.Material] = args.Price;

        UpdateUi(ent, args.Actor);
    }

    private void OnSetFee(Entity<RequisitionsConsoleComponent> ent, ref RequisitionSetFeeMessage args)
    {
        if (!HasConfigAccess(ent, args.Actor))
            return;

        var incoming = args.Fee;

        // One fee list holds both lathe and fridge fees, discriminated by Source. The flatpack fee's scope/name
        // are fixed; operators may only set its price and flat/percent type.
        var existing = ent.Comp.Fees.FirstOrDefault(f => f.Id == incoming.Id);
        if (existing != null && existing.Id == ent.Comp.FlatpackFeeId)
        {
            existing.Price = incoming.Price;
            existing.Type = incoming.Type;
        }
        else if (existing != null)
        {
            existing.Name = incoming.Name;
            existing.Price = incoming.Price;
            existing.Type = incoming.Type;
            existing.Scope = incoming.Scope;
            existing.Targets = incoming.Targets;
            // Source is immutable once created; keep the existing one.
        }
        else
        {
            ent.Comp.Fees.Add(incoming);
        }

        UpdateUi(ent, args.Actor);
    }

    private void OnRemoveFee(Entity<RequisitionsConsoleComponent> ent, ref RequisitionRemoveFeeMessage args)
    {
        if (!HasConfigAccess(ent, args.Actor))
            return;

        var id = args.Id;

        // The automatic flatpack fee cannot be removed while a flatpacker is linked.
        if (id == ent.Comp.FlatpackFeeId && ent.Comp.FlatpackerLinked)
            return;

        var actor = args.Actor;
        ent.Comp.Fees.RemoveAll(f => f.Id == id);
        UpdateUi(ent, actor);
    }

    private void OnSetDetailedInvoice(Entity<RequisitionsConsoleComponent> ent, ref RequisitionSetDetailedInvoiceMessage args)
    {
        if (!HasConfigAccess(ent, args.Actor))
            return;

        ent.Comp.DetailedInvoice = args.Detailed;
        UpdateUi(ent, args.Actor);
    }

    private void OnSetFridgePrice(Entity<RequisitionsConsoleComponent> ent, ref RequisitionSetFridgePriceMessage args)
    {
        if (!HasConfigAccess(ent, args.Actor))
            return;

        if (args.Price < 0)
            ent.Comp.FridgeItemPrices.Remove(args.Item);
        else
            ent.Comp.FridgeItemPrices[args.Item] = args.Price;

        UpdateUi(ent, args.Actor);
    }

    #endregion

    #region Access

    // Faction-aware config gate. Delegates to the fork's AccessReaderSystem, which resolves the
    // console's owning station (faction) and passes faction owners plus crew whose role holds a matching
    // access permission. A console with no AccessReaderComponent (freshly built) is open until configured.
    public bool HasConfigAccess(EntityUid console, EntityUid actor)
    {
        return _access.IsAllowed(actor, console);
    }

    #endregion

    private void OnBoundUiOpened(Entity<RequisitionsConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        RefreshLinkState(ent);
        UpdateUi(ent, args.Actor);
    }

    // Server rebuilds and pushes the full UI state. No-op on the client. actor is the player whose action
    // triggered the update, if any.
    protected virtual void UpdateUi(Entity<RequisitionsConsoleComponent> ent, EntityUid? actor = null)
    {
    }
}
