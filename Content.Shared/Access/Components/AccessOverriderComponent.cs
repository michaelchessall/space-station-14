using Content.Shared.Access.Systems;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Access.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedAccessOverriderSystem))]
public sealed partial class AccessOverriderComponent : Component
{
    public static string PrivilegedIdCardSlotId = "AccessOverrider-privilegedId";

    [DataField]
    public ItemSlot PrivilegedIdSlot = new();

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public SoundSpecifier? DenialSound;

    public EntityUid TargetAccessReaderId = new();


    [Serializable, NetSerializable]
    public sealed class AccessReaderChangeModeMessage : BoundUserInterfaceMessage
    {
        public AccessReaderChangeModeMessage()
        {
        }
    }

    [Serializable, NetSerializable]
    public sealed class AccessReaderPersonalAddMessage : BoundUserInterfaceMessage
    {
        public readonly string Access;

        public AccessReaderPersonalAddMessage(string access)
        {
            Access = access;
        }
    }

    [Serializable, NetSerializable]
    public sealed class AccessReaderPersonalAccessToggledMessage : BoundUserInterfaceMessage
    {
        public readonly string Access;

        public AccessReaderPersonalAccessToggledMessage(string access)
        {
            Access = access;
        }
    }

    [Serializable, NetSerializable]
    public sealed class AccessReaderAccessToggledMessage : BoundUserInterfaceMessage
    {
        public readonly string Access;

        public AccessReaderAccessToggledMessage(string access)
        {
            Access = access;
        }
    }
    [Serializable, NetSerializable]
    public sealed class WriteToTargetAccessReaderIdMessage : BoundUserInterfaceMessage
    {
        public readonly List<ProtoId<AccessLevelPrototype>> AccessList;

        public WriteToTargetAccessReaderIdMessage(List<ProtoId<AccessLevelPrototype>> accessList)
        {
            AccessList = accessList;
        }
    }

    [DataField, AutoNetworkedField]
    public List<ProtoId<AccessLevelPrototype>> AccessLevels = new();

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public float DoAfter;

    [Serializable, NetSerializable]
    public sealed class AccessOverriderBoundUserInterfaceState : BoundUserInterfaceState
    {
        public readonly string TargetLabel;
        public readonly Color TargetLabelColor;
        public readonly string PrivilegedIdName;
        public readonly bool IsPrivilegedIdPresent;
        public readonly bool IsPrivilegedIdAuthorized;

        public List<string>? AccessList;
        public List<string>? PossibleAccess;
        public string StationName;
        public bool PersonalAccess;
        public List<string>? PersonalAccessList;
        public List<string>? MissingAccessList;
        public List<string>? AllAccesses;

        public AccessOverriderBoundUserInterfaceState(bool isPrivilegedIdPresent,
            bool isPrivilegedIdAuthorized,
            string privilegedIdName,
            string targetLabel,
            Color targetLabelColor,
            List<string>? accessList,
            List<string>? possibleAccess,
            string stationName,
            bool personalAccess,
            List<string>? personalAccessList,
            List<string>? missingAccessList,
            List<string>? allAccesses)
        {
            IsPrivilegedIdPresent = isPrivilegedIdPresent;
            IsPrivilegedIdAuthorized = isPrivilegedIdAuthorized;
            PrivilegedIdName = privilegedIdName;
            TargetLabel = targetLabel;
            TargetLabelColor = targetLabelColor;
            AccessList = accessList;
            PossibleAccess = possibleAccess;
            PersonalAccess = personalAccess;
            PersonalAccessList = personalAccessList;
            StationName = stationName;
            MissingAccessList = missingAccessList;
            AllAccesses = allAccesses;
        }
    }

    [Serializable, NetSerializable]
    public enum AccessOverriderUiKey : byte
    {
        Key,
    }
}
