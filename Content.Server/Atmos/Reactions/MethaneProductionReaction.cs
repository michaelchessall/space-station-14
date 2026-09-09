using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class MethaneProductionReaction : IGasReactionEffect
    {
        public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
        {
            var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
            var temperature = mixture.Temperature;
            var energyReleased = 0f;

            if (mixture.Pressure < Atmospherics.SabatierMinimumPressure)
                return ReactionResult.NoReaction;

            var nCO2 = mixture.GetMoles(Gas.CarbonDioxide);
            var nH2 = mixture.GetMoles(Gas.Hydrogen);

            // CO₂ + 4H₂ -> 2H₂O + CH₄
            var limiting = MathF.Min(nCO2 / 1f, nH2 / 4f);
            var extent = limiting / Atmospherics.SabatierConversionRate;

            if (extent > Atmospherics.GasMinMoles)
            {
                mixture.AdjustMoles(Gas.CarbonDioxide, -extent * 1f);
                mixture.AdjustMoles(Gas.Hydrogen, -extent * 4f);
                mixture.AdjustMoles(Gas.Methane, extent * 1f);
                mixture.AdjustMoles(Gas.WaterVapor, extent * 2f);

                energyReleased = Atmospherics.FireMethaneEnergyReleased * extent * 0.02f;
                energyReleased /= heatScale;
            }

            if (energyReleased > 0)
            {
                var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
                if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
                    mixture.Temperature = (temperature * oldHeatCapacity + energyReleased) / newHeatCapacity;
            }

            return extent > Atmospherics.GasMinMoles ? ReactionResult.Reacting : ReactionResult.NoReaction;
        }
    }
}
