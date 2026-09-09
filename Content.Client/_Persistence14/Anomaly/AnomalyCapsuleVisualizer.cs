using System.Linq;
using Content.Shared._Persistence14.Research.Anomalies;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Client._Persistence14.Anomaly;

public sealed partial class AnomalyCapsuleVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedAnomalyCapsuleSystem _capsule = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnomalyCapsuleVisualizerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AnomalyCapsuleVisualizerComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
        SubscribeLocalEvent<AnomalyCapsuleVisualizerComponent, EntRemovedFromContainerMessage>(OnItemRemoved);
    }

    public void OnStartup(Entity<AnomalyCapsuleVisualizerComponent> visualizer, ref ComponentStartup args)
    {
        UpdateVisuals(visualizer);
    }

    public void OnItemInserted(Entity<AnomalyCapsuleVisualizerComponent> visualizer, ref EntInsertedIntoContainerMessage args)
    {
        UpdateVisuals(visualizer);
    }

    public void OnItemRemoved(Entity<AnomalyCapsuleVisualizerComponent> visualizer, ref EntRemovedFromContainerMessage args)
    {
        UpdateVisuals(visualizer);
    }

    private void UpdateVisuals(Entity<AnomalyCapsuleVisualizerComponent> visualizer)
    {
        if (!TryComp<AnomalyCapsuleComponent>(visualizer, out var capsuleComp))
            return; // Visualizers should only be on capsules. But some capsules may use other/no visualizer.

        UpdateCoreVisuals((visualizer.Owner, capsuleComp, visualizer.Comp));
        UpdateModuleVisuals((visualizer.Owner, capsuleComp, visualizer.Comp));
    }

    private void UpdateCoreVisuals(Entity<AnomalyCapsuleComponent, AnomalyCapsuleVisualizerComponent> capsule)
    {
        if (!_slots.TryGetSlot(capsule.Owner, capsule.Comp1.CoreSlot, out var slot) ||
            !TryComp<AnomalyCapsuleCoreVisualizerComponent>(slot.Item, out var coreVisualizer) ||
            !TryComp<SpriteComponent>(capsule.Owner, out var spriteComp) ||
            !_sprite.TryGetLayer((capsule.Owner, spriteComp), AnomalyCapsuleVisualLayers.Core, out var layer, false))
        {
            _appearance.SetData(capsule.Owner, AnomalyCapsuleVisualData.HasCore, false);
            return;
        }

        _sprite.LayerSetSprite(layer, coreVisualizer.Core);
        _appearance.SetData(capsule.Owner, AnomalyCapsuleVisualData.HasCore, true);
    }

    private void UpdateModuleVisuals(Entity<AnomalyCapsuleComponent, AnomalyCapsuleVisualizerComponent> capsule)
    {
        var modules = _capsule.GetModules(capsule)
            .Where(p => HasComp<AnomalyCapsuleModuleVisualizerComponent>(p))
            .Select(p => (p.Owner, Comp<AnomalyCapsuleModuleVisualizerComponent>(p)))
            .ToList();
        if (modules.Count <= 0)
        {
            _appearance.SetData(capsule.Owner, AnomalyCapsuleVisualData.HasModule1, false);
            _appearance.SetData(capsule.Owner, AnomalyCapsuleVisualData.HasModule2, false);
            return;
        }

        if (modules.Count == 1)
        {
            _appearance.SetData(capsule.Owner, AnomalyCapsuleVisualData.HasModule1, true);
            _appearance.SetData(capsule.Owner, AnomalyCapsuleVisualData.HasModule2, false);
            _sprite.LayerSetColor(capsule.Owner, AnomalyCapsuleVisualLayers.Module1, modules[0].Item2.ModuleColor);
            return;
        }

        _appearance.SetData(capsule.Owner, AnomalyCapsuleVisualData.HasModule1, true);
        _appearance.SetData(capsule.Owner, AnomalyCapsuleVisualData.HasModule2, true);
        _sprite.LayerSetColor(capsule.Owner, AnomalyCapsuleVisualLayers.Module1, modules[0].Item2.ModuleColor);
        _sprite.LayerSetColor(capsule.Owner, AnomalyCapsuleVisualLayers.Module2, modules[1].Item2.ModuleColor);
    }
}

public enum AnomalyCapsuleVisualData
{
    HasCore,
    HasModule1,
    HasModule2
}

public enum AnomalyCapsuleVisualLayers
{
    Base,
    Core,
    Module1,
    Module2
}