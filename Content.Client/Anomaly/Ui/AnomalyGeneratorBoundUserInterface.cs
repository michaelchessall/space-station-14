using Content.Shared._Persistence14.Research.Anomalies;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Anomaly.Ui;

/*
 * THIS FILE HAS BEEN BASICALLY ENTIRELY REWRITTEN FOR PERSISTENCE 14
 */

[UsedImplicitly]
public sealed partial class AnomalyGeneratorBoundUserInterface : BoundUserInterface
{
    private AnomalyGeneratorWindow? _window;

    public AnomalyGeneratorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<AnomalyGeneratorWindow>();

        _window.GenerateAction = () => SendMessage(new GenerateAnomalyEvent());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not AnomalyGeneratorBUIState msg)
            return;

        _window?.UpdateState(msg);
    }
}