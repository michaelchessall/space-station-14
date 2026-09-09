using Robust.Shared.GameStates;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;

namespace Content.Shared._Persistence14.Nutrition;

/// <summary>
/// Contains network state for InedibleFoodSequenceElementComponent.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class InedibleFoodSequenceElementComponent : Component
{
    [DataField("reagents")]
    public List<ReagentQuantity> Contents;

    [DataField("volume")]
    public FixedPoint2 Volume;
}
