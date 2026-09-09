using Robust.Shared.Serialization;

namespace Content.Shared._Persistence14.Requisitions;

[Serializable, NetSerializable]
public enum RequisitionsConsoleUiKey : byte
{
    Key,
}

// Where a catalogue line / cart line / fee comes from. Dispatch, pricing, and fee scoping switch on this.
[Serializable, NetSerializable]
public enum RequisitionItemSource : byte
{
    // A recipe printed on a linked lathe. The id is a LatheRecipePrototype id.
    Lathe,

    // An item stocked in a linked smart fridge. The id is the item's identity name.
    Fridge,
}

// Everything the console UI needs, computed server-side and pushed to the client. The cart itself lives
// entirely on the client; only the final RequisitionCheckoutMessage is sent back.
[Serializable, NetSerializable]
public sealed class RequisitionsConsoleState : BoundUserInterfaceState
{
    // The joint, de-duplicated recipe list from every linked machine.
    public List<RequisitionCatalogueEntry> Catalogue = new();

    // Material id -> total amount available across the linked machines (the "department stock").
    public Dictionary<string, int> Stock = new();

    // Material id -> amount the customer has inserted into this console to lower the bill (raw units).
    public Dictionary<string, int> Contributed = new();

    // Material id -> operator-set price. Only materials used by the linked catalogue appear here.
    public Dictionary<string, int> MaterialPrices = new();

    // Fallback per-sheet price for a material with no entry in MaterialPrices. Sent so the
    // client's preview uses the same fallback the server bills with.
    public int MaterialFallbackPrice;

    // All operator-defined fees (both lathe and fridge, discriminated by RequisitionFee.Source),
    // including the automatic flatpack fee when a flatpacker is linked. Each config tab filters this by source.
    public List<RequisitionFee> Fees = new();

    // Machines that can be linked/unlinked (Configuration tab).
    public List<RequisitionLinkEntry> Linkable = new();

    public bool FlatpackerLinked;

    // Material-cost multiplier applied to flatpacked items, for client-side cost preview.
    public float FlatpackMultiplier = 1.5f;

    // A checkout's prints are still running; the shop tab is locked until they finish.
    public bool Processing;

    // Boards sitting in the console's internal storage waiting to be flatpacked (config tab).
    public int PendingFlatpacks;

    // Whether printed invoices itemise each line's materials/fees, or just show "item — cost" and a total.
    public bool DetailedInvoice = true;

    // Operator-set fridge item prices, keyed by item name (fridge config tab).
    public Dictionary<string, int> FridgeItemPrices = new();

    // Incremented each time a slotted invoice is parsed into a cart. The client applies LoadedOrder once per
    // new token value.
    public int LoadedOrderToken;

    // The cart parsed from the most recently slotted invoice, applied by the client on a new token.
    public List<RequisitionCartItem> LoadedOrder = new();

    // The slotted invoice's billed total, restored as the final price when the cart is loaded.
    public int LoadedOrderPrice;

    // Whether an invoice is currently sitting in the console's invoice slot.
    public bool InvoiceSlotted;
}

// One catalogue line: a single recipe, merged across every machine that can print it.
[Serializable, NetSerializable]
public sealed class RequisitionCatalogueEntry
{
    // Lathe recipe id, or fridge item name, per Source.
    public string Id = string.Empty;
    public string Name = string.Empty;

    // Result entity prototype id, used to draw the icon. Null for reagent-only recipes.
    public string? Result;

    // Raw material -> amount required (before any flatpack multiplier).
    public Dictionary<string, int> Materials = new();

    // True if at least one linked flatpacker can flatpack this item.
    public bool Flatpackable;

    // Remaining research prints for a limited recipe, or null if it's unlimited (static).
    public int? PrintsRemaining;

    // How many linked machines can print this (for display; duplicates are squashed to one line).
    public int SourceCount;

    // Whether this line is a lathe recipe or a smart-fridge item. Drives dispatch, pricing, and styling.
    public RequisitionItemSource Source;

    // For a fridge item, how many are currently stocked across the linked fridges. Null for lathe items.
    public int? Available;

    // For a fridge item, the operator-set unit price (fridge items carry no material cost).
    public int FridgeUnitPrice;

    // Convenience derived from Source (not serialized).
    public bool FromFridge => Source == RequisitionItemSource.Fridge;
}

// A machine that can be linked to the console (shown in the config tab).
[Serializable, NetSerializable]
public sealed class RequisitionLinkEntry
{
    public NetEntity Machine;
    public string Label = string.Empty;
    public bool Linked;
    public bool InRange;
    public bool Flatpacker;
}

