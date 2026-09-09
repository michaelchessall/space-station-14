using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using System.Collections.Generic;

namespace Content.Shared.Radio.Components;

/// <summary>
/// This component relays radio messages to the parent entity's chat when equipped.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HeadsetComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField, AutoNetworkedField]
    public bool IsEquipped = false;

    [DataField, AutoNetworkedField]
    public SlotFlags RequiredSlot = SlotFlags.EARS;

    [DataField, AutoNetworkedField]
    public HashSet<int> TransmitTo = new();

    [DataField, AutoNetworkedField]
    public HashSet<int> RecieveFrom = new();

    [DataField, AutoNetworkedField]
    public bool ShowTags = true;

}
[Serializable, NetSerializable]
public sealed class HeadsetMenuBoundUserInterfaceState : BoundUserInterfaceState
{
    public Dictionary<int, string> FormattedStations = new();
    public HashSet<int> TransmitTo = new();
    public HashSet<int> RecieveFrom = new();
    public bool ShowTags;

    public HeadsetMenuBoundUserInterfaceState(Dictionary<int, string> formattedStations, HashSet<int> transmitTo, HashSet<int> recieveFrom, bool showTags)
    {
        FormattedStations = formattedStations;
        TransmitTo = transmitTo;
        RecieveFrom = recieveFrom;
        ShowTags = showTags;
    }
}
[Serializable, NetSerializable]
public enum HeadsetMenuUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class HeadsetMenuOutputToggle : BoundUserInterfaceMessage
{
    public int Target;
    public bool Enabled;
    public HeadsetMenuOutputToggle(int target, bool enabled)
    {
        Target = target;
        Enabled = enabled;
    }
}

[Serializable, NetSerializable]
public sealed class HeadsetMenuTagsToggle(bool enabled) : BoundUserInterfaceMessage
{
    public bool Enabled = enabled;
}

[Serializable, NetSerializable]
public sealed class HeadsetMenuInputToggle : BoundUserInterfaceMessage
{
    public int Target;
    public bool Enabled;
    public HeadsetMenuInputToggle(int target, bool enabled)
    {
        Target = target;
        Enabled = enabled;
    }
}
