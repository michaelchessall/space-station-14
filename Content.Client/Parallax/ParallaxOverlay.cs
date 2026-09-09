using System.Numerics;
using Content.Client.Parallax.Managers;
using Content.Shared.CCVar;
using Content.Shared.Parallax.Biomes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Parallax;

public sealed class ParallaxOverlay : Overlay
{
    private const float FadeDuration = 5.0f; // seconds
    private string? _lastParallax;
    private float _fadeTimer = 0f;
    private bool _fading = false;
    private float _lastFadeStartTime = 0f;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IParallaxManager _manager = default!;
    private readonly SharedMapSystem _mapSystem;
    private readonly ParallaxSystem _parallax;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public ParallaxOverlay()
    {
        ZIndex = ParallaxSystem.ParallaxZIndex;
        IoCManager.InjectDependencies(this);
        _mapSystem = _entManager.System<SharedMapSystem>();
        _parallax = _entManager.System<ParallaxSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace || _entManager.HasComponent<BiomeComponent>(_mapSystem.GetMapOrInvalid(args.MapId)))
            return false;

        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return;

        if (!_configurationManager.GetCVar(CCVars.ParallaxEnabled))
            return;

        var position = args.Viewport.Eye?.Position.Position ?? Vector2.Zero;
        var worldHandle = args.WorldHandle;
        var realTime = (float)_timing.RealTime.TotalSeconds;

        // Get current parallax name
        var currentParallax = _parallax.GetParallax(args.MapId);
        var layers = _parallax.GetParallaxLayers(args.MapId);

        // Detect parallax change and start fade
        if (_lastParallax != null && _lastParallax != currentParallax && !_fading)
        {
            _fading = true;
            _fadeTimer = 0f;
            _lastFadeStartTime = realTime;
        }

        // If fading, draw both old and new
        if (_fading && _lastParallax != null && _lastParallax != currentParallax)
        {
            var oldLayers = _parallax.GetParallaxLayersByName(_lastParallax);
            _fadeTimer = realTime - _lastFadeStartTime;
            float t = MathF.Min(_fadeTimer / FadeDuration, 1f);

            // Draw old parallax (fade out)
            DrawParallaxLayers(args, oldLayers, position, worldHandle, realTime, 1f - t);
            // Draw new parallax (fade in)
            DrawParallaxLayers(args, layers, position, worldHandle, realTime, t);

            if (t >= 1f)
            {
                _fading = false;
                _lastParallax = currentParallax;
            }
        }
        else
        {
            // Not fading, just draw normally
            DrawParallaxLayers(args, layers, position, worldHandle, realTime, 1f);
            _lastParallax = currentParallax;
        }

        worldHandle.UseShader(null);
    }

    private void DrawParallaxLayers(in OverlayDrawArgs args, ParallaxLayerPrepared[] layers, Vector2 position, DrawingHandleWorld worldHandle, float realTime, float alpha)
    {
        foreach (var layer in layers)
        {
            ShaderInstance? shader;
            if (!string.IsNullOrEmpty(layer.Config.Shader))
                shader = _prototypeManager.Index<ShaderPrototype>(layer.Config.Shader).Instance();
            else
                shader = null;

            worldHandle.UseShader(shader);
            var tex = layer.Texture;
            var size = (tex.Size / (float)EyeManager.PixelsPerMeter) * layer.Config.Scale;
            var home = layer.Config.WorldHomePosition + _manager.ParallaxAnchor;
            var scrolled = layer.Config.Scrolling * realTime;
            var originBL = (position - home) * layer.Config.Slowness + scrolled;
            originBL += home;
            originBL += layer.Config.WorldAdjustPosition;
            originBL -= size / 2;

            Color? modulate = alpha < 1f ? new Color(1f, 1f, 1f, alpha) : (Color?)null;

            if (layer.Config.Tiled)
            {
                var flooredBL = args.WorldAABB.BottomLeft - originBL;
                flooredBL = (flooredBL / size).Floored() * size;
                flooredBL += originBL;
                for (var x = flooredBL.X; x < args.WorldAABB.Right; x += size.X)
                {
                    for (var y = flooredBL.Y; y < args.WorldAABB.Top; y += size.Y)
                    {
                        worldHandle.DrawTextureRect(tex, Box2.FromDimensions(new Vector2(x, y), size), modulate);
                    }
                }
            }
            else
            {
                worldHandle.DrawTextureRect(tex, Box2.FromDimensions(originBL, size), modulate);
            }
        }
    }
}

