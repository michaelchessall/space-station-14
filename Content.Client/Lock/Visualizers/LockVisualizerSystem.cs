using Content.Shared.Lock;
using Content.Shared.Storage;
using Robust.Client.GameObjects;

namespace Content.Client.Lock.Visualizers;

public sealed class LockVisualizerSystem : VisualizerSystem<LockVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, LockVisualsComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null ||
            !AppearanceSystem.TryGetData<bool>(uid, LockVisuals.Locked, out _, args.Component) ||
            !SpriteSystem.TryGetLayer((uid, args.Sprite), LockVisualLayers.Lock, out var layer, false))
            return;

        // Lock state for the entity.
        if (!AppearanceSystem.TryGetData<bool>(uid, LockVisuals.Locked, out var locked, args.Component))
            locked = true;

        var rsi = layer.RSI ?? args.Sprite.BaseRSI; // Default to layer's RSI, if layer has none, use base RSI.
        var unlockedStateExist = rsi?.TryGetState(comp.StateUnlocked, out _) ?? false;

        if (AppearanceSystem.TryGetData<bool>(uid, StorageVisuals.Open, out var open, args.Component))
        {
            SpriteSystem.LayerSetVisible((uid, args.Sprite), LockVisualLayers.Lock, !open);
        }
        else if (!unlockedStateExist!)
            SpriteSystem.LayerSetVisible((uid, args.Sprite), LockVisualLayers.Lock, locked);

        if (!open && unlockedStateExist!)
        {
            SpriteSystem.LayerSetRsiState((uid, args.Sprite), LockVisualLayers.Lock, locked ? comp.StateLocked : comp.StateUnlocked);
        }
    }
}

public enum LockVisualLayers : byte
{
    Lock
}
