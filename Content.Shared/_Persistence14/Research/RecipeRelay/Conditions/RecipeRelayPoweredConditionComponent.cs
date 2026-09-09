using Content.Shared._Persistence14.Research.RecipeRelay;
using Content.Shared.Power.EntitySystems;

namespace Content.Shared._Persistence14.RecipeRelay.Conditions;

[RegisterComponent]
public sealed partial class RecipeRelayPoweredConditionComponent : Component { }

public sealed partial class RecipeRelayPoweredConditionSystem : RecipeRelayConditionSystem<RecipeRelayPoweredConditionComponent>
{
    [Dependency] private SharedPowerReceiverSystem _power = default!;

    protected override void OnLinkAttempt(EntityUid uid, RecipeRelayPoweredConditionComponent component, ref RecipeRelayLinkAttemptEvent args)
    {
        if (!_power.IsPowered(uid))
        {
            args.AllowDisplay = false;
            args.CancelMessage = "link-fail-power";
            args.Cancel();
        }
    }
}