using Content.Shared._Persistence14.Research.RecipeRelay;

namespace Content.Shared._Persistence14.RecipeRelay.Conditions;

[RegisterComponent]
public sealed partial class RecipeRelaySameGridConditionComponent : Component { }

public sealed partial class RecipeRelaySameGridConditionSystem : RecipeRelayConditionSystem<RecipeRelaySameGridConditionComponent>
{
    protected override void OnLinkAttempt(EntityUid uid, RecipeRelaySameGridConditionComponent component, ref RecipeRelayLinkAttemptEvent args)
    {
        var sourceXform = Transform(args.Source);
        var receiverXform = Transform(args.Receiver);

        if (sourceXform.GridUid != receiverXform.GridUid)
        {
            args.AllowDisplay = false;
            args.CancelMessage = "link-fail-grid";
            args.Cancel();
        }
    }
}