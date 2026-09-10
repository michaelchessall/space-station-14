using Content.Shared.Access.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.GridControl.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.GridControl.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedGridConfigSystem))]
public sealed partial class GridConfigComponent : Component
{
    public static string PrivilegedIdCardSlotId = "GridConfig-privilegedId";

    [DataField]
    public ItemSlot PrivilegedIdSlot = new();

    [DataField]
    public int? ConnectedStation = null;

    [DataField]
    public bool PersonalMode = false;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public float DoAfter = 5f;



    [Serializable, NetSerializable]
    public sealed class GridConfigBoundUserInterfaceState : BoundUserInterfaceState
    {
        public bool IdPresent = false;
        public bool IsOwner = false;
        public bool IsAuth = false;
        public bool PersonalMode = false;
        public bool IsControlled = false;
        public Dictionary<int, string>? PossibleStations = null;
        public string? TargetName = null;
        public string? OwnerName = null;
        public string? GridName = null;
        public string? IdName = null;
        public int? targetStation = null;

        public int GridTileCount = 0;
        public int CurrentTileCount = 0;
        public int MaxPersonalClaimTileCount = 0;

        public string? ErrorMessage = null;
        public GridConfigBoundUserInterfaceState(
            bool idpresent, bool isowner, bool isauth, bool personalmode, bool isControlled,
            Dictionary<int, string>? possiblestations, string? targetname,
            string? ownername, string? gridname, string? idname, int? targetStation,
            int gridTileCount, int currentTileCount, int maxPersonalClaimTileCount, string? errorMessage
        )
        {
            IdPresent = idpresent;
            IsOwner = isowner;
            IsAuth = isauth;
            PersonalMode = personalmode;
            IsControlled = isControlled;
            PossibleStations = possiblestations;
            TargetName = targetname;
            OwnerName = ownername;
            GridName = gridname;
            IdName = idname;
            this.targetStation = targetStation;
            GridTileCount = gridTileCount;
            CurrentTileCount = currentTileCount;
            MaxPersonalClaimTileCount = maxPersonalClaimTileCount;
            ErrorMessage = errorMessage;
        }
    }

    [Serializable, NetSerializable]
    public enum GridConfigUiKey : byte
    {
        Key,
    }

    [Serializable, NetSerializable]
    public sealed class GridConfigChangeName : BoundUserInterfaceMessage
    {
        public string Name;
        public GridConfigChangeName(string name)
        {
            Name = name;
        }
    }
    [Serializable, NetSerializable]
    public sealed class GridConfigDisconnect : BoundUserInterfaceMessage
    {
        public GridConfigDisconnect()
        {
        }
    }
    [Serializable, NetSerializable]
    public sealed class GridConfigConnect : BoundUserInterfaceMessage
    {
        public GridConfigConnect()
        {
        }
    }

    [Serializable, NetSerializable]
    public sealed class GridConfigChangeMode : BoundUserInterfaceMessage
    {
        public GridConfigChangeMode()
        {
        }
    }

    [Serializable, NetSerializable]
    public sealed class GridConfigTargetSelect : BoundUserInterfaceMessage
    {
        public int Target;
        public GridConfigTargetSelect(int target)
        {
            Target = target;
        }
    }
}

[Serializable, NetSerializable]
public enum GridConfigVisualState : byte
{

    NoId,

    Id,
    Off,
    On,
}
[Serializable, NetSerializable]
public enum GridConfigVisuals : byte
{
    HasId,
    Screen
}

[Serializable, NetSerializable]
public enum GridConfigVisualLayers : byte
{
    ID,
    Screen,
}