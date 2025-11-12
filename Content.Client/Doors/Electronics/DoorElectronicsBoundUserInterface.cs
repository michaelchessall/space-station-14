using Content.Shared.Access;
using Content.Shared.Doors.Electronics;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.Doors.Electronics;

public sealed class DoorElectronicsBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private DoorElectronicsConfigurationMenu? _window;

    public DoorElectronicsBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DoorElectronicsConfigurationMenu>();
        _window.OnAccessChanged += UpdateConfiguration;
        _window.OnAccessToggle += AccessToggle;
        _window.PersonalAccessToggle += PersonalAccessToggle;
        _window.PersonalSaveButton.OnPressed += PersonalAccessAdd;
        _window.ChangeMode.OnPressed += ChangeMode;
        Reset();
    }

    public override void OnProtoReload(PrototypesReloadedEventArgs args)
    {
        base.OnProtoReload(args);

        if (!args.WasModified<AccessLevelPrototype>())
            return;

        Reset();
    }

    private void Reset()
    {
        List<ProtoId<AccessLevelPrototype>> accessLevels = new();

        foreach (var accessLevel in _prototypeManager.EnumeratePrototypes<AccessLevelPrototype>())
        {
            if (accessLevel.Name != null)
            {
                accessLevels.Add(accessLevel.ID);
            }
        }

        accessLevels.Sort();
        _window?.Reset(_prototypeManager, accessLevels);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        var castState = (DoorElectronicsConfigurationState) state;

        _window?.UpdateState(castState);
    }

    public void UpdateConfiguration(List<ProtoId<AccessLevelPrototype>> newAccessList)
    {
        SendMessage(new DoorElectronicsUpdateConfigurationMessage(newAccessList));
    }
    public void AccessToggle(string access)
    {
        SendMessage(new DoorElectronicsAccessToggleMessage(access));
    }
    public void PersonalAccessToggle(string access)
    {
        SendMessage(new DoorElectronicsPersonalRemoveMessage(access));
    }

    public void PersonalAccessAdd(ButtonEventArgs args)
    {
        if (_window == null) return;
        Button button = (Button)args.Button;
        var name = _window.PersonalLineEdit.Text;
        if(name != null && name != "")
        {
            SendMessage(new DoorElectronicsPersonalAddMessage(name));
            _window.PersonalLineEdit.Text = "";
        }
        
    }
    public void ChangeMode(ButtonEventArgs args)
    {
        SendMessage(new DoorElectronicsChangeModeMessage());
    }
}
