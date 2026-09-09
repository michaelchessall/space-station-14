using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers; // Persistence: Chat stacking from RMC14 - pull/7587
using Content.Server.Chat.Systems;
using Content.Server.Power.Components;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Chat; // Persistence: Chat stacking from RMC14 - pull/7587
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Speech;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Replays;
using Robust.Shared.Utility;
using System.Diagnostics;
using System.Linq;

namespace Content.Server.Radio.EntitySystems;

/// <summary>
///     This system handles intrinsic radios and the general process of converting radio messages into chat messages.
/// </summary>
public sealed class RadioSystem : EntitySystem
{
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IReplayRecordingManager _replay = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly HeadsetSystem _headset = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IChatManager _chatManager = default!; // Persistence: Chat stacking from RMC14 - pull/7587
    // set used to prevent radio feedback loops.
    private readonly HashSet<string> _messages = new();

    private EntityQuery<TelecomExemptComponent> _exemptQuery;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IntrinsicRadioReceiverComponent, RadioReceiveEvent>(OnIntrinsicReceive);
        SubscribeLocalEvent<IntrinsicRadioTransmitterComponent, EntitySpokeEvent>(OnIntrinsicSpeak);

        SubscribeLocalEvent<TelecomServerComponent, ExaminedEvent>(OnServerExamined);

