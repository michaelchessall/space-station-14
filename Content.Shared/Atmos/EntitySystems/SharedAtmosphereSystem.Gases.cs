using Content.Shared.Atmos.Prototypes;
using Content.Shared.Atmos.Reactions;
using Content.Shared.CCVar;
using JetBrains.Annotations;
using Robust.Shared.Maths;
using System.Runtime.CompilerServices;

namespace Content.Shared.Atmos.EntitySystems;

public abstract partial class SharedAtmosphereSystem
{
    /*
     Partial class for operations involving GasMixtures.

     Sometimes methods here are abstract because they need different client/server implementations
     due to sandboxing.
     */

    /// <summary>
    /// Cached array of gas specific heats.
    /// </summary>
    public float[] GasSpecificHeats => _gasSpecificHeats;
    private float[] _gasSpecificHeats = new float[Atmospherics.TotalNumberOfGases];

    /// <summary>
    /// Mask used to determine if a gas is flammable or not.
    /// </summary>
    /// <para>This is used to quickly determine if a <see cref="GasMixture"/> contains any flammable gas.
    /// When determining flammability, the float is multiplied with the mask and then
    /// added to see if the mixture is flammable, and how many moles are considered flammable.</para>
    /// <para>This is done instead of a massive if statement of doom everywhere.</para>
    /// <example><para>Say Plasma has the <see cref="GasPrototype.IsFuel"/> bool set to true.
    /// Atmospherics will place a 1 in the spot where plasma goes in the masking array.
    /// Whenever we need to determine if a GasMixture contains fuel gases, we multiply the
    /// gas array by the mask. Fuel gases will keep their value (being multiplied by one)
    /// whereas non-fuel gases will be multiplied by zero and be zeroed out.
    /// The resulting array can be HorizontalAdded, with any value above zero indicating fuel gases.</para>
    /// <para>This works for multiple fuel gases at the same time, so it's a fairly quick way
    /// to determine if a mixture has the gases we care about.</para></example>
    protected readonly float[] GasFuelMask = new float[Atmospherics.AdjustedNumberOfGases];

    /// <summary>
    /// Mask used to determine if a gas is an oxidizer or not.
    /// <para>Used in the same way as <see cref="GasFuelMask"/>.
    /// Nothing really super special.</para>
    /// </summary>
    protected readonly float[] GasOxidizerMask = new float[Atmospherics.AdjustedNumberOfGases];

    /// <summary>
    /// Mask used to determine both fuel and oxidizer properties of a gas at the same time.
    /// Primarily used to quickly report the specific moles in a mixture that caused a flammable reaction to occur.
    /// </summary>
    protected readonly float[] GasOxidiserFuelMask = new float[Atmospherics.TotalNumberOfGases];

    public string?[] GasReagents = new string[Atmospherics.TotalNumberOfGases];
    protected readonly GasPrototype[] GasPrototypes = new GasPrototype[Atmospherics.TotalNumberOfGases];

    public virtual void InitializeGases()
    {
        foreach (var gas in Enum.GetValues<Gas>())
        {
            var idx = (int)gas;
            // Log an error if the corresponding prototype isn't found
            if (!ProtoMan.TryIndex<GasPrototype>(gas.ToString(), out var gasPrototype))
            {
                Log.Error($"Failed to find corresponding {nameof(GasPrototype)} for gas ID {(int)gas} ({gas}) with expected ID \"{gas.ToString()}\". Is your prototype named correctly?");
                continue;
            }
            GasPrototypes[idx] = gasPrototype;
            GasReagents[idx] = gasPrototype.Reagent;
        }

        Array.Resize(ref _gasSpecificHeats, MathHelper.NextMultipleOf(Atmospherics.TotalNumberOfGases, 4));

        for (var i = 0; i < GasPrototypes.Length; i++)
        {
            /*
             As an optimization routine we pre-divide the specific heat by the heat scale here,
             so we don't have to do it every time we calculate heat capacity.
             Most usages are going to want the scaled value anyway.

             If you would like the unscaled specific heat, you'd need to multiply by HeatScale again.
             TODO ATMOS: please just make this 2 separate arrays instead of invoking multiplication every time.
             */
            _gasSpecificHeats[i] = GasPrototypes[i].SpecificHeat / HeatScale;

            // """Mask""" built here. Used to determine if a gas is fuel/oxidizer or not decently quickly and clearly.
            GasFuelMask[i] = GasPrototypes[i].IsFuel ? 1 : 0;

            // Same for oxidizer mask.
            GasOxidizerMask[i] = GasPrototypes[i].IsOxidizer ? 1 : 0;

            // OxidiserFuel mask is just fuel and oxidizer combined, because both are required for a reaction to occur.
            GasOxidiserFuelMask[i] = GasFuelMask[i] * GasOxidizerMask[i];
        }
    }

