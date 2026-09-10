using Robust.Shared.Serialization;
namespace Content.Shared._Persistence14.ColorLabel;

[RegisterComponent]
public sealed partial class ColorLabelComponent : Component
{
    /// <summary>
    /// Current color of the label. Has transparency.
    /// </summary>
    [DataField]
    public Color? AssignedColor = null;

    /// <summary>
    /// Sprite that gets recolored by the player.
    /// Uses `SpriteSystem.LayerSetColor`, so you probably want this to be mostly white.
    /// </summary>
    [DataField]
    public String? YesColorSprite = null;

    /// <summary>
    /// Sprite used when the assigned color is null.
    /// </summary>
    [DataField]
    public String? NoColorSprite = null;
}

[Serializable, NetSerializable]
public enum ColorLabelLayers : byte
{
    TargetLayer
}
