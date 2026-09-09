using Content.Shared._Persistence14.PersistentIdentifier.Reference;
using Robust.Shared.GameStates;

namespace Content.Shared._Persistence14.Research.RecipeRelay;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RecipeRelaySourceComponent : Component
{
    /// <summary>
    /// If true, sources which are also receivers may continue to relay to the true source.
    /// </summary>
    [DataField]
    public bool AllowRecursiveRelay = false;

    /// <summary>
    /// A list of all currently connected receivers.
    /// </summary>
    [DataField(readOnly: true), AutoNetworkedField]
    public HashSet<PersistentEntityReference> Receivers = new();
}