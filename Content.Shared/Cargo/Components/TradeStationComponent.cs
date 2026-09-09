using Content.Shared.Cargo.Prototypes;
using Content.Shared.HijackBeacon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Cargo.Components;

/// <summary>
/// Target for approved orders to spawn at.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TradeStationComponent : Component
{
    /// <summary>
    ///     The Trade Station's current hijack state. Modified by HijackBeaconSystem.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Hacked = false;

    [DataField, AutoNetworkedField]
    public int UID = 0;

    [DataField]
    public List<ProtoId<CargoMarketPrototype>> Markets = new()
    {
        "market"
    };
    [DataField]
    public int ExperiencePoints = 0;

    [DataField]
    public List<ProtoId<InfrastructureLevelPrototype>> Levels = new()
    {
        "ILevelGeneral1",
        "ILevelGeneral2",
        "ILevelGeneral3",
        "ILevelGeneral4"
    };
}
