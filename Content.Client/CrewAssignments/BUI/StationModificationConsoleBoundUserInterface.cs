using Content.Client.Cargo.UI;
using Content.Client.CrewAssignments.UI;
using Content.Shared.Cargo;
using Content.Shared.Cargo.BUI;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Events;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.CrewAccesses.Components;
using Content.Shared.CrewAssignments.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Station.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Linq;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.CrewAssignments.BUI;

public sealed class StationModificationConsoleBoundUserInterface : BoundUserInterface
{

    [ViewVariables]
    private StationModificationMenu? _menu;

    [ViewVariables]
    public string? AccountName { get; private set; }

    [ViewVariables]
    public int BankBalance { get; private set; }

    public Dictionary<string, CrewAccess>? Accesses { get; private set; }
    public Dictionary<int, CrewAssignment>? Assignments { get; private set; }


    public StationModificationConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        var spriteSystem = EntMan.System<SpriteSystem>();
        var dependencies = IoCManager.Instance!;
        _menu = new StationModificationMenu(Owner, EntMan, dependencies.Resolve<IPrototypeManager>(), spriteSystem);
        var localPlayer = dependencies.Resolve<IPlayerManager>().LocalEntity;
        var description = new FormattedMessage();

        string orderRequester;

        if (EntMan.EntityExists(localPlayer))
            orderRequester = Identity.Name(localPlayer.Value, EntMan);
        else
            orderRequester = string.Empty;
        
        _menu.OnClose += Close;
        _menu.OnOwnerPressed += RemoveOwner;
        _menu.NewOwnerConfirm.OnPressed += AddOwner;
        _menu.StationNameConfirm.OnPressed += ChangeStationName;
        _menu.AccessCreateConfirm.OnPressed += CreateNewAccess;
        _menu.AccessDeleteConfirm.OnPressed += DeleteAccess;
        _menu.CreateAssignment.OnPressed += CreateAssignment;
        _menu.OnAssignmentAccessPressed += ToggleAssignmentAccess;
        _menu.CommandLevelConfirm.OnPressed += ChangeCommandLevel;
        _menu.AssignmentWageConfirm.OnPressed += ChangeWage;
        _menu.AssignmentNameConfirm.OnPressed += ChangeAssignmentName;
        _menu.DeleteAssignment.OnPressed += DeleteAssignment;
        _menu.OpenCentered();
    }


    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not StationModificationInterfaceState cState)
            return;


        if (_menu == null)
            return;
        var station = EntMan.GetEntity(cState.Station);
        var owners = cState.Owners;
        Accesses = cState.CrewAccess;
        Assignments = cState.CrewAssignments;
        _menu?.UpdateOwners(owners);
        _menu?.UpdateStation(station, cState.Name);
        _menu?.UpdateAccesses(Accesses);
        _menu?.UpdateAssignments(Assignments);

    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _menu?.Dispose();
    }

    private void RemoveOwner(ButtonEventArgs args)
    {
        if (args.Button is not StationOwnerButton row)
            return;            

        SendMessage(new StationModificationRemoveOwner(row.Owner));
    }
    private void AddOwner(ButtonEventArgs args)
    {
        if (_menu == null) return;
        string newOwner = _menu.NewOwnerField.Text;
        if (newOwner == null || newOwner == "") return;
        SendMessage(new StationModificationAddOwner(newOwner));
    }

    private void ChangeStationName(ButtonEventArgs args)
    {
        if (_menu == null) return;
        string newName = _menu.StationNameField.Text;
        if (newName == null || newName == "") return;
        SendMessage(new StationModificationChangeName(newName));
    }

    private void CreateNewAccess(ButtonEventArgs args)
    {
        if (_menu == null) return;
        string newName = _menu.AccessCreateField.Text;
        if (newName == null || newName == "") return;
        SendMessage(new StationModificationAddAccess(newName));
    }
    private void DeleteAccess(ButtonEventArgs args)
    {
        if (_menu == null || Accesses == null) return;
        var i = _menu.PossibleAccesses.SelectedId;
        var access = Accesses.ElementAt(i);

        SendMessage(new StationModificationRemoveAccess(access.Key));
    }

    private void CreateAssignment(ButtonEventArgs args)
    {
        if (_menu == null) return;
        string newName = _menu.NewAssignmentNameField.Text;
        if (newName == null || newName == "") return;
        SendMessage(new StationModificationCreateAssignment(newName));
    }
    private void ToggleAssignmentAccess(ButtonToggledEventArgs args)
    {
        if (_menu == null) return;
        var assignment = _menu.PossibleAssignments.SelectedId;
        Button real = (Button)args.Button;
        if(real==null||real.Text==null) return;
        SendMessage(new StationModificationToggleAssignmentAccess(assignment, args.Pressed, real.Text));
    }

    private void ChangeCommandLevel(ButtonEventArgs args)
    {
        if (_menu == null) return;
        var assignment = _menu.PossibleAssignments.SelectedId;
        var clevel = _menu.CLevelSpinBox.Value;

        SendMessage(new StationModificationChangeAssignmentCLevel(assignment, clevel));
    }

    private void ChangeWage(ButtonEventArgs args)
    {
        if (_menu == null) return;
        var assignment = _menu.PossibleAssignments.SelectedId;
        var wage = _menu.WageSpinBox.Value;

        SendMessage(new StationModificationChangeAssignmentWage(assignment, wage));
    }
    private void ChangeAssignmentName(ButtonEventArgs args)
    {
        if (_menu == null) return;
        var assignment = _menu.PossibleAssignments.SelectedId;
        string newName = _menu.AssignmentNameField.Text;
        if (newName == null || newName == "") return;
        SendMessage(new StationModificationChangeAssignmentName(assignment, newName));
    }

    private void DeleteAssignment(ButtonEventArgs args)
    {
        if (_menu == null || Accesses == null) return;
        var i = _menu.PossibleAssignments.SelectedId;
        SendMessage(new StationModificationDeleteAssignment(i));
    }
}