        _exemptQuery = GetEntityQuery<TelecomExemptComponent>();
    }

    private void OnServerExamined(Entity<TelecomServerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(TelecomServerComponent)))
        {
            args.PushMarkup($"Range: {ent.Comp.MaxRange:F0}m");
        }
    }

    private void OnIntrinsicSpeak(EntityUid uid, IntrinsicRadioTransmitterComponent component, EntitySpokeEvent args)
    {
        if (args.Channel != null && component.Channels.Contains(args.Channel.ID))
        {
            SendRadioMessage(uid, args.Message, args.Channel, uid);
            args.Channel = null; // prevent duplicate messages from other listeners.
        }
    }

    private void OnIntrinsicReceive(EntityUid uid, IntrinsicRadioReceiverComponent component, ref RadioReceiveEvent args)
    {
        if (TryComp(uid, out ActorComponent? actor))
            _netMan.ServerSendMessage(args.ChatMsg, actor.PlayerSession.Channel);
    }

    /// <summary>
    /// Send radio message to all active radio listeners
    /// </summary>
    public void SendRadioMessage(EntityUid messageSource, string message, ProtoId<RadioChannelPrototype> channel, EntityUid radioSource, bool escapeMarkup = true, bool useNetworkOverride = true)
    {
        SendRadioMessage(messageSource, message, _prototype.Index(channel), radioSource, escapeMarkup: escapeMarkup, useNetworkOverride);
    }

    /// <summary>
    /// Send radio message to all active radio listeners
    /// </summary>
    /// <param name="messageSource">Entity that spoke the message</param>
    /// <param name="radioSource">Entity that picked up the message and will send it, e.g. headset</param>
    public void SendRadioMessage(EntityUid messageSource, string message, RadioChannelPrototype channel, EntityUid radioSource, bool escapeMarkup = true, bool useNetworkOverride = true)
    {
        // TODO if radios ever garble / modify messages, feedback-prevention needs to be handled better than this.
        if (!_messages.Add(message))
            return;

        var debugWatch = Stopwatch.StartNew();

        var evt = new TransformSpeakerNameEvent(messageSource, MetaData(messageSource).EntityName);
        RaiseLocalEvent(messageSource, evt);

        var name = evt.VoiceName;
        name = FormattedMessage.EscapeText(name);

        SpeechVerbPrototype speech;
        if (evt.SpeechVerb != null && _prototype.Resolve(evt.SpeechVerb, out var evntProto))
            speech = evntProto;
        else
            speech = _chat.GetSpeechVerb(messageSource, message);

        var content = escapeMarkup
            ? FormattedMessage.EscapeText(message)
            : message;

        var wrappedMessage = Loc.GetString(speech.Bold ? "chat-radio-message-wrap-bold" : "chat-radio-message-wrap",
            ("color", channel.Color),
            ("fontType", speech.FontId),
            ("fontSize", speech.FontSize),
            ("verb", Loc.GetString(_random.Pick(speech.SpeechVerbStrings))),
            ("channel", $"\\[{channel.LocalizedName}\\]"),
            ("name", name),
            ("message", content));

        // most radios are relayed to chat, so lets parse the chat message beforehand
        var chat = new ChatMessage(
            ChatChannel.Radio,
            message,
            wrappedMessage,
            GetNetEntity(messageSource), // Persistence: Chat stacking from RMC14 - pull/7587
            _chatManager.EnsurePlayer(CompOrNull<ActorComponent>(messageSource)?.PlayerSession.UserId)?.Key,  // Persistence: Chat stacking from RMC14 - pull/7587
            repeatCheckSender: !HasComp<ChatRepeatIgnoreSenderComponent>(radioSource));  // Persistence: Chat stacking from RMC14 - pull/7587
        var chatMsg = new MsgChatMessage { Message = chat };
        var ev = new RadioReceiveEvent(message, messageSource, channel, radioSource, chatMsg);

        var sendAttemptEv = new RadioSendAttemptEvent(channel, radioSource);
        RaiseLocalEvent(ref sendAttemptEv);
        RaiseLocalEvent(radioSource, ref sendAttemptEv);
        var canSend = !sendAttemptEv.Cancelled;

        var sourceTransform = Transform(radioSource);
        var sourceMapId = sourceTransform.MapID;
        var hasActiveServer = false;
        var sourceServerExempt = _exemptQuery.HasComp(radioSource);

        // Relay network
        var useNetwork = _cfg.GetCVar(CCVars.TCommsUseNetwork) && useNetworkOverride;
        NetworkGraph? network = null;
        NetworkNode? transmitterNode = null;
        if (useNetwork)
        {
            network = NetworkGraph.BuildGraph(
                EntityQuery<TelecomServerComponent, EncryptionKeyHolderComponent, ApcPowerReceiverComponent, TransformComponent>()
                    .Select(entry =>
                    {
                        var (telecomServer, keys, power, transform) = entry;
                        return new NetworkNode()
                        {
                            IsPowered = power.Powered,
                            Range = telecomServer.MaxRange,
                            MapCoordinates = _xform.GetMapCoordinates(transform)
                        };
                    })
            );
            transmitterNode = new NetworkNode()
            {
                IsPowered = true,
                Range = float.PositiveInfinity,
                MapCoordinates = _xform.GetMapCoordinates(sourceTransform)
            };
        }
        else
            hasActiveServer = true;// HasActiveServer(sourceMapId, channel.ID);

        var radioQuery = EntityQueryEnumerator<ActiveRadioComponent, TransformComponent>();
        var encryptionID = 0;
        if (TryComp<HeadsetComponent>(radioSource, out var headset) && headset != null)
        {
            encryptionID = headset.TransmitTo;
        }
        while (canSend && radioQuery.MoveNext(out var receiver, out var radio, out var transform))
        {
            if (TryComp<IntercomComponent>(receiver, out _)) continue;
#pragma warning disable CS0642 // Possible mistaken empty statement
            if (encryptionID == 0 && !channel.Encrypted) ;
#pragma warning restore CS0642 // Possible mistaken empty statement

            else if (TryComp<HeadsetComponent>(receiver, out var targetHeadset) && targetHeadset != null)
            {
                if (targetHeadset.RecieveFrom != 0 && targetHeadset.RecieveFrom != encryptionID)
                {
                    continue;
                }
                var station = _station.GetStationByID(encryptionID);
                if (station == null) continue;
                var parent = Transform(receiver).ParentUid;

                if (parent.IsValid())
                {
                    if (!_headset.HasChannelAccess(parent, station.Value, channel))
                    {
                        continue;
                    }
                }
                else
                {
                    continue;
                }

            }
            else if (!radio.ReceiveAllChannels)
            {
                if (!radio.Channels.Contains(channel.ID) || (TryComp<IntercomComponent>(receiver, out var intercom) &&
                                                             !intercom.SupportedChannels.Contains(channel.ID)))
                    continue;
            }

            if (!channel.LongRange && transform.MapID != sourceMapId && !radio.GlobalReceive)
                continue;

            // don't need telecom server for long range channels or handheld radios and intercoms
            var needServer = !channel.LongRange && !sourceServerExempt;
            if (!useNetwork && needServer && !hasActiveServer)
                continue;
            else if (useNetwork && needServer)
            {
                var receiverNode = new NetworkNode()
                {
                    IsPowered = true,
                    Range = float.PositiveInfinity,
                    MapCoordinates = _xform.GetMapCoordinates(transform)
                };
                if (!network!.CanTransmit(transmitterNode!, receiverNode))
                    continue;
            }

            // check if message can be sent to specific receiver
            var attemptEv = new RadioReceiveAttemptEvent(channel, radioSource, receiver);
            RaiseLocalEvent(ref attemptEv);
            RaiseLocalEvent(receiver, ref attemptEv);
            if (attemptEv.Cancelled)
                continue;

            // send the message
            RaiseLocalEvent(receiver, ref ev);
        }

        debugWatch.Stop();
        var debugDuration = debugWatch.ElapsedMilliseconds;

        if (name != Name(messageSource))
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Radio message from {ToPrettyString(messageSource):user} as {name} on {channel.LocalizedName} ({debugDuration:F0}ms): {message}");
        else
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Radio message from {ToPrettyString(messageSource):user} on {channel.LocalizedName} ({debugDuration:F0}ms): {message}");

        _replay.RecordServerMessage(chat);
        _messages.Remove(message);
    }

    /// <inheritdoc cref="TelecomServerComponent"/>
    private bool HasActiveServer(MapId mapId, string channelId)
    {
        var servers = EntityQuery<TelecomServerComponent, EncryptionKeyHolderComponent, ApcPowerReceiverComponent, TransformComponent>();
        foreach (var (_, keys, power, transform) in servers)
        {
            if (transform.MapID == mapId &&
                power.Powered &&
                keys.Channels.Contains(channelId))
            {
                return true;
            }
        }
        return false;
    }

}

