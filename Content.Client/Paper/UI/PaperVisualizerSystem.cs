using Robust.Client.GameObjects;

using static Content.Shared.Paper.PaperComponent;

namespace Content.Client.Paper.UI;

public sealed class PaperVisualizerSystem : VisualizerSystem<PaperVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, PaperVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;
        if (AppearanceSystem.TryGetData<string>(uid, PaperVisuals.Invoice, out var invState, args.Component))
        {
            if (invState != string.Empty)
            {
                SpriteSystem.LayerSetRsiState((uid, args.Sprite), 0, "invoicepaid");
            }
            else
            {
                SpriteSystem.LayerSetRsiState((uid, args.Sprite), 0, "invoiceunpaid");
            }

        }
        if (AppearanceSystem.TryGetData<PaperStatus>(uid, PaperVisuals.Status, out var writingStatus, args.Component))
            SpriteSystem.LayerSetVisible((uid, args.Sprite), PaperVisualLayers.Writing, writingStatus == PaperStatus.Written);

        if (AppearanceSystem.TryGetData<string>(uid, PaperVisuals.Stamp, out var stampState, args.Component))
        {
            if (stampState != string.Empty)
            {
                SpriteSystem.LayerSetRsiState((uid, args.Sprite), PaperVisualLayers.Stamp, stampState);
                SpriteSystem.LayerSetVisible((uid, args.Sprite), PaperVisualLayers.Stamp, true);
            }
            else
            {
                SpriteSystem.LayerSetVisible((uid, args.Sprite), PaperVisualLayers.Stamp, false);
            }

        }

        if (AppearanceSystem.TryGetData<string>(uid, PaperVisuals.Signed, out var signedState, args.Component))
        {
            if (signedState != string.Empty)
            {
                SpriteSystem.LayerSetRsiState((uid, args.Sprite), PaperVisualLayers.Signature, signedState);
                SpriteSystem.LayerSetVisible((uid, args.Sprite), PaperVisualLayers.Signature, true);
            }
            else
                SpriteSystem.LayerSetVisible((uid, args.Sprite), PaperVisualLayers.Signature, true);
        }
    }
}

public enum PaperVisualLayers
{
    Stamp,
    Writing,
    Signature,
}