// A single line the customer is buying. Id is a lathe recipe id or a fridge item name
// depending on Source.
[Serializable, NetSerializable]
public struct RequisitionCartItem
{
    public string Id;
    public RequisitionItemSource Source;
    public int Quantity;
    public bool Flatpack;
}

// ---------------------------------------------------------------------------
// Customer messages
// ---------------------------------------------------------------------------

// Sent when the customer confirms their cart. Any raw materials the customer physically inserted into the
// console beforehand are applied automatically to lower the bill.
[Serializable, NetSerializable]
public sealed class RequisitionCheckoutMessage : BoundUserInterfaceMessage
{
    public List<RequisitionCartItem> Items;

    // Whether to print a payable invoice for this order.
    public bool PrintInvoice;

    // Title the customer typed for the invoice.
    public string InvoiceTitle;

    // A price the operator manually set for this order, or null to bill the calculated amount. Only sent when the
    // operator actually overrode it, so a normal checkout still bills exactly what printed.
    public int? OverridePrice;

    public RequisitionCheckoutMessage(List<RequisitionCartItem> items, bool printInvoice, string invoiceTitle, int? overridePrice)
    {
        Items = items;
        PrintInvoice = printInvoice;
        InvoiceTitle = invoiceTitle;
        OverridePrice = overridePrice;
    }
}

// The customer changed their mind: the sheets they inserted toward this order are returned. Not access-gated.
[Serializable, NetSerializable]
public sealed class RequisitionCancelMessage : BoundUserInterfaceMessage
{
}

// Print the invoice this cart would generate, without dispatching any prints or dispensing anything. The
// resulting paper can be slotted back into a console to reload the cart. Always prints regardless of the
// checkout tab's "print invoice" toggle.
[Serializable, NetSerializable]
public sealed class RequisitionPreviewInvoiceMessage : BoundUserInterfaceMessage
{
    public List<RequisitionCartItem> Items;
    public string InvoiceTitle;
    public int? OverridePrice;

    public RequisitionPreviewInvoiceMessage(List<RequisitionCartItem> items, string invoiceTitle, int? overridePrice)
    {
        Items = items;
        InvoiceTitle = invoiceTitle;
        OverridePrice = overridePrice;
    }
}

// ---------------------------------------------------------------------------
// Operator (access-gated) messages — the server re-checks access on every one.
// ---------------------------------------------------------------------------

// Link or unlink a nearby printing machine.
[Serializable, NetSerializable]
public sealed class ToggleRequisitionLinkMessage : BoundUserInterfaceMessage
{
    public NetEntity Machine;

    public ToggleRequisitionLinkMessage(NetEntity machine)
    {
        Machine = machine;
    }
}

// Set (or clear, when price < 0) the price of a raw material.
[Serializable, NetSerializable]
public sealed class RequisitionSetMaterialPriceMessage : BoundUserInterfaceMessage
{
    public string Material;
    public int Price;

    public RequisitionSetMaterialPriceMessage(string material, int price)
    {
        Material = material;
        Price = price;
    }
}

// Add a new fee or edit an existing one (matched by RequisitionFee.Id).
[Serializable, NetSerializable]
public sealed class RequisitionSetFeeMessage : BoundUserInterfaceMessage
{
    public RequisitionFee Fee;

    public RequisitionSetFeeMessage(RequisitionFee fee)
    {
        Fee = fee;
    }
}

// Remove a fee by id. The automatic flatpack fee cannot be removed.
[Serializable, NetSerializable]
public sealed class RequisitionRemoveFeeMessage : BoundUserInterfaceMessage
{
    public string Id;

    public RequisitionRemoveFeeMessage(string id)
    {
        Id = id;
    }
}

// Set whether printed invoices are fully itemised or trimmed to one line per item plus a total.
[Serializable, NetSerializable]
public sealed class RequisitionSetDetailedInvoiceMessage : BoundUserInterfaceMessage
{
    public bool Detailed;

    public RequisitionSetDetailedInvoiceMessage(bool detailed)
    {
        Detailed = detailed;
    }
}

// Eject any boards stuck in the internal flatpack storage back into the world.
[Serializable, NetSerializable]
public sealed class RequisitionEjectFlatpacksMessage : BoundUserInterfaceMessage
{
}

// Set (or clear, when price < 0) the manual price of a smart-fridge item, keyed by item name.
[Serializable, NetSerializable]
public sealed class RequisitionSetFridgePriceMessage : BoundUserInterfaceMessage
{
    public string Item;
    public int Price;

    public RequisitionSetFridgePriceMessage(string item, int price)
    {
        Item = item;
        Price = price;
    }
}

