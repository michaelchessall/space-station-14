using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Funkystation.Footprints;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FootprintComponent : Component
{
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public List<FootprintData> Prints = new();
}

[DataDefinition]
[Serializable, NetSerializable]
public partial record struct FootprintData
{
    [DataField]
    public Vector2 Offset;

    [DataField]
    public Angle Rotation;

    [DataField]
    public Color Color;

    [DataField]
    public string State;

    public FootprintData(Vector2 offset, Angle rotation, Color color, string state)
    {
        Offset = offset;
        Rotation = rotation;
        Color = color;
        State = state;
    }
}
