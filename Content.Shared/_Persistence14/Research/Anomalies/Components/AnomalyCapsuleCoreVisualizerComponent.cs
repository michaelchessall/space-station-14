using Robust.Shared.Utility;


namespace Content.Shared._Persistence14.Research.Anomalies;

[RegisterComponent]
public sealed partial class AnomalyCapsuleCoreVisualizerComponent : Component
{
    [DataField("core", required: true)]
    public SpriteSpecifier Core = default!;
}