using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Whether or not world generation is enabled.
    /// </summary>
    public static readonly CVarDef<bool> WorldgenEnabled =
        CVarDef.Create("worldgen.enabled", true, CVar.SERVERONLY); // can't be enabled in .toml config

    /// <summary>
    ///     The worldgen config to use.
    /// </summary>
    public static readonly CVarDef<string> WorldgenConfig =
        CVarDef.Create("worldgen.worldgen_config", "Default", CVar.SERVERONLY);

    public static readonly CVarDef<int> WorldgenSeed =
        CVarDef.Create("worldgen.seed", 1337, CVar.SERVERONLY);

    /// <summary>
    ///     How much round time in seconds must pass before a chunk is unloaded
    /// </summary>
    public static readonly CVarDef<int> WorldChunkUnloadDelay =
        CVarDef.Create("worldgen.chunk_unload_delay", 1 * 5 * 60 * 60, CVar.SERVERONLY); // 3 Days default of total Round Time
}