public sealed class NetworkNode
{
    public MapCoordinates MapCoordinates { get; set; }
    public float Range { get; set; }
    public bool IsPowered { get; set; }

    public bool InRange(NetworkNode other)
    {
        var maxRange = Math.Min(Range, other.Range);
        if (maxRange == float.PositiveInfinity) return true;
        return MapCoordinates.InRange(other.MapCoordinates, maxRange);
    }
}

public sealed class NetworkGraph
{
    private readonly Dictionary<NetworkNode, List<NetworkNode>> _adjency = new();
    public IReadOnlyDictionary<NetworkNode, List<NetworkNode>> Adjency => _adjency;

    public static NetworkGraph BuildGraph(IEnumerable<NetworkNode> relays)
    {
        var relayList = relays.Where(r => r.IsPowered).ToList();

        var graph = new NetworkGraph();

        foreach (var relay in relayList)
            graph._adjency[relay] = new();

        for (var i = 0; i < relayList.Count; i++)
        {
            for (var j = i + 1; j < relayList.Count; j++)
            {
                var a = relayList[i];
                var b = relayList[j];


                if (a.InRange(b))
                {
                    graph._adjency[a].Add(b);
                    graph._adjency[b].Add(a);
                }
            }
        }

        return graph;
    }

    public IEnumerable<NetworkNode> GetNodesInRange(NetworkNode current)
    {
        foreach (var relay in _adjency.Keys)
        {
            if (!relay.IsPowered) continue;
            if (relay.InRange(current))
                yield return relay;
        }
    }

    public bool CanTransmit(NetworkNode transmitter, NetworkNode receiver)
    {
        var visited = new HashSet<NetworkNode>();
        var queue = new Queue<NetworkNode>();

        foreach (var relay in GetNodesInRange(transmitter))
        {
            visited.Add(relay);
            queue.Enqueue(relay);
        }

        while (queue.Count > 0)
        {
            var relay = queue.Dequeue();
            if (relay.InRange(receiver))
                return true;

            foreach (var neighbour in _adjency[relay])
            {
                if (visited.Add(neighbour))
                    queue.Enqueue(neighbour);
            }
        }
        return false;
    }
}