    /// <summary>
    /// Gets only the moles that are considered a fuel and an oxidizer in a <see cref="GasMixture"/>.
    /// </summary>
    /// <param name="mixture">The <see cref="GasMixture"/> to get the flammable moles for.</param>
    /// <param name="buffer">A buffer to write the flammable moles into. Must be the same length as the number of gases.</param>
    /// <returns>A <see cref="Span{T}"/> of moles where only the flammable and oxidizer moles are returned, and the rest are 0.</returns>
    [PublicAPI]
    public void GetFlammableMoles(GasMixture mixture, float[] buffer)
    {
        NumericsHelpers.Multiply(mixture.Moles, GasOxidiserFuelMask, buffer);
    }

    /// <summary>
    /// Computes the blended fire color for a gas mixture based on the proportional
    /// moles of each reactive gas present (fuels and oxidizers with burn colors).
    /// Mixing is performed in Oklab perceptual color space for smooth, natural blends.
    /// </summary>
    [PublicAPI]
    public Color GetFuelBurnColor(GasMixture mixture)
    {
        var defaultColor = Color.FromHex("#FFB733");
        var totalMoles = 0f;
        var labL = 0f;
        var labA = 0f;
        var labB = 0f;
        var alpha = 0f;

        for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
        {
            if (GasFuelMask[i] == 0 && GasOxidizerMask[i] == 0)
                continue;

            var moles = mixture.GetMoles(i);
            if (moles <= Atmospherics.Epsilon)
                continue;

            var burnColor = GasPrototypes[i].BurnColor;
            if (burnColor == defaultColor)
                continue;

            SrgbToOklab(burnColor, out var gL, out var gA, out var gB);
            totalMoles += moles;
            labL += gL * moles;
            labA += gA * moles;
            labB += gB * moles;
            alpha += burnColor.A * moles;
        }

        if (totalMoles <= Atmospherics.Epsilon)
            return defaultColor;

        var result = OklabToSrgb(labL / totalMoles, labA / totalMoles, labB / totalMoles);
        result.A = alpha / totalMoles;
        return result;
    }

    /// <summary>
    /// Converts an sRGB <see cref="Color"/> (0-1 floats) to Oklab (L, a, b).
    /// </summary>
    private static void SrgbToOklab(Color c, out float L, out float a, out float b)
    {
        // sRGB → linear
        var lr = SrgbToLinear(c.R);
        var lg = SrgbToLinear(c.G);
        var lb = SrgbToLinear(c.B);

        // Linear RGB → LMS
        var l = 0.4122214708f * lr + 0.5363325363f * lg + 0.0514459929f * lb;
        var m = 0.2119034982f * lr + 0.6806995451f * lg + 0.1073969566f * lb;
        var s = 0.0883024619f * lr + 0.2817188376f * lg + 0.6299787005f * lb;

        // Cube root
        var lc = MathF.Cbrt(l);
        var mc = MathF.Cbrt(m);
        var sc = MathF.Cbrt(s);

        // LMS → Oklab
        L = 0.2104542553f * lc + 0.7936177850f * mc - 0.0040720468f * sc;
        a = 1.9779984951f * lc - 2.4285922050f * mc + 0.4505937099f * sc;
        b = 0.0259040371f * lc + 0.7827717662f * mc - 0.8086757660f * sc;
    }

