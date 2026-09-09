namespace Content.Shared._Persistence14.Research.Anomalies;

[RegisterComponent]
public sealed partial class AnomalyCapsuleModuleVisualizerComponent : Component
{
    [DataField("color", required: true)]
    public Color ModuleColor = Color.White;

    /// <summary>
    /// Sprite layers to modify the color of for the module entity.
    /// </summary>
    [DataField("moduleLayers")]
    public HashSet<Enum> ModuleApplyColorLayers = new();
}

public enum AnomalyCapsuleModuleVisualLayers
{
    Chip,
    Prong
}