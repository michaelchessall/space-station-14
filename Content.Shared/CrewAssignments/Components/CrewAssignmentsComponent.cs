using Content.Shared._NF.Bank.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CrewAssignments.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class CrewAssignmentsComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public Dictionary<int, CrewAssignment> CrewAssignments { get; set; } = new();
    [DataField]
    [AutoNetworkedField]
    public int nextID = 1;
    public bool TryGetAssignment(int id, out CrewAssignment? assignment)
    {
        if (id == 0)
        {
            assignment = null;
            return false;
        }
        if (CrewAssignments.TryGetValue(id, out var currAssignment))
        {
            assignment = currAssignment;
            return true;
        }
        else
        {
            assignment = null;
            return false;
        }
    }
    public void CreateAssignment(string assignmentname, int wage = 0, int clevel = 0)
    {
        var id = nextID;
        nextID++;
        CrewAssignment newAssignment = new CrewAssignment(id, assignmentname, wage, clevel);
        CrewAssignments.Add(id, newAssignment);
    }
}


[DataDefinition]
[Serializable]
public partial class CrewAssignment
{
    [DataField("_id")]
    public int ID = 0;
    [DataField("_name")]
    public string Name = "Unnamed Crew Assignment";
    [DataField("_wage")]
    public int Wage = 0;
    [DataField("_clevel")]
    public int Clevel = 0;
    [DataField("_accessids")]
    public List<string> AccessIDs = new();


    public CrewAssignment(int id, string name, int wage, int clevel)
    {
        ID = id;
        Name = name;
        Wage = wage;
        Clevel = clevel;
    }
}
