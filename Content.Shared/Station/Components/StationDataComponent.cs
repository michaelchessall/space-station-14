using Content.Shared.CrewAssignments.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Station.Components;

/// <summary>
/// Stores core information about a station, namely its config and associated grids.
/// All station entities will have this component.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationDataComponent : Component
{
    /// <summary>
    /// The game map prototype, if any, associated with this station.
    /// </summary>
    [DataField]
    public StationConfig? StationConfig;

    /// <summary>
    /// List of all grids this station is part of.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Grids = new();

    /// <summary>
    /// List of all characters who can access the Station Modification Console
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> Owners = new();
    [DataField, AutoNetworkedField]
    public int UID = 0;

    [DataField, AutoNetworkedField]
    public string? StationName;

    public bool IsOwner(string owner)
    {
        if (Owners.Contains(owner)) return true;
        return false;
    }

    public void RemoveOwner(string owner)
    {
        if (!Owners.Remove(owner)) return;
        Dirty();
    }
    public void AddOwner(string owner)
    {
        if (Owners.Contains(owner)) return;
        Owners.Add(owner);
        Dirty();
    }
}
