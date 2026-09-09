using Content.Shared._Persistence14.Atmos.Geyser;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Prototypes;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client._Persistence14.Atmos.Geyser;

public sealed partial class GasGeyserSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;

    public override void Initialize()
    {
        SubscribeNetworkEvent<GasGeyserErruptedEvent>(OnErupt);
    }

    private void OnErupt(GasGeyserErruptedEvent args)
    {
        var uid = GetEntity(args.Geyser);

        if (!Exists(uid) ||
            !TryComp<GasGeyserComponent>(uid, out var geyser) ||
            !TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!_sprite.TryGetLayer((uid, sprite), GasGeyserVisualizationLayer.Gas, out var layer, false))
            return;

        layer.Loop = false;
        _sprite.LayerSetAutoAnimated(layer, true);

        _sprite.LayerSetRsiState(
            layer,
            new RSI.StateId(geyser.ErruptionAnimationKey),
            refresh: true
        );
        _sprite.LayerSetColor(layer, AverageGasColor(geyser.Moles));

    }

    private Color AverageGasColor(float[] moles)
    {
        var totalMoles = 0f;
        var r = 0f;
        var g = 0f;
        var b = 0f;
        var a = 0f;

        for (int i = 0; i < moles.Length; i++)
        {
            var gasMoles = moles[i];

            if (gasMoles <= 0)
                continue;

            var gas = (Gas)i;

            if (!_protoMan.TryIndex<GasPrototype>(gas.ToString(), out var gasProto))
                continue;
            var color = ParseGasColor(gasProto.Color);
            r += color.R * gasMoles;
            g += color.G * gasMoles;
            b += color.B * gasMoles;
            a += color.A * gasMoles;
            totalMoles += gasMoles;
        }

        if (totalMoles <= 0) return Color.White;

        return new Color(r / totalMoles, g / totalMoles, b / totalMoles, a / totalMoles);
    }

    /// <summary>
    /// Ensures a given string matches properly with the required shape for parsing a color string into a color object.
    /// </summary>
    private static Color ParseGasColor(string hex)
    {
        if (!hex.StartsWith('#'))
            hex = $"#{hex}";

        return Color.FromHex(hex);
    }
}

public enum GasGeyserVisualizationLayer
{
    Hole,
    Gas
}