using Content.Shared._Persistence14.Radiation;
using Content.Shared.Radiation.Components;

namespace Content.Server.Radiation.Systems; // Breaking the rules a bit, but need to extend this namespace to add to the RadiationSystem

public sealed partial class RadiationSystem
{
    private void UpdateDecay(float deltaTime)
    {
        var decayEnts = EntityQueryEnumerator<RadioactiveDecayComponent, RadiationSourceComponent>();

        while (decayEnts.MoveNext(out var uid, out var decay, out var source))
        {
            source.Intensity = CalculateDecay(source.Intensity, deltaTime, decay.HalfLife);
        }
    }

    private static float CalculateDecay(float initialIntensity, float deltaTime, TimeSpan halfLife)
    {
        float ratio = deltaTime / (float)halfLife.TotalSeconds;
        return initialIntensity * MathF.Pow(0.5f, ratio);
    }
}