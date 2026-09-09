using Content.Shared._Persistence14.PersistentIdentifier;
using Content.Shared._Persistence14.PersistentIdentifier.Reference;
using Robust.Shared.GameStates;

namespace Content.Shared._Persistence14.Research.RecipeRelay;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedRecipeRelaySystem))]
public sealed partial class RecipeRelayReceiverComponent : Component
{
    /// <summary>
    /// The Recipe Relay Source this receiver is getting data from.
    /// </summary>
    [DataField(readOnly: true), AutoNetworkedField]
    public PersistentEntityReference Source = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public bool HasSource => Source != PersistentIdentifierSystem.EmptyId;
}