    /// <summary>
    /// Converts Oklab (L, a, b) back to an sRGB <see cref="Color"/>.
    /// </summary>
    private static Color OklabToSrgb(float L, float a, float b)
    {
        // Oklab → LMS (cube-root space)
        var lc = L + 0.3963377774f * a + 0.2158037573f * b;
        var mc = L - 0.1055613458f * a - 0.0638541728f * b;
        var sc = L - 0.0894841775f * a - 1.2914855480f * b;

        // Undo cube root
        var l = lc * lc * lc;
        var m = mc * mc * mc;
        var s = sc * sc * sc;

        // LMS → linear RGB
        var lr = +4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s;
        var lg = -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s;
        var lb = -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s;

        // linear → sRGB, clamped
        return new Color(
            LinearToSrgb(lr),
            LinearToSrgb(lg),
            LinearToSrgb(lb));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SrgbToLinear(float c)
    {
        return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float LinearToSrgb(float c)
    {
        c = Math.Clamp(c, 0f, 1f);
        return c <= 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1f / 2.4f) - 0.055f;
    }

    /// <summary>
    /// Determines if a <see cref="GasMixture"/> is ignitable or not.
    /// This is a combination of determining if a mixture both has oxidizer and fuel.
    /// </summary>
    /// <param name="mixture">The <see cref="GasMixture"/> to determine.</param>
    /// <param name="epsilon">The minimum amount of moles at which a <see cref="GasMixture"/> is
    /// considered ignitable, for both oxidizer and fuel.</param>
    /// <returns>True if the <see cref="GasMixture"/> is ignitable, otherwise, false.</returns>
    [PublicAPI]
    public bool IsMixtureIgnitable(GasMixture mixture, float epsilon = Atmospherics.Epsilon)
    {
        return IsMixtureFuel(mixture, epsilon) && IsMixtureOxidizer(mixture, epsilon);
    }

    /// <summary>
    /// Determines if a <see cref="GasMixture"/> has fuel gases in it or not.
    /// </summary>
    /// <param name="mixture">The <see cref="GasMixture"/> to determine.</param>
    /// <param name="epsilon">The minimum amount of moles at which a <see cref="GasMixture"/>
    /// is considered fuel.</param>
    /// <returns>True if the <see cref="GasMixture"/> is fuel, otherwise, false.</returns>
    [PublicAPI]
    public abstract bool IsMixtureFuel(GasMixture mixture, float epsilon = Atmospherics.Epsilon);

    /// <summary>
    /// Determines if a <see cref="GasMixture"/> has oxidizer gases in it or not.
    /// </summary>
    /// <param name="mixture">The <see cref="GasMixture"/> to determine.</param>
    /// <param name="epsilon">The minimum amount of moles at which a <see cref="GasMixture"/>
    /// is considered an oxidizer.</param>
    /// <returns>True if the <see cref="GasMixture"/> is an oxidizer, otherwise, false.</returns>
    [PublicAPI]
    public abstract bool IsMixtureOxidizer(GasMixture mixture, float epsilon = Atmospherics.Epsilon);

    /// <summary>
    /// Calculates the heat capacity for a <see cref="GasMixture"/>.
    /// </summary>
    /// <param name="mixture">The <see cref="GasMixture"/> to calculate the heat capacity for.</param>
    /// <param name="applyScaling">Whether to apply the heat capacity scaling factor.
    /// This is an extremely important boolean to consider or else you will get heat transfer wrong.
    /// See <see cref="CCVars.AtmosHeatScale"/> for more info.</param>
    /// <returns>The heat capacity of the <see cref="GasMixture"/>.</returns>
    [PublicAPI]
    public float GetHeatCapacity(GasMixture mixture, bool applyScaling)
    {
        var scale = GetHeatCapacityCalculation(mixture.Moles, mixture.Immutable);

        // By default GetHeatCapacityCalculation() has the heat-scale divisor pre-applied.
        // So if we want the un-scaled heat capacity, we have to multiply by the scale.
        return applyScaling ? scale : scale * HeatScale;
    }

    /// <summary>
    /// Calculates the thermal energy for a <see cref="GasMixture"/>.
    /// </summary>
    /// <param name="mixture">The <see cref="GasMixture"/> to calculate the thermal
    /// energy of.</param>
    /// <returns>The <see cref="GasMixture"/>'s thermal energy in joules.</returns>
    [PublicAPI]
    public float GetThermalEnergy(GasMixture mixture)
    {
        return mixture.Temperature * GetHeatCapacity(mixture);
    }

    /// <summary>
    /// Calculates the thermal energy for a gas mixture,
    /// using a provided cached heat capacity value.
    /// </summary>
    /// <param name="mixture">The <see cref="GasMixture"/> to calculate the thermal energy of.</param>
    /// <param name="cachedHeatCapacity">A cached heat capacity value for the gas mixture,
    /// to avoid redundant heat capacity calculations.</param>
    /// <returns>The <see cref="GasMixture"/>'s thermal energy in joules.</returns>
    [PublicAPI]
    public float GetThermalEnergy(GasMixture mixture, float cachedHeatCapacity)
    {
        return mixture.Temperature * cachedHeatCapacity;
    }

    /// <summary>
    /// Merges one <see cref="GasMixture"/> into another, modifying the receiver.
    /// </summary>
    /// <param name="receiver">The <see cref="GasMixture"/> to merge into. This will be modified.</param>
    /// <param name="giver">The <see cref="GasMixture"/> to merge from. This will not be modified.</param>
    [PublicAPI]
    public void Merge(GasMixture receiver, GasMixture giver)
    {
        if (receiver.Immutable)
            return;

        if (MathF.Abs(receiver.Temperature - giver.Temperature) > Atmospherics.MinimumTemperatureDeltaToConsider)
        {
            var receiverHeatCapacity = GetHeatCapacity(receiver);
            var giverHeatCapacity = GetHeatCapacity(giver);
            var combinedHeatCapacity = receiverHeatCapacity + giverHeatCapacity;
            if (combinedHeatCapacity > Atmospherics.MinimumHeatCapacity)
            {
                receiver.Temperature = (GetThermalEnergy(giver, giverHeatCapacity) + GetThermalEnergy(receiver, receiverHeatCapacity)) / combinedHeatCapacity;
            }
        }

        NumericsHelpers.Add(receiver.Moles, giver.Moles);
    }

    /// <summary>
    /// Performs reactions for a given gas mixture on an optional holder.
    /// </summary>
    /// <param name="mixture">The <see cref="GasMixture"/> to perform reactions on.</param>
    /// <param name="holder"><see cref="IGasMixtureHolder"/> that holds the <see cref="GasMixture"/>.
    /// used by Atmospherics to determine locality for certain reaction effects.</param>
    /// <returns>The <see cref="ReactionResult"/> of the reactions performed.</returns>
    [PublicAPI]
    public abstract ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder);

