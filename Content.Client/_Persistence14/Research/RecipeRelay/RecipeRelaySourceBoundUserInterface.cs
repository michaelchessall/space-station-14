using Content.Shared._Persistence14.Research.RecipeRelay;
using Robust.Client.UserInterface;

namespace Content.Client._Persistence14.Research.RecipeRelay;

public sealed partial class RecipeRelaySourceBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private RecipeRelaySourceWindow? _window;

    public RecipeRelaySourceBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<RecipeRelaySourceWindow>();
        _window.OnClientPressed += client =>
        {
            SendMessage(new RecipeRelayToggleReceieverMessage(client));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not RecipeRelaySourceBoundUserInterfaceState cast)
            return;

        _window?.UpdateState(cast);
    }
}