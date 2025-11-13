using Content.Shared.Access.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.CrewAssignments.Components;
using Content.Shared.CrewRecords.Components;
using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Access.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedIdCardConsoleSystem))]
public sealed partial class IdCardConsoleComponent : Component
{
    public static string PrivilegedIdCardSlotId = "IdCardConsole-privilegedId";

    public static string TargetIdCardSlotId = "IdCardConsole-targetId";

    public CrewRecord? SelectedRecord;
    public CrewRecord? PrivRecord;

    [DataField]
    public ItemSlot PrivilegedIdSlot = new();

    [DataField]
    public ItemSlot TargetIdSlot = new();

    [Serializable, NetSerializable]
    public sealed class WriteToTargetIdMessage : BoundUserInterfaceMessage
    {
        public readonly string FullName;
        public readonly string JobTitle;
        public readonly List<ProtoId<AccessLevelPrototype>> AccessList;
        public readonly ProtoId<JobPrototype> JobPrototype;

        public WriteToTargetIdMessage(string fullName, string jobTitle, List<ProtoId<AccessLevelPrototype>> accessList, ProtoId<JobPrototype> jobPrototype)
        {
            FullName = fullName;
            JobTitle = jobTitle;
            AccessList = accessList;
            JobPrototype = jobPrototype;
        }
    }

    [Serializable, NetSerializable]
    public sealed class SearchRecord : BoundUserInterfaceMessage
    {
        public readonly string FullName;

        public SearchRecord(string fullName)
        {
            FullName = fullName;
        }
    }
    [Serializable, NetSerializable]
    public sealed class ChangeAssignment : BoundUserInterfaceMessage
    {
        public readonly int ID;

        public ChangeAssignment(int id)
        {
            ID = id;
        }
    }

    // Put this on shared so we just send the state once in PVS range rather than every time the UI updates.

    [DataField, AutoNetworkedField]
    public List<ProtoId<AccessLevelPrototype>> AccessLevels = new()
    {
        "Armory",
        "Atmospherics",
        "Bar",
        "Brig",
        "Detective",
        "Captain",
        "Cargo",
        "Chapel",
        "Chemistry",
        "ChiefEngineer",
        "ChiefMedicalOfficer",
        "Command",
        "Cryogenics",
        "Engineering",
        "External",
        "HeadOfPersonnel",
        "HeadOfSecurity",
        "Hydroponics",
        "Janitor",
        "Kitchen",
        "Lawyer",
        "Maintenance",
        "Medical",
        "Quartermaster",
        "Research",
        "ResearchDirector",
        "Salvage",
        "Security",
        "Service",
        "Theatre",
    };

    [Serializable, NetSerializable]
    public sealed class IdCardConsoleBoundUserInterfaceState : BoundUserInterfaceState
    {
        public readonly bool IsPrivilegedIdPresent;
        public readonly bool IsPrivilegedIdAuthorized;

        public readonly bool IsTargetIdPresent;
        public readonly string? TargetIdFullName;
        public readonly string? PrivilegedIdName;
        public readonly string? PrivFullName;
        public readonly string? TargetIdName;
        public readonly CrewAssignment? Assignment;
        public readonly CrewAssignment? PrivAssignment;
        public readonly Dictionary<int, CrewAssignment>? AllAssignments;
        public readonly bool IsOwner = false;

        public IdCardConsoleBoundUserInterfaceState(bool isPrivilegedIdPresent,
            bool isPrivilegedIdAuthorized,
            bool isTargetIdPresent,
            string? targetIdFullName,
            string? targetIdName,
            string? privilegedIdName,
            string? privIdFullName,
            CrewAssignment? crewAssignment,
            CrewAssignment? privCrewAssignment,
            Dictionary<int, CrewAssignment>? allAssignments,
            bool isOwner)
        {
            IsPrivilegedIdPresent = isPrivilegedIdPresent;
            IsPrivilegedIdAuthorized = isPrivilegedIdAuthorized;
            IsTargetIdPresent = isTargetIdPresent;
            TargetIdFullName = targetIdFullName;
            TargetIdName = targetIdName;
            PrivilegedIdName = privilegedIdName;
            PrivFullName = privIdFullName;
            Assignment = crewAssignment;
            PrivAssignment = privCrewAssignment;
            AllAssignments = allAssignments;
            this.IsOwner = isOwner;
        }
    }

    [Serializable, NetSerializable]
    public enum IdCardConsoleUiKey : byte
    {
        Key,
    }
}
