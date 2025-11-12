using Content.Shared._NF.Bank.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CrewRecords.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class CrewRecordsComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public Dictionary<string, CrewRecord> CrewRecords { get; set; } = new();

    public bool TryGetRecord(string name, out CrewRecord? record)
    {
        if (CrewRecords.TryGetValue(name, out var currRecord))
        {
            record = currRecord;
            return true;
        }
        else
        {
            record = null;
            return false;
        }
    }
    public bool CreateRecord(string recordname, out CrewRecord? record)
    {
        if (CrewRecords.TryGetValue(recordname, out record)) return false;
        record = new CrewRecord(recordname);
        CrewRecords.Add(recordname, record);
        return true;
    }

    public bool TryEnsureRecord(string name, out CrewRecord? record, EntityManager? entityManager = null)
    {
        if (TryGetRecord(name, out record)) return true;
        CreateRecord(name, out record);
        if (entityManager != null) entityManager.Dirty(Owner, this);
        return true;
    }
}


[DataDefinition]
[Serializable]
public partial class CrewRecord
{

    [DataField("_name")]
    public string Name = "Unnamed Crew Record";
    [DataField("_assignmentid")]
    public int AssignmentID = 0;

    public CrewRecord(string name)
    {
        Name = name;
    }
}
