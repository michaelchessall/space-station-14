using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.Events;

[Serializable, NetSerializable]
public sealed class StationModificationDeleteChannel : BoundUserInterfaceMessage
{
    public ProtoId<RadioChannelPrototype> ChannelID;

    public StationModificationDeleteChannel(ProtoId<RadioChannelPrototype> channelId)
    {
        ChannelID = channelId;
    }
}
