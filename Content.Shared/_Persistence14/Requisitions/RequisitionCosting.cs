using System;
using System.Collections.Generic;

namespace Content.Shared._Persistence14.Requisitions;

// Pricing math shared by the client's cost preview and the server's invoice billing. Each material's sheet
// cost is rounded once per line by SheetCost, and per-line costs are summed.
public static class RequisitionCosting
{
    // Raw material needed for one unit after the flatpack multiplier, rounded up.
    public static int PerUnitRaw(int baseAmount, float flatpackMultiplier)
    {
        return (int) MathF.Ceiling(baseAmount * flatpackMultiplier);
    }

    // Cost of a raw material amount, charged per sheet.
    public static int SheetCost(int rawAmount, float sheetVolume, int pricePerSheet)
    {
        if (rawAmount <= 0)
            return 0;
        if (sheetVolume <= 0)
            sheetVolume = 1;
        return (int) MathF.Round(rawAmount / sheetVolume * pricePerSheet);
    }

    // Price of a material, or the fallback when the material has no entry.
    public static int Price(IReadOnlyDictionary<string, int> prices, string material, int fallback)
    {
        return prices.TryGetValue(material, out var p) ? p : fallback;
    }
}
