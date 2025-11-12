using Content.Shared._NF.Bank.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CrewAccesses.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class CrewAccessesComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public Dictionary<string, CrewAccess> CrewAccesses { get; set; } = new();
    public bool TryGetAccess(string id, out CrewAccess? access)
    {
        if (CrewAccesses.TryGetValue(id, out var currAccess))
        {
            access = currAccess;
            return true;
        }
        else
        {
            access = null;
            return false;
        }
    }
    public void CreateAccess(string accessname)
    {
        CrewAccess newAccess = new CrewAccess(accessname);
        CrewAccesses.Add(accessname, newAccess);
        Dirty();
    }
    public void RemoveAccess(string accessname)
    {
        CrewAccesses.Remove(accessname);
        Dirty();
    }
}

[DataDefinition]
[Serializable]
public partial class CrewAccess
{
    [DataField("_name")]
    public string Name = "Unnamed Crew Access";


    public CrewAccess(string name)
    {
        Name = name;
    }
}
