using Content.Shared._Persistence14.RandomTable;
using Content.Shared._Persistence14.RandomTable.Selectors;

[RegisterComponent]
public sealed partial class AnomalyCapsuleCoreComponent : Component
{
    [DataField(required: true)]
    public RandomTableSelector AnomalyPool = new RandomTableNullSelector();
}

