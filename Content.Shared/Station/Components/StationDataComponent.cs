using Content.Shared.CrewAssignments.Prototypes;
using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using System.Text;

namespace Content.Shared.Station.Components;

/// <summary>
/// Stores core information about a station, namely its config and associated grids.
/// All station entities will have this component.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationDataComponent : Component
{
    /// <summary>
    /// Hard cap for any faction tag shown in UI, IDs, and IFF labels.
    /// </summary>
    public const int MaxFactionTagLength = 4;

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

    [DataField, AutoNetworkedField]
    public int ImportTax = 0;

    [DataField, AutoNetworkedField]
    public int ExportTax = 0;

    [DataField, AutoNetworkedField]
    public int SalesTax = 0;

    [DataField]
    public bool JobNetEnabled = true;

    [DataField]
    public ProtoId<FactionLevelPrototype> Level = "FactionLevel1";

    // Persistence 14: Replicate faction radio metadata to clients so headset and chat UI can resolve custom names, hotkeys, and colors without opening the faction console.
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<RadioChannelPrototype>, FactionRadioData> RadioData = new()
    {
        { "Common", new FactionRadioData(true) },
        { "CentCom", new FactionRadioData() },
        { "Command", new FactionRadioData() },
        { "Engineering", new FactionRadioData() },
        { "Medical", new FactionRadioData() },
        { "Science", new FactionRadioData() },
        { "Security", new FactionRadioData() },
        { "Service", new FactionRadioData() },
        { "Supply", new FactionRadioData() },
        { "Syndicate", new FactionRadioData() },
        { "Handheld", new FactionRadioData() },
        { "Binary", new FactionRadioData() },
        { "Freelance", new FactionRadioData() },
        { "Xenoborg", new FactionRadioData() },
        { "Mothership", new FactionRadioData() }
    };
    // End Persistence 14

    /// <summary>
    /// Optional custom faction tag set from the station modification console.
    /// If null or empty, we fall back to an auto-generated tag.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? FactionTag;

    /// <summary>
    /// Returns the tag that should actually be displayed to players.
    /// Prefer the configured value, otherwise derive one from the faction name.
    /// </summary>
    public string GetResolvedFactionTag(string factionName)
    {
        var configured = NormalizeFactionTag(FactionTag);
        if (!string.IsNullOrEmpty(configured))
            return configured;

        return GenerateFactionTag(factionName);
    }

    /// <summary>
    /// Sanitizes player input so the tag is compact and predictable.
    /// We strip whitespace and enforce a 4-character cap.
    /// </summary>
    public static string NormalizeFactionTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return string.Empty;

        var sb = new StringBuilder(MaxFactionTagLength);
        foreach (var ch in tag.Trim())
        {
            if (char.IsWhiteSpace(ch))
                continue;

            sb.Append(ch);
            if (sb.Length >= MaxFactionTagLength)
                break;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a default tag from the first letter of each word in the faction name.
    /// Example: "Wayfarer Dynamics" becomes "WD".
    /// </summary>
    public static string GenerateFactionTag(string factionName)
    {
        if (string.IsNullOrWhiteSpace(factionName))
            return string.Empty;

        var sb = new StringBuilder(MaxFactionTagLength);
        var words = factionName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var word in words)
        {
            if (word.Length == 0)
                continue;

            sb.Append(word[0]);
            if (sb.Length >= MaxFactionTagLength)
                break;
        }

        return sb.ToString();
    }

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

[DataDefinition]
[Serializable]
[Virtual]
public partial class FactionRadioData
{
    // Persistence 14: Store per-faction custom radio metadata separately from the shared radio prototypes so each faction can rename, recolor, and rebind its own channels.
    [DataField("_enabled")]
    public bool Enabled = false;

    [DataField]
    public bool IsCustom = false;

    [DataField]
    public char Hotkey = '\0';

    [DataField]
    public string? CustomName;

    [DataField]
    public string? CustomColor;

    [DataField("_access")]
    public List<string> Access = new();


    public FactionRadioData(bool enabled = false)
    {
        Enabled = enabled;
    }

    public Color GetColor()
    {
        if (string.IsNullOrWhiteSpace(CustomColor))
            return Color.Lime;

        return Color.TryFromHex(CustomColor) ?? Color.Lime;
    }
    // End Persistence 14
}
