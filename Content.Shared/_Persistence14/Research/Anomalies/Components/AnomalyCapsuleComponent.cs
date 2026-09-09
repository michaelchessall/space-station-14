namespace Content.Shared._Persistence14.Research.Anomalies;

[RegisterComponent]
public sealed partial class AnomalyCapsuleComponent : Component
{
    [DataField(required: true)]
    public string CoreSlot = "[unknown]";

    [DataField(required: true)]
    public HashSet<string> ModuleSlots = new();
}

