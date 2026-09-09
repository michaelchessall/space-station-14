requisitions-title = Requisitions Console

# Tabs
requisitions-tab-shop = Shop
requisitions-tab-lathe-prices = Lathe Prices
requisitions-tab-fridge-prices = Fridge Prices
requisitions-tab-configuration = Configuration
requisitions-invoice-title-placeholder = Invoice title…

# Header
requisitions-linked-count = {$count} machines linked
requisitions-flatpacker-online = flatpacker: online
requisitions-flatpacker-offline = flatpacker: not linked

# Catalogue / cart
requisitions-search-placeholder = Search catalogue…
requisitions-catalogue-header = Available to print
requisitions-cart-header = Cart
requisitions-cart-empty = Cart is empty. Click items on the left to add them.
requisitions-prints-remaining = {$count} left
requisitions-available = {$count} available
requisitions-catalogue-section-fabricators = Fabricators
requisitions-catalogue-section-fridge = Smart fridge
requisitions-flatpack = flatpack

# Stock
requisitions-stock-header = Department stock (live)
requisitions-stock-used = (−{$used})
requisitions-stock-none = No material silo linked — link one on the Configuration tab to show stock.

# Checkout
requisitions-breakdown-header = Total cost breakdown
requisitions-summary-material = Material cost
requisitions-summary-fees = Fees
requisitions-summary-total = Total
requisitions-final-price = Final price
requisitions-final-price-tooltip = The price charged on the invoice. Defaults to the calculated total; type your own to override. Changing the cart resets it back to the calculated amount.
requisitions-checkout-button = Confirm & print
requisitions-cancel-button = Cancel — return my sheets
requisitions-processing = PROCESSING A CHECKOUT — please wait
requisitions-print-invoice = Print invoice
requisitions-preview-invoice = Print invoice preview
requisitions-preview-invoice-tooltip = Prints the invoice this cart would generate, without ordering or printing anything. Slot the paper back in later to reload this cart.
requisitions-preview-done = Invoice preview printed.
requisitions-preview-partial = Invoice preview printed. Some items are unknown to this console.
requisitions-invoice-unreadable = This invoice can't be read — no order to load.
requisitions-invoice-default-title = Requisition Invoice
requisitions-invoice-items = ITEMS
requisitions-invoice-total-header = TOTAL
requisitions-invoice-your-materials = your materials
requisitions-invoice-failed = FAILED
requisitions-fail-no-machine = No linked machine can print this.
requisitions-fail-no-materials = Not enough materials to print this.
requisitions-fail-no-flatpacker = No flatpacker is linked to pack this.
requisitions-fail-unknown = Unknown item.
requisitions-fail-out-of-stock = Out of stock in the linked fridge.
requisitions-fail-error = Could not be processed.
requisitions-checkout-done = Order printed.
requisitions-checkout-partial = Order printed. Some items were skipped — not enough materials.
requisitions-checkout-failed = Nothing could be printed: not enough materials.
requisitions-material-sheets = {$amount} {$unit}

# Lathe Prices tab
requisitions-config-header = Raw material & fee pricing
requisitions-config-add = + Add fee
# Configuration tab
requisitions-configuration-header = Links & invoice settings
requisitions-config-detailed-invoice = Detailed invoice
requisitions-config-detailed-invoice-tooltip = On: the invoice itemises each line's materials and fees plus totals. Off: just one line per item ("name — cost"), any failures, and the grand total.
requisitions-config-materials = Raw materials (price per sheet)
requisitions-config-fees = Fees
requisitions-config-links = Linked machines
requisitions-config-eject = Eject {$count} stored board(s)
requisitions-links-none = No linkable machines in range.
requisitions-link-linked = ✓ linked
requisitions-link-inrange = in range
requisitions-assign-button = Assign
requisitions-fee-type-tooltip = Flat charge, or a percentage of the item's value
requisitions-fee-type-flat = Flat
requisitions-fee-type-percent = Percent
requisitions-save = Save
requisitions-new-fee-name = New fee
requisitions-fee-flatpack = Flatpack Fee
requisitions-applies-all = all items
requisitions-applies-flatpack = flatpacked items
requisitions-applies-count = {$count ->
    [one] {$count} item
   *[other] {$count} items
}
requisitions-applies-none = no items yet

# Assign dialog
requisitions-dialog-assign-title = {$name} — applies to
requisitions-dialog-assign-all = Apply to every item on the checkout

# Fridge config tab
requisitions-config-fridge-header = Smart fridge item pricing
requisitions-config-fridge-prices = Fridge items (price per unit)
requisitions-config-fridge-fees = Fridge fees
requisitions-fridge-none = No smart fridge linked, or it holds no items.
requisitions-fridge-unit-price = Unit price

# Invoice slot
requisitions-invoice-slot-name = Invoice

# Access
requisitions-access-denied = Access denied.
