using Content.Shared.Research.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Persistence14.Research.RecipeRelay;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RecipeContainerComponent : Component
{
    /// <summary>
    /// The set of all permanantly unlocked recipes which do not have recipe counts.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<LatheRecipePrototype>> PermanentRecipes = new();

    /// <summary>
    /// A lookup dictionary for unlocked recipe quantities stored by their prototype ID.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<LatheRecipePrototype>, int> UnlockedRecipes = new();
}