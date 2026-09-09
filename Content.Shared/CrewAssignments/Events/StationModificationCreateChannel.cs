using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.Events;

[Serializable, NetSerializable]
public sealed class StationModificationCreateChannel : BoundUserInterfaceMessage
{
    public string Name;
    public string Hotkey;
    public int Red;
    public int Green;
    public int Blue;

    public StationModificationCreateChannel(string name, string hotkey, int red, int green, int blue)
    {
        Name = name;
        Hotkey = hotkey;
        Red = red;
        Green = green;
        Blue = blue;
    }
}
