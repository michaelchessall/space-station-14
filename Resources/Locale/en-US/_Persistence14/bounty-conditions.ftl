bounty-condition-item-quantity =
    { $quantity ->
        [1] {$item}
        *[other] {$item} x{$quantity}
    }

bounty-condition-gas =
    { $moles ->
        [1] {$moles} mole of {$gas}
        *[other] {$moles} moles of {$gas}
    }

bounty-condition-reagent = {$quantity}u of {$reagent}

bounty-total-value = Total Value: [color=limegreen]${$reward}[/color] { $hasPricePer ->
        [true] ({$pricePer} per {$unit})
        *[false] { "" }
    }

bounty-condition-unit-unit = unit
bounty-condition-unit-mole = mole