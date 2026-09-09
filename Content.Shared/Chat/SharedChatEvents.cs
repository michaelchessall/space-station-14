using Content.Shared.Inventory;
using Content.Shared.Radio;
using Content.Shared.Speech;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chat;

/// <summary>
/// This event should be sent everytime an entity talks (Radio, local chat, etc...).
/// The event is sent to both the entity itself, and all clothing (For stuff like voice masks).
/// </summary>
public sealed class TransformSpeakerNameEvent : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = SlotFlags.WITHOUT_POCKET;
    public EntityUid Sender;
    public string VoiceName;
    public ProtoId<SpeechVerbPrototype>? SpeechVerb;

    public TransformSpeakerNameEvent(EntityUid sender, string name)
    {
        Sender = sender;
        VoiceName = name;
        SpeechVerb = null;
    }
}

/// <summary>
/// Raised broadcast in order to transform speech.transmit
/// </summary>
public sealed class TransformSpeechEvent : CancellableEntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = SlotFlags.WITHOUT_POCKET;
    public EntityUid Sender;
    public string Message;

    public TransformSpeechEvent(EntityUid sender, string message)
    {
        Sender = sender;
        Message = message;
    }
}

public sealed class CheckIgnoreSpeechBlockerEvent : EntityEventArgs
{
    public EntityUid Sender;
    public bool IgnoreBlocker;

    public CheckIgnoreSpeechBlockerEvent(EntityUid sender, bool ignoreBlocker)
    {
        Sender = sender;
        IgnoreBlocker = ignoreBlocker;
    }
}

/// <summary>
/// Raised on an entity right before it speaks or whispers (see the "say"/"whisper" commands),
/// letting any system override the ChatTransmitRange that will actually be used for the
/// message - e.g. to suppress the visible speech bubble for an entity whose speech should be
/// heard/relayed some other way instead (see the Eye anomaly's hivemind vessel, which relays its
/// speech through every tethered body rather than being audible at its own position). Starts at
/// whatever the command's own default would have been; leave Range unchanged to fall back to
/// that default. Deliberately generic/reusable - the commands that raise this know nothing about
/// any specific reason a system might want to override the range.
/// </summary>
public sealed class GetSpeechTransmitRangeEvent : EntityEventArgs
{
    public ChatTransmitRange Range;

    public GetSpeechTransmitRangeEvent(ChatTransmitRange range)
    {
        Range = range;
    }
}

/// <summary>
/// Raised on an entity when it speaks, either through 'say' or 'whisper'.
/// </summary>
public sealed class EntitySpokeEvent : EntityEventArgs
{
    public readonly EntityUid Source;
    public readonly string Message;
    public readonly string? ObfuscatedMessage; // not null if this was a whisper
    public readonly int? EncryptionID;

    /// <summary>
    /// If the entity was trying to speak into a radio, this was the channel they were trying to access. If a radio
    /// message gets sent on this channel, this should be set to null to prevent duplicate messages.
    /// </summary>
    public RadioChannelPrototype? Channel;

    public EntitySpokeEvent(EntityUid source, string message, RadioChannelPrototype? channel, string? obfuscatedMessage, int? encryptionID = null)
    {
        Source = source;
        Message = message;
        Channel = channel;
        ObfuscatedMessage = obfuscatedMessage;
        EncryptionID = encryptionID;
    }
}
