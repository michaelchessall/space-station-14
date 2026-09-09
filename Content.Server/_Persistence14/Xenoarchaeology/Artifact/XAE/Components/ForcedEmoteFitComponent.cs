using Content.Server.Xenoarchaeology.Artifact.XAE;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Xenoarchaeology.Artifact.XAE.Components;

/// <summary>
/// Tracks the ongoing forced-emote "fits" applied to a mob by <see cref="XAEForcedEmoteComponent"/>.
/// A mob can be hit by several different forced emotes at once (e.g. from separate artifact nodes),
/// so each auto-emote is tracked with its own end time and removed independently. Storing only a
/// single emote would let a second fit overwrite the first, orphaning it so it never stops. The
/// component is removed once no fits remain.
/// </summary>
[RegisterComponent, Access(typeof(XAEForcedEmoteSystem))]
public sealed partial class ForcedEmoteFitComponent : Component
{
    /// <summary>
    /// The active forced emotes and the time each one ends, keyed by the AutoEmote to remove.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<AutoEmotePrototype>, TimeSpan> EndTimes = new();
}
