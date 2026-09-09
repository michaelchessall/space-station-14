namespace Content.Shared._Persistence14.Chemistry;

[RegisterComponent]
public sealed partial class BountySolutionComponent : Component
{
    [DataField(required: true)]
    public string Solution;
}