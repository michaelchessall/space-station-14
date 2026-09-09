using Content.Shared._Persistence14.Research.RecipeRelay;
using Content.Shared.Access.Systems;

namespace Content.Shared._Persistence14.RecipeRelay.Conditions;

[RegisterComponent]
public sealed partial class RecipeRelayAccessConditionComponent : Component { }

public sealed partial class RecipeRelayAccessConditionSystem : RecipeRelayConditionSystem<RecipeRelayAccessConditionComponent>
{
    [Dependency] private AccessReaderSystem _access = default!;

    protected override void OnLinkAttempt(EntityUid uid, RecipeRelayAccessConditionComponent component, ref RecipeRelayLinkAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.User is not { } user ||
            !_access.IsAllowed(user, args.Source) ||
            !_access.IsAllowed(user, args.Receiver))
        {
            args.AllowDisplay = false;
            args.CancelMessage = "link-fail-access";
            args.BlockedTooltip = "link-tooltip-no-access";
            args.Cancel();
        }
    }

    protected override void OnUnlinkAttempt(EntityUid uid, RecipeRelayAccessConditionComponent component, ref RecipeRelayUnlinkAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.User is not { } user ||
            !_access.IsAllowed(user, args.Source) ||
            !_access.IsAllowed(user, args.Receiver))
        {
            args.AllowDisplay = true;
            args.CancelMessage = "unlink-fail-access";
            args.BlockedTooltip = "link-tooltip-no-access";
            args.Cancel();
        }
    }
}