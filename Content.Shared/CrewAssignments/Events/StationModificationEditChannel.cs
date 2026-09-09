using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.Events;

[Serializable, NetSerializable]
public sealed class StationModificationEditChannel : BoundUserInterfaceMessage
{
    public ProtoId<RadioChannelPrototype> ChannelID;
    public string Name;
    public string Hotkey;
    public int Red;
    public int Green;
    public int Blue;

    public StationModificationEditChannel(ProtoId<RadioChannelPrototype> channelId, string name, string hotkey, int red, int green, int blue)
    {
        ChannelID = channelId;
        Name = name;
        Hotkey = hotkey;
        Red = red;
        Green = green;
        Blue = blue;
    }
}
