using Content.Server.Popups;
using Content.Server.Singularity.Events;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Singularity.Components;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server.Singularity.EntitySystems;

public sealed class ContainmentFieldSystem : EntitySystem
{
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContainmentFieldComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ContainmentFieldComponent, StartCollideEvent>(HandleFieldCollide);
        SubscribeLocalEvent<ContainmentFieldComponent, EventHorizonAttemptConsumeEntityEvent>(HandleEventHorizon);
    }

    private void OnStartup(EntityUid uid, ContainmentFieldComponent component, ComponentStartup args)
    {
        if (HasActiveConnection(uid, component))
            return;

        Timer.Spawn(component.ConnectionTimeout, () => RemoveIfStillDisconnected(uid));
    }

    private void RemoveIfStillDisconnected(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid) || !TryComp<ContainmentFieldComponent>(uid, out var component))
            return;

        if (HasActiveConnection(uid, component))
            return;

        QueueDel(uid);
    }

    private bool HasActiveConnection(EntityUid uid, ContainmentFieldComponent component)
    {
        if (component.GeneratorUid is not { Valid: true } generatorUid)
            return false;

        if (!TryComp<ContainmentFieldGeneratorComponent>(generatorUid, out var generator))
            return false;

        foreach (var (_, (_, fields)) in generator.Connections)
        {
            if (fields.Contains(uid))
                return true;
        }

        return false;
    }

    private void HandleFieldCollide(EntityUid uid, ContainmentFieldComponent component, ref StartCollideEvent args)
    {
        var otherBody = args.OtherEntity;

        if (component.DestroyGarbage && HasComp<SpaceGarbageComponent>(otherBody))
        {
            _popupSystem.PopupEntity(Loc.GetString("comp-field-vaporized", ("entity", otherBody)), uid, PopupType.LargeCaution);
            QueueDel(otherBody);
        }

        if (TryComp<PhysicsComponent>(otherBody, out var physics) && physics.Mass <= component.MaxMass && physics.Hard)
        {
            var fieldDir = _transformSystem.GetWorldPosition(uid);
            var playerDir = _transformSystem.GetWorldPosition(otherBody);

            _throwing.TryThrow(otherBody, playerDir - fieldDir, baseThrowSpeed: component.ThrowForce);
        }
    }

    private void HandleEventHorizon(EntityUid uid, ContainmentFieldComponent component, ref EventHorizonAttemptConsumeEntityEvent args)
    {
        if (args.Cancelled)
            return;

        if (!args.EventHorizon.CanBreachContainment)
        {
            args.Cancelled = true;
            return;
        }

        if (component.GeneratorUid is not { Valid: true } generatorUid)
            return;

        if (!TryComp<ContainmentFieldGeneratorComponent>(generatorUid, out var generator))
            return;

        if (generator.IsConnected && generator.PowerBuffer * 2 >= ContainmentFieldGeneratorComponent.MaxPowerBuffer)
            args.Cancelled = true;
    }
}
