using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;
using Content.Shared.Access;

namespace Content.Shared.Doors.Electronics;

/// <summary>
/// Allows an entity's AccessReader to be configured via UI.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DoorElectronicsComponent : Component
{
}

[Serializable, NetSerializable]
public sealed class DoorElectronicsUpdateConfigurationMessage : BoundUserInterfaceMessage
{
    public List<ProtoId<AccessLevelPrototype>> AccessList;

    public DoorElectronicsUpdateConfigurationMessage(List<ProtoId<AccessLevelPrototype>> accessList)
    {
        AccessList = accessList;
    }
}

[Serializable, NetSerializable]
public sealed class DoorElectronicsAccessToggleMessage : BoundUserInterfaceMessage
{
    public string Access;

    public DoorElectronicsAccessToggleMessage(string access)
    {
        Access = access;
    }
}


[Serializable, NetSerializable]
public sealed class DoorElectronicsChangeModeMessage : BoundUserInterfaceMessage
{

    public DoorElectronicsChangeModeMessage()
    {
    }
}


[Serializable, NetSerializable]
public sealed class DoorElectronicsPersonalAddMessage : BoundUserInterfaceMessage
{
    public string Access;

    public DoorElectronicsPersonalAddMessage(string access)
    {
        Access = access;
    }
}


[Serializable, NetSerializable]
public sealed class DoorElectronicsPersonalRemoveMessage : BoundUserInterfaceMessage
{
    public string Access;

    public DoorElectronicsPersonalRemoveMessage(string access)
    {
        Access = access;
    }
}
[Serializable, NetSerializable]
public sealed class DoorElectronicsConfigurationState : BoundUserInterfaceState
{
    public List<string> AccessList;
    public List<string>? PossibleAccess;
    public string StationName;
    public bool PersonalAccess;
    public List<string>? PersonalAccessList;


    public DoorElectronicsConfigurationState(List<string> accessList, List<string>? possibleaccess, string stationname, bool personalAccess, List<string>? personalAccessList)
    {
        AccessList = accessList;
        PossibleAccess = possibleaccess;
        StationName = stationname;
        PersonalAccess = personalAccess;
        PersonalAccessList = personalAccessList;
    }
}

[Serializable, NetSerializable]
public enum DoorElectronicsConfigurationUiKey : byte
{
    Key
}
