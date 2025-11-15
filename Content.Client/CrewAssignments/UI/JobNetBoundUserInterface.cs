using Content.Client.CrewAssignments.UI;
using Content.Shared.Cargo.BUI;
using Content.Shared.Cargo.Events;
using Content.Shared.CrewAssignments;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using System.Linq;
using static Robust.Client.UserInterface.Controls.BaseButton;
using static Robust.Client.UserInterface.Controls.OptionButton;

namespace Content.Client.Store.Ui;

[UsedImplicitly]
public sealed class JobNetBoundUserInterface : BoundUserInterface
{
    private IPrototypeManager _prototypeManager = default!;

    [ViewVariables]
    private JobNetMenu? _menu;

    [ViewVariables]
    private string _search = string.Empty;

    [ViewVariables]
    private HashSet<ListingDataWithCostModifiers> _listings = new();

    public JobNetBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<JobNetMenu>();

        _menu.PossibleJobs.OnItemSelected += OnJobPressed;

    }

    public void OnJobPressed(ItemSelectedEventArgs args)
    {
        SendMessage(new JobNetSelectMessage(args.Id));
    }
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_menu == null) return;
        if (state is not JobNetUpdateState cState)
            return;
        _menu.UpdateState(cState);


    }
}
