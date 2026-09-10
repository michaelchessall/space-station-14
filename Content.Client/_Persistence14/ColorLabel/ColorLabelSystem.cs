using Content.Shared._Persistence14.ColorLabel;
using Robust.Client.GameObjects;

namespace Content.Client._Persistence14.ColorLabel;

public sealed class ColorLabelSystem : VisualizerSystem<ColorLabelComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, ColorLabelComponent component, ref AppearanceChangeEvent args)
    {
      if (!SpriteSystem.LayerMapTryGet((uid, args.Sprite), ColorLabelLayers.TargetLayer, out var layer, true))
        return;
      // TODO turn into a match statement to avoid `.Value` usage?
      if (component.AssignedColor == null)
      {
        if (component.NoColorSprite == null)
            SpriteSystem.LayerSetVisible((uid, args.Sprite), layer, false);
            // TODO is the sprite automatically made visible again later?
        else
            SpriteSystem.LayerSetRsiState((uid, args.Sprite), layer, component.NoColorSprite);
            // TODO is the color cleared automatically?
      }
      else
      {
        SpriteSystem.LayerSetRsiState((uid, args.Sprite), layer, component.YesColorSprite);
        SpriteSystem.LayerSetColor((uid, args.Sprite), layer, component.AssignedColor.Value);
      }
    }
}
