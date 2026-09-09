using Robust.Shared.Noise;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using System.Numerics;

namespace Content.Server.Worldgen.Prototypes;

/// <summary>
///     This is a config for noise channels, used by worldgen.
/// </summary>
[Virtual]
public class NoiseChannelConfig
{
    /// <summary>
    ///     The noise type used by the noise generator.
    /// </summary>
    [DataField("noiseType")]
    public FastNoiseLite.NoiseType NoiseType { get; private set; } = FastNoiseLite.NoiseType.Cellular;

    [DataField("frequency")]
    public float Frequency { get; private set; } = 0.1f;

    /// <summary>
    ///     The fractal type used by the noise generator.
    /// </summary>
    [DataField("fractalType")]
    public FastNoiseLite.FractalType FractalType { get; private set; } = FastNoiseLite.FractalType.FBm;

    [DataField("cellularDistanceFunction")]
    public FastNoiseLite.CellularDistanceFunction CellularDistanceFunction { get; private set; } = FastNoiseLite.CellularDistanceFunction.Hybrid;

    [DataField("cellularReturnType")]
    public FastNoiseLite.CellularReturnType CellularReturnType { get; private set; } = FastNoiseLite.CellularReturnType.CellValue;

    [DataField("cellularJitter")]
    public float CellularJitter { get; private set; } = 1f;

    [DataField("domainWarpType")]
    public FastNoiseLite.DomainWarpType? DomainWarpType { get; private set; } = null;

    [DataField("domainWarpAmp")]
    public float DomainWarpAmp { get; private set; } = 100;

    [DataField("domainWarpFractalType")]
    public FastNoiseLite.FractalType DomainWarpFractalType { get; private set; } = FastNoiseLite.FractalType.DomainWarpIndependent;

    /// <summary>
    ///     Multiplied by pi in code when used.
    /// </summary>
    [DataField("fractalLacunarityByPi")]
    public float FractalLacunarityByPi { get; private set; } = 2.0f / 3.0f;

    [DataField("fractalOctaves")]
    public int FractalOctaves { get; private set; } = 10;

    [DataField("fractalGain")]
    public float FractalGain { get; private set; } = 0.5f;

    /// <summary>
    ///     Ranges of values that get clamped down to the "clipped" value.
    /// </summary>
    [DataField("clippingRanges")]
    public List<Vector2> ClippingRanges { get; private set; } = new();

    /// <summary>
    ///     The value clipped chunks are set to.
    /// </summary>
    [DataField("clippedValue")]
    public float ClippedValue { get; private set; }

    /// <summary>
    ///     A value the output is multiplied by.
    /// </summary>
    [DataField("outputMultiplier")]
    public float OutputMultiplier { get; private set; } = 1.0f;

    /// <summary>
    ///     A value the input is multiplied by.
    /// </summary>
    [DataField("inputMultiplier")]
    public float InputMultiplier { get; private set; } = 1.0f;

    /// <summary>
    ///     Remaps the output of the noise function from the range (-1, 1) to (0, 1). This is done before all other output
    ///     transformations.
    /// </summary>
    [DataField("remapTo0Through1")]
    public bool RemapTo0Through1 { get; private set; }

    /// <summary>
    ///     For when the transformation you need is too complex to describe in YAML.
    /// </summary>
    [DataField("noisePostProcess")]
    public NoisePostProcess? NoisePostProcess { get; private set; }

    /// <summary>
    ///     For when you need a complex transformation of the input coordinates.
    /// </summary>
    [DataField("noiseCoordinateProcess")]
    public NoiseCoordinateProcess? NoiseCoordinateProcess { get; private set; }

    /// <summary>
    ///     The "center" of the range of values. Or the minimum if mapped 0 through 1.
    /// </summary>
    [DataField("minimum")]
    public float Minimum { get; private set; }
}

[Prototype]
public sealed partial class NoiseChannelPrototype : NoiseChannelConfig, IPrototype, IInheritingPrototype
{
    /// <inheritdoc />
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<NoiseChannelPrototype>))]
    public string[]? Parents { get; private set; }

    /// <inheritdoc />
    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    /// <inheritdoc />
    [IdDataField]
    public string ID { get; private set; } = default!;
}

/// <summary>
///     A wrapper around FastNoise's noise generation, using noise channel configs.
/// </summary>
public struct NoiseGenerator
{
    private readonly NoiseChannelConfig _config;
    private readonly FastNoiseLite _noise;

    /// <summary>
    ///     Produces a new noise generator from the given channel config and rng seed.
    /// </summary>
    public NoiseGenerator(NoiseChannelConfig config, int seed)
    {
        _config = config;
        _noise = new FastNoiseLite();
        _noise.SetSeed(seed);
        _noise.SetNoiseType(_config.NoiseType);
        _noise.SetFractalType(_config.FractalType);
        _noise.SetFractalLacunarity(_config.FractalLacunarityByPi * MathF.PI);

        _noise.SetFrequency(_config.Frequency);
        _noise.SetFractalOctaves(_config.FractalOctaves);
        _noise.SetFractalGain(_config.FractalGain);
        _noise.SetCellularDistanceFunction(_config.CellularDistanceFunction);
        _noise.SetCellularReturnType(_config.CellularReturnType);
        _noise.SetCellularJitter(_config.CellularJitter);

        // Domain Warp
        if (_config.DomainWarpType != null)
            _noise.SetDomainWarpType(_config.DomainWarpType.Value);
        _noise.SetDomainWarpAmp(_config.DomainWarpAmp);
    }

    /// <summary>
    ///     Evaluates the noise generator at the provided coordinates.
    /// </summary>
    /// <param name="coords">Coordinates to use as input</param>
    /// <returns>Computed noise value</returns>
    public float Evaluate(Vector2 coords)
    {
        var finCoords = coords * _config.InputMultiplier;

        if (_config.NoiseCoordinateProcess is not null)
            finCoords = _config.NoiseCoordinateProcess.Process(finCoords);
        else if (_config.DomainWarpType is not null)
        {
            var x = finCoords.X;
            var y = finCoords.Y;
            _noise.DomainWarp(ref x, ref y);
            finCoords = new Vector2(x, y);
        }

        var value = _noise.GetNoise(finCoords.X, finCoords.Y);

        if (_config.RemapTo0Through1)
            value = (value + 1.0f) / 2.0f;

        foreach (var range in _config.ClippingRanges)
        {
            if (range.X < value && value < range.Y)
            {
                value = _config.ClippedValue;
                break;
            }
        }

        if (_config.NoisePostProcess is not null)
            value = _config.NoisePostProcess.Process(value);
        value *= _config.OutputMultiplier;
        return value + _config.Minimum;
    }
}

/// <summary>
///     A processing class that adjusts the input coordinate space to a noise channel.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class NoiseCoordinateProcess
{
    public abstract Vector2 Process(Vector2 inp);
}

/// <summary>
///     A processing class that adjusts the final result of the noise channel.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class NoisePostProcess
{
    public abstract float Process(float inp);
}