    /// <summary>
    /// Gets the heat capacity for a <see cref="GasMixture"/>.
    /// </summary>
    /// <param name="mixture">The <see cref="GasMixture"/> to calculate the heat capacity for.</param>
    /// <returns>The heat capacity of the <see cref="GasMixture"/>.</returns>
    /// <remarks>Note that the heat capacity of the mixture may be slightly different from
    /// "real life" as we intentionally fake a heat capacity for space in <see cref="Atmospherics.SpaceHeatCapacity"/>
    /// in order to allow Atmospherics to cool down space.</remarks>
    protected float GetHeatCapacity(GasMixture mixture)
    {
        return GetHeatCapacityCalculation(mixture.Moles, mixture.Immutable);
    }

    /// <summary>
    /// Gets the heat capacity for a <see cref="GasMixture"/>.
    /// </summary>
    /// <param name="moles">The moles array of the <see cref="GasMixture"/></param>
    /// <param name="space">Whether this <see cref="GasMixture"/> represents space,
    /// and thus experiences space-specific mechanics (we cheat and make it a bit cooler).
    /// See <see cref="Atmospherics.SpaceHeatCapacity"/>.</param>
    /// <returns>The heat capacity of the <see cref="GasMixture"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected abstract float GetHeatCapacityCalculation(float[] moles, bool space);
}
