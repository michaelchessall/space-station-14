using Content.Shared._Persistence14.EntityEffects;
using Content.Shared._Persistence14.Radiation;
using Content.Shared.EntityEffects;
using Content.Shared.Radiation.Components;
using Robust.Shared.Physics;

namespace Content.Server._Persistence14.EntityEffects;

public sealed partial class IrradiateEntityEffectSystem : EntityEffectSystem<TransformComponent, Irradiate>
{
    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<Irradiate> args)
    {
        if (!TryComp<RadiationSourceComponent>(entity.Owner, out var sourceComponent))
        {
            if (!args.Effect.AddComponentIfAbsent)
                return;

            sourceComponent = AddComp<RadiationSourceComponent>(entity.Owner);
            sourceComponent.Intensity = 0f;
        }
        sourceComponent.Intensity += args.Effect.Intensity;

        if (args.Effect.Decays)
        {
            EnsureComp<RadioactiveDecayComponent>(entity.Owner, out var decayComponent);
            decayComponent.HalfLife = args.Effect.HalfLife;
        }

        if (args.Effect.Activates)
        {
            EnsureComp<RadiationActivationComponent>(entity.Owner, out var activationComponent);
            if (activationComponent.MaxIntensity < args.Effect.MaxIntensity)
                activationComponent.MaxIntensity = args.Effect.MaxIntensity;
        }
    }
}