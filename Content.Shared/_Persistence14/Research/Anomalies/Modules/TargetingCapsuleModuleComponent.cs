using Content.Shared._Persistence14.PersistentIdentifier;
using Content.Shared._Persistence14.PersistentIdentifier.Reference;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Persistence14.Research.Anomalies.Modules;

[RegisterComponent]
public sealed partial class TargetingCapsuleModuleComponent : Component
{
    [DataField]
    public PersistentEntityReference Target;

    [DataField]
    public SoundSpecifier ConnectSound = new SoundPathSpecifier("/Audio/Items/Mining/fultext_deploy.ogg");
}

[RegisterComponent]
public sealed partial class TargetingModuleTargetComponent : Component { }

public sealed partial class TargetingCapsuleModuleSystem : AnomalyCapsuleModuleSystem<TargetingCapsuleModuleComponent>
{
    [Dependency] private readonly PersistentIdentifierSystem _pid = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _gameTime = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TargetingCapsuleModuleComponent, AfterInteractEvent>(OnAfterInteract);
    }

    protected override void OnGenerationAttempt(Entity<TargetingCapsuleModuleComponent> module, ref AnomalyGeneratorAttemptEvent args)
    {
        if (!_pid.TryResolveId(module.Comp.Target, out var target))
        {
            args.Cancel();
            return;
        }

        if (!TryComp<TargetingModuleTargetComponent>(target.Owner, out var targetComp))
        {
            LogManager.GetSawmill(SharedAnomalyCapsuleSystem.Sawmill).Error($"Capsule target {ToPrettyString(target.Owner)} is an invalid anomaly spawn target.");
            args.Cancel();
            return;
        }

        var transform = Transform(target);
        args.Context.TargetCoordinates = transform.Coordinates;
    }

    private void OnAfterInteract(Entity<TargetingCapsuleModuleComponent> module, ref AfterInteractEvent args)
    {
        if (args.Target is not { } target)
            return;
        if (!TryComp<TargetingModuleTargetComponent>(args.Target, out var targetingComp))
        {
            if (_gameTime.IsFirstTimePredicted && _net.IsClient)
                _popup.PopupEntity(Loc.GetString("anomaly-capsule-targeting-module-failed"), target);
            return;
        }

        _pid.AssignIdReference(ref module.Comp.Target, target);
        if (_gameTime.IsFirstTimePredicted && _net.IsClient)
        {
            _popup.PopupEntity(Loc.GetString("anomaly-capsule-targeting-module-connected"), target);
            _audio.PlayPvs(module.Comp.ConnectSound, target);
        }
    }
}