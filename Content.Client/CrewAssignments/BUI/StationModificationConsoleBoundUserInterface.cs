using Content.Client.Cargo.UI;
using Content.Client.CrewAssignments.UI;
using Content.Shared.Cargo;
using Content.Shared.Cargo.BUI;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Events;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.CrewAccesses.Components;
using Content.Shared.CrewAssignments.Components;
using Content.Shared.CrewAssignments.Events;
using Content.Shared.IdentityManagement;
using Content.Shared.Station.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
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
        _menu.FactionTagConfirm.OnPressed += ChangeFactionTag;
        _menu.AccessCreateConfirm.OnPressed += CreateNewAccess;
        _menu.AccessDeleteConfirm.OnPressed += DeleteAccess;
        _menu.CreateAssignment.OnPressed += CreateAssignment;
        _menu.OnAssignmentAccessPressed += ToggleAssignmentAccess;
        _menu.OnChannelAccessPressed += ToggleChannelAccess;
        _menu.CommandLevelConfirm.OnPressed += ChangeCommandLevel;
        _menu.AssignmentWageConfirm.OnPressed += ChangeWage;
        _menu.AssignmentNameConfirm.OnPressed += ChangeAssignmentName;
        _menu.SpendingLimitConfirm.OnPressed += ChangeAssignmentSpendingLimit;
        _menu.DeleteAssignment.OnPressed += DeleteAssignment;
        _menu.DefaultAccessCreate.OnPressed += DefaultAccessCreate;
        _menu.ClaimBtn.OnPressed += ToggleClaim;
        _menu.GenRecBtn.OnPressed += ToggleGenRec;
        _menu.ReassignmentBtn.OnPressed += ToggleAssign;
        _menu.ITaxConfirm.OnPressed += ChangeITax;
        _menu.ETaxConfirm.OnPressed += ChangeETax;
        _menu.STaxConfirm.OnPressed += ChangeSTax;
        _menu.LevelPurchaseButton.OnPressed += PurchaseUpgrade;
        _menu.ChannelEnable.OnPressed += OnChannelEnable;
        _menu.ChannelDisable.OnPressed += OnChannelDisable;
        _menu.CreateChannelButton.OnPressed += OnCreateChannel;
        _menu.EditChannelButton.OnPressed += OnEditChannel;
        _menu.DeleteChannelButton.OnPressed += OnDeleteChannel;
        _menu.JobNetOn.OnPressed += OnJobNetOn;
        _menu.JobNetOff.OnPressed += OnJobNetOff;
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
        _menu?.UpdateStation(station, cState.Name, cState.FactionTag);
        _menu?.UpdateAccesses(Accesses);
        _menu?.UpdateAssignments(Assignments);
        _menu?.UpdateUpgrades(cState.Level, cState.AccountBalance);
        _menu?.UpdateChannels(cState.RadioData);
        if (_menu != null)
        {
            _menu.ETaxSpinBox.Value = cState.ExportTax;
            _menu.ITaxSpinBox.Value = cState.ImportTax;
            _menu.STaxSpinBox.Value = cState.SalesTax;
            if (cState.TradeStationClaimed)
            {
                _menu.TaxOptions.Visible = true;
                _menu.ClaimTradeStationLabel.Visible = false;
            }
            else
            {
                _menu.TaxOptions.Visible = false;
                _menu.ClaimTradeStationLabel.Visible = true;
            }
            if (cState.JobNetEnabled)
            {
                _menu.JobNetOff.Pressed = false;
                _menu.JobNetOn.Pressed = true;
            }
            else
            {
                _menu.JobNetOff.Pressed = true;
                _menu.JobNetOn.Pressed = false;
            }
        }

    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _menu?.Dispose();
    }

    private void OnJobNetOff(ButtonEventArgs args)
    {
        SendMessage(new StationModificationJobNetOff());
    }
    private void OnJobNetOn(ButtonEventArgs args)
    {
        SendMessage(new StationModificationJobNetOn());
    }
    private void PurchaseUpgrade(ButtonEventArgs args)
    {
        if (_menu == null) return;
        SendMessage(new StationModificationPurchaseUpgrade());
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

    private void ChangeFactionTag(ButtonEventArgs args)
    {
        if (_menu == null)
            return;

        SendMessage(new StationModificationChangeFactionTag(_menu.FactionTagField.Text));
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
        if (_menu == null || Accesses == null || Accesses.Count == 0) return;
        var i = _menu.PossibleAccesses.SelectedId;
        var access = Accesses.ElementAtOrDefault(i);

        SendMessage(new StationModificationRemoveAccess(access.Key));
    }

    private void CreateAssignment(ButtonEventArgs args)
    {
        if (_menu == null) return;
        string newName = _menu.NewAssignmentNameField.Text;
        if (newName == null || newName == "") return;
        SendMessage(new StationModificationCreateAssignment(newName));
        _menu.LastAssignmentCreated = newName;
        _menu.NewAssignmentNameField.Text = "";
    }

    private void ToggleChannelAccess(ButtonToggledEventArgs args)
    {
        if (_menu == null || _menu.RadioData == null) return;
        var channelId = _menu.SelectedChannelId;
        if (channelId == null) return;
        Button real = (Button)args.Button;
        if (real == null || real.Text == null) return;
        SendMessage(new StationModificationToggleChannelAccess(channelId.Value, args.Pressed, real.Text));
    }
    private void OnChannelEnable(ButtonEventArgs args)
    {
        if (_menu == null || _menu.RadioData == null) return;
        var channelId = _menu.SelectedChannelId;
        if (channelId == null) return;
        SendMessage(new StationModificationEnableChannel(channelId.Value));
    }

    private void OnChannelDisable(ButtonEventArgs args)
    {
        if (_menu == null || _menu.RadioData == null) return;
        var channelId = _menu.SelectedChannelId;
        if (channelId == null) return;
        SendMessage(new StationModificationDisableChannel(channelId.Value));
    }

    private void OnCreateChannel(ButtonEventArgs args)
    {
        if (_menu == null) return;

        var name = _menu.CustomChannelName.Text;
        var hotkey = _menu.CustomChannelHotkey.Text;
        // Persistence 14: Send the picker color directly so custom channel save/load round-trips do not distort HSV saturation or value through an extra sRGB conversion.
        var color = _menu.ChannelColorPicker.Color;

        SendMessage(new StationModificationCreateChannel(name, hotkey, color.RByte, color.GByte, color.BByte));
        // End Persistence 14
    }

    private void OnEditChannel(ButtonEventArgs args)
    {
        if (_menu == null || _menu.RadioData == null)
            return;

        var channelId = _menu.SelectedChannelId;
        if (channelId == null)
            return;

        var name = _menu.EditChannelName.Text;
        var hotkey = _menu.EditChannelHotkey.Text;
        // Persistence 14: Send the picker color directly so editing an existing custom channel preserves the client preview color apart from the explicit brightness clamp.
        var color = _menu.ChannelColorPicker.Color;

        SendMessage(new StationModificationEditChannel(channelId.Value, name, hotkey, color.RByte, color.GByte, color.BByte));
        // End Persistence 14
    }

    private void OnDeleteChannel(ButtonEventArgs args)
    {
        if (_menu == null || _menu.RadioData == null)
            return;

        var channelId = _menu.SelectedChannelId;
        if (channelId == null)
            return;

        if (_menu.RequireDeleteChannelConfirmation())
            return;

        SendMessage(new StationModificationDeleteChannel(channelId.Value));
    }
    private void ToggleAssignmentAccess(ButtonToggledEventArgs args)
    {
        if (_menu == null) return;
        var assignment = _menu.PossibleAssignments.SelectedId;
        Button real = (Button)args.Button;
        if (real == null || real.Text == null) return;
        SendMessage(new StationModificationToggleAssignmentAccess(assignment, args.Pressed, real.Text));
    }

    private void ToggleClaim(ButtonEventArgs args)
    {
        if (_menu == null) return;
        var assignment = _menu.PossibleAssignments.SelectedId;
        SendMessage(new StationModificationToggleClaim(assignment));
    }

    private void ToggleGenRec(ButtonEventArgs args)
    {
        if (_menu == null) return;
        var assignment = _menu.PossibleAssignments.SelectedId;
        SendMessage(new StationModificationToggleGenRec(assignment));
    }


    private void ToggleAssign(ButtonEventArgs args)
    {
        if (_menu == null) return;
        var assignment = _menu.PossibleAssignments.SelectedId;
        SendMessage(new StationModificationToggleAssign(assignment));
    }

    private void ChangeCommandLevel(ButtonEventArgs args)
    {
        if (_menu == null) return;
        var assignment = _menu.PossibleAssignments.SelectedId;
        var clevel = _menu.CLevelSpinBox.Value;

        SendMessage(new StationModificationChangeAssignmentCLevel(assignment, clevel));
    }

    private void ChangeITax(ButtonEventArgs args)
    {
        if (_menu == null) return;
        var clevel = _menu.ITaxSpinBox.Value;

        SendMessage(new StationModificationChangeImportTax(clevel));
    }

    private void ChangeETax(ButtonEventArgs args)
    {
        if (_menu == null) return;
        var clevel = _menu.ETaxSpinBox.Value;

        SendMessage(new StationModificationChangeExportTax(clevel));
    }
    private void ChangeSTax(ButtonEventArgs args)
    {
        if (_menu == null) return;
        var clevel = _menu.STaxSpinBox.Value;

        SendMessage(new StationModificationChangeSalesTax(clevel));
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
    private void ChangeAssignmentSpendingLimit(ButtonEventArgs args)
    {
        if (_menu == null) return;
        var assignment = _menu.PossibleAssignments.SelectedId;
        int newLimit = _menu.SpendingLimitSpinBox.Value;
        if (newLimit < 0) return;
        SendMessage(new StationModificationChangeAssignmentSpendingLimit(assignment, newLimit));
    }

    private void DeleteAssignment(ButtonEventArgs args)
    {
        if (_menu == null || Accesses == null) return;
        var i = _menu.PossibleAssignments.SelectedId;
        SendMessage(new StationModificationDeleteAssignment(i));
    }

    private void DefaultAccessCreate(ButtonEventArgs args)
    {
        SendMessage(new StationModificationDefaultAccess());
    }
}
