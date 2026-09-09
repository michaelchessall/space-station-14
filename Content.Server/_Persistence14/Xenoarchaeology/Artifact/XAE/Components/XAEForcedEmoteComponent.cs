using Content.Server.Xenoarchaeology.Artifact.XAE;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Xenoarchaeology.Artifact.XAE.Components;

/// <summary>
/// Xeno artifact effect: forces every alive mob within <see cref="Radius"/> into a repeated emote
/// "fit" (e.g. uncontrollable laughter) for <see cref="Duration"/>. Line of sight is not required.
/// The repeat interval and per-tick probability come from the referenced <see cref="AutoEmote"/>
/// prototype, reusing the game's AutoEmote machinery (as used by cluwnes/zombies/etc.).
/// </summary>
[RegisterComponent, Access(typeof(XAEForcedEmoteSystem))]
public sealed partial class XAEForcedEmoteComponent : Component
{
    /// <summary>
    /// The auto-emote forced onto affected mobs. It defines which emote, the interval, and the
    /// per-interval probability. Keep its `force` false so only mobs able to perform the emote do.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<AutoEmotePrototype> AutoEmote;

    /// <summary>
    /// Radius (in tiles) around the artifact within which mobs are affected. Line of sight is ignored.
    /// </summary>
    [DataField]
    public float Radius = 5f;

    /// <summary>
    /// How long the fit lasts. AutoEmote has no lifetime of its own, so this system times it out.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);
}
