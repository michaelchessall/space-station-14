using Content.Shared._Persistence14.Research.Anomalies;
using Robust.Client.GameObjects;

namespace Content.Client._Persistence14.Anomaly;

public sealed partial class AnomalyCapsuleModuleVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnomalyCapsuleModuleVisualizerComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<AnomalyCapsuleModuleVisualizerComponent> visualizer, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(visualizer.Owner, out var spriteComp))
            return;

        Entity<SpriteComponent?> sprite = (visualizer.Owner, spriteComp);

        foreach (var layer in visualizer.Comp.ModuleApplyColorLayers)
        {
            _sprite.LayerSetColor(sprite, layer, visualizer.Comp.ModuleColor);
        }
    }
}