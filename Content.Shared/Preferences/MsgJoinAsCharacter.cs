using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using System.IO;

namespace Content.Shared.Preferences
{
    /// <summary>
    /// The client sends this to select a character slot.
    /// </summary>
    public sealed class MsgJoinAsCharacter : NetMessage
    {
        public override MsgGroups MsgGroup => MsgGroups.Command;

        public int Slot;

        public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
        {
            Slot = buffer.ReadInt32();
        }

        public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
        {
            buffer.Write(Slot);
        }
    }
}
