using Robust.Shared.Prototypes;
using Content.Shared.Radio;

namespace Content.Shared.Chat;

/// <summary>
/// Resolves a station-specific custom radio channel for a typed key prefix.
/// </summary>
public sealed class ResolveCustomRadioChannelEvent : EntityEventArgs
{
    public char Key;
    public ProtoId<RadioChannelPrototype>? Channel;
    public int? EncryptionID;

    public ResolveCustomRadioChannelEvent(char key)
    {
        Key = key;
    }
}
