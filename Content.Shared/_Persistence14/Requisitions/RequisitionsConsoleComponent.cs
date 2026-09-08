using System;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom; // Persistence: TimeOffsetSerializer

namespace Content.Shared._Persistence14.Requisitions;

// A staff-run ordering console. It links (ore-silo style) to nearby lathes, flatpackers, ore silos, and smart
// fridges, merges their offerings into one catalogue, and prints/dispenses a whole cart in a single checkout.
// Payment, when charged, is a printed Invoice paid via bank into the console's owning faction; a printed
// invoice can be re-slotted into a console to reload its cart (read back from the invoice's item lines). The
// whole console is access-gated at open time (empty AccessReader = open until configured). Access-gated config
// tabs let an operator set per-material and per-fridge-item prices, define fees, link machines, and toggle
// detailed invoices.
[RegisterComponent]
[Access(typeof(SharedRequisitionsConsoleSystem))]
public sealed partial class RequisitionsConsoleComponent : Component
{
    // The item slot (see the entity's ItemSlots) that accepts an invoice to reload its cart.
    public const string InvoiceSlotId = "requisitions-invoice-slot";

    #region Invoice loading (server-side runtime state)

    // Bumped each time an invoice is slotted and successfully parsed. Pushed to the client via BUI state so the
    // cart is reloaded exactly once per slotted invoice, not on every background refresh.
    public int LoadedOrderToken;

    // The cart parsed from the currently slotted invoice, surfaced to the client through the state.
    public List<RequisitionCartItem> LoadedOrder = new();

    // The slotted invoice's billed total, so the client can restore its final price when loading the cart.
    public int LoadedOrderPrice;

    #endregion

    #region Caching (server-side runtime state)

    // Cached lathe half of the catalogue (the expensive part — a GetAvailableRecipes per linked lathe).
    // Null means "rebuild"; invalidated only when the recipe set can actually change (links, research/prints),
    // so the frequent material-insert refresh reuses it. The fridge half is always rebuilt fresh (cheap and live).
    public List<RequisitionCatalogueEntry>? LatheCatalogueCache;

    #endregion

    #region Linking

    // The item-printing machines (things with LatheComponent) this console dispatches prints to.
    [DataField]
    public HashSet<EntityUid> LinkedMachines = new();

    // The maximum distance a machine can be from the console and still be linkable. Mirrors the ore silo.
    [DataField]
    public float Range = 10f;

    #endregion

    #region Pricing configuration (server-authoritative, pushed to clients via BUI state)

    // Operator-set price charged per unit of a given raw material. Only materials that appear in the linked
    // machines' recipes are ever shown or priced.
    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, int> MaterialPrices = new();

    // Extra named charges (research fee, handling fee, the automatic flatpack fee, …).
    [DataField]
    public List<RequisitionFee> Fees = new();

    // Default per-material prices seeded from YAML when a material first becomes priceable.
    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, int> DefaultMaterialPrices = new();

    // Fallback price for a priceable material not listed in DefaultMaterialPrices.
    [DataField]
    public int FallbackMaterialPrice;

    // Operator-set price for a smart-fridge item, keyed by the item's identity name. Fridge items carry no
    // material cost, so their whole price is set here by hand on the fridge config tab.
    [DataField]
    public Dictionary<string, int> FridgeItemPrices = new();

    // Price charged for a fridge item whose name has no entry in FridgeItemPrices.
    [DataField]
    public int FridgeFallbackPrice;

    // When true, a printed invoice itemises each line's materials and fees plus per-order totals. When false,
    // the invoice is trimmed to just one line per item ("name — cost"), failures, and the grand total.
    [DataField]
    public bool DetailedInvoice = true;

    #endregion

    #region State

    // Number of requisition prints still in progress across the linked machines. While > 0 the console is
    // "processing a checkout" and refuses to start another one.
    [DataField]
    public int OutstandingJobs;

    #endregion

    #region Flatpack storage

    // Internal container holding printed boards waiting to be flatpacked. Boards are moved here (not held in
    // memory) so nothing is lost if packing stalls; an authorised operator can eject them from the config tab.
    [DataField]
    public string FlatpackStorageId = "requisitions-flatpack-storage";

    // Earliest time to retry feeding a flatpacker, so a stalled pack doesn't churn every tick.
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] // Persistence: TimeOffsetSerializer
    public TimeSpan NextFlatpackTry;

    #endregion

    #region Flatpack

    // Set true when at least one linked machine is a flatpack creator. Enables the flatpack column and fee.
    [DataField]
    public bool FlatpackerLinked;

    // The id of the automatic flatpack fee entry in Fees.
    [DataField]
    public string FlatpackFeeId = "Flatpack";

    // Multiplier applied to a recipe's material cost when it is flatpacked. Flatpacking is more expensive.
    [DataField]
    public float FlatpackMaterialMultiplier = 1.5f;

    #endregion
}

// An extra charge the operator can attach to some or all catalogue items.
[DataDefinition, Serializable, NetSerializable]
public sealed partial class RequisitionFee
{
    // Stable identifier for this fee (used by config messages and the flatpack fee).
    [DataField(required: true)]
    public string Id = default!;

    // Player-facing name, e.g. "Research Fee".
    [DataField]
    public string Name = string.Empty;

    // For a RequisitionFeeType.Flat fee, the flat charge in the console's currency. For a
    // RequisitionFeeType.Percent fee, the percentage added to the item's material value.
    [DataField]
    public int Price;

    // Whether Price is a flat charge or a percentage of the item's value.
    [DataField]
    public RequisitionFeeType Type = RequisitionFeeType.Flat;

    // Whether this fee belongs to the lathe (raw-material) side or the fridge side. Each config tab
    // shows only fees of its own source, and a fee only ever applies to cart lines of the same source.
    [DataField]
    public RequisitionItemSource Source = RequisitionItemSource.Lathe;

    // Which catalogue items this fee applies to.
    [DataField]
    public RequisitionFeeScope Scope = RequisitionFeeScope.Specific;

    // When Scope is RequisitionFeeScope.Specific, the ids it applies to
    // (lathe recipe ids or fridge item names, matching Source).
    [DataField]
    public HashSet<string> Targets = new();

    // How much this fee adds to a single cart line, given that line's material value (itemWorth)
    // and quantity. Percent fees scale with the value; flat fees are charged per unit.
    public int AmountFor(int itemWorth, int quantity)
    {
        return Type == RequisitionFeeType.Percent
            ? (int) MathF.Round(itemWorth * Price / 100f)
            : Price * quantity;
    }
}

[Serializable, NetSerializable]
public enum RequisitionFeeType : byte
{
    // A fixed charge, per unit.
    Flat,

    // A percentage added on top of the item's material value.
    Percent,
}

[Serializable, NetSerializable]
public enum RequisitionFeeScope : byte
{
    // Only the ids listed in RequisitionFee.Targets.
    Specific,

    // Every catalogue item.
    All,

    // Only items that are being flatpacked. Reserved for the automatic flatpack fee.
    Flatpack,
}
