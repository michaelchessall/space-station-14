using Content.Shared._Persistence14.Research.RecipeRelay;

namespace Content.Shared._Persistence14.RecipeRelay.Conditions;

public abstract partial class RecipeRelayConditionSystem<TComponent> : EntitySystem where TComponent : Component
{
    public override void Initialize()
    {
        SubscribeLocalEvent<TComponent, RecipeRelayLinkAttemptEvent>(OnLinkAttempt);
        SubscribeLocalEvent<TComponent, RecipeRelayUnlinkAttemptEvent>(OnUnlinkAttempt);
    }
    protected virtual void OnLinkAttempt(EntityUid uid, TComponent component, ref RecipeRelayLinkAttemptEvent args) { }
    protected virtual void OnUnlinkAttempt(EntityUid uid, TComponent component, ref RecipeRelayUnlinkAttemptEvent args) { }
}