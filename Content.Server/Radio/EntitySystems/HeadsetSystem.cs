using Content.Server.Station.Systems;
using Content.Shared.Chat;
using Content.Shared.CrewAssignments.Components;
using Content.Shared.CrewRecords.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Radio.EntitySystems;
using Content.Shared.Station.Components;
using Content.Shared.Mind;
using Robust.Server.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using System.Linq;

namespace Content.Server.Radio.EntitySystems;

public sealed class HeadsetSystem : SharedHeadsetSystem
{
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HeadsetComponent, RadioReceiveEvent>(OnHeadsetReceive);
        SubscribeLocalEvent<HeadsetComponent, EncryptionChannelsChangedEvent>(OnKeysChanged);

        SubscribeLocalEvent<WearingHeadsetComponent, EntitySpokeEvent>(OnSpeak);
        Subs.BuiEvents<HeadsetComponent>(HeadsetMenuUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<HeadsetMenuInputToggle>(OnToggleInput);
            subs.Event<HeadsetMenuOutputToggle>(OnToggleOutput);
            subs.Event<HeadsetMenuTagsToggle>(OnToggleTags);
        });
    }

    private void UpdateUserInterface(EntityUid uid, HeadsetComponent component, EntityUid player)
    {
        var formattedStations = GetFormattedStations(player);
        component.TransmitTo.RemoveWhere(id => !formattedStations.ContainsKey(id));
        component.RecieveFrom.RemoveWhere(id => !formattedStations.ContainsKey(id));

        var newState = new HeadsetMenuBoundUserInterfaceState(formattedStations, new HashSet<int>(component.TransmitTo), new HashSet<int>(component.RecieveFrom), component.ShowTags);
        _userInterface.SetUiState(uid, HeadsetMenuUiKey.Key, newState);

    }
    private void UpdateUserInterface(EntityUid uid, HeadsetComponent component, BoundUIOpenedEvent args)
    {
        if (!component.Initialized)
            return;
        var player = args.Actor;
        UpdateUserInterface(uid, component, player);
    }

    private void OnToggleInput(EntityUid uid, HeadsetComponent component, HeadsetMenuInputToggle args)
    {
        if (!component.Initialized)
            return;

        var stations = GetFormattedStations(args.Actor);

        if (args.Target == 0)
        {
            if (args.Enabled)
                component.RecieveFrom.Clear();
            else
            {
                // "All factions" unchecked switches from implicit-all to explicit toggles.
                component.RecieveFrom.Clear();
                foreach (var stationId in stations.Keys)
                {
                    component.RecieveFrom.Add(stationId);
                }
            }
        }
        else if (args.Enabled)
        {
            component.RecieveFrom.Add(args.Target);
        }
        else
        {
            component.RecieveFrom.Remove(args.Target);
        }

        Dirty(uid, component);
        var player = args.Actor;
        UpdateUserInterface(uid, component, player);
    }

    private void OnToggleOutput(EntityUid uid, HeadsetComponent component, HeadsetMenuOutputToggle args)
    {
        if (!component.Initialized)
            return;

        var stations = GetFormattedStations(args.Actor);

        if (args.Target == 0)
        {
            if (args.Enabled)
                component.TransmitTo.Clear();
            else
            {
                // "All factions" unchecked switches from implicit-all to explicit toggles.
                component.TransmitTo.Clear();
                foreach (var stationId in stations.Keys)
                {
                    component.TransmitTo.Add(stationId);
                }
            }
        }
        else if (args.Enabled)
        {
            component.TransmitTo.Add(args.Target);
        }
        else
        {
            component.TransmitTo.Remove(args.Target);
        }

        Dirty(uid, component);
        var player = args.Actor;
        UpdateUserInterface(uid, component, player);
    }

    private void OnToggleTags(EntityUid uid, HeadsetComponent component, HeadsetMenuTagsToggle args)
    {
        if (!component.Initialized)
            return;

        component.ShowTags = args.Enabled;

        Dirty(uid, component);
        var player = args.Actor;
        UpdateUserInterface(uid, component, player);
    }

    private Dictionary<int, string> GetFormattedStations(EntityUid player)
    {
        Dictionary<int, string> formattedStations = new();

        if (!_mind.TryGetMind(player, out _, out var mind) || string.IsNullOrWhiteSpace(mind.CharacterName))
            return formattedStations;

        var recordKey = mind.CharacterName;

        foreach (var station in _station.GetStations())
        {
            if (!TryComp<CrewRecordsComponent>(station, out var crewRecords) || !crewRecords.TryGetRecord(recordKey, out _))
                continue;

            if (!TryComp<StationDataComponent>(station, out var data) || data.StationName == null)
                continue;

            formattedStations[data.UID] = data.StationName;
        }

        return formattedStations;
    }
    private void OnKeysChanged(EntityUid uid, HeadsetComponent component, EncryptionChannelsChangedEvent args)
    {
        UpdateRadioChannels(uid, component, args.Component);
    }

    private void UpdateRadioChannels(EntityUid uid, HeadsetComponent headset, EncryptionKeyHolderComponent? keyHolder = null)
    {
        // make sure to not add ActiveRadioComponent when headset is being deleted
        if (!headset.Enabled || MetaData(uid).EntityLifeStage >= EntityLifeStage.Terminating)
            return;

        EnsureComp<ActiveRadioComponent>(uid);
    }

    public bool HasChannelAccess(EntityUid player, EntityUid faction, RadioChannelPrototype channel)
    {
        if (!_mind.TryGetMind(player, out _, out var mind) || string.IsNullOrWhiteSpace(mind.CharacterName))
            return false;

        var recordKey = mind.CharacterName;

        if (TryComp<StationDataComponent>(faction, out var sD) && sD != null)
        {
            if (sD.RadioData.ContainsKey(channel.ID))
            {
                if (sD.RadioData.TryGetValue(channel.ID, out var data) && data != null)
                {
                    if (!data.Enabled) return false;
                    if (data.Access.Count <= 0) return true;

                    if (TryComp<CrewRecordsComponent>(faction, out var crewRecords) && crewRecords != null)
                    {
                        if (crewRecords.TryGetRecord(recordKey, out var crewRecord) && crewRecord != null)
                        {
                            if (TryComp<CrewAssignmentsComponent>(faction, out var crewAssignments) && crewAssignments != null)
                            {
                                if (crewAssignments.TryGetAssignment(crewRecord.AssignmentID, out var crewAssignment) && crewAssignment != null)
                                {
                                    foreach (var access in data.Access)
                                    {
                                        if (crewAssignment.AccessIDs.Contains(access)) return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        return false;
    }

    private void OnSpeak(EntityUid uid, WearingHeadsetComponent component, EntitySpokeEvent args)
    {
        if (args.Channel != null)
        {
            if (TryComp<HeadsetComponent>(component.Headset, out var headsetComp) && headsetComp != null)
            {
                if (!args.Channel.Encrypted)
                {
                    _radio.SendRadioMessage(uid, args.Message, args.Channel, component.Headset);
                    args.Channel = null; // prevent duplicate messages from other listeners.
                    return;
                }

                // Persistence 14: Use resolved transmit factions from the shared helper so encrypted custom channels work for explicit selections and for the UI's implicit "all factions" mode.
                var transmitStations = GetTransmitStations(args.Source, headsetComp).ToList();
                if (transmitStations.Count <= 0)
                    return;

                if (args.EncryptionID is { } targetedEncryptionId && targetedEncryptionId > 0)
                {
                    var targetedFaction = _station.GetStationByID(targetedEncryptionId);
                    if (targetedFaction == null || !transmitStations.Contains(targetedFaction.Value))
                        return;

                    if (HasChannelAccess(args.Source, targetedFaction.Value, args.Channel))
                    {
                        _radio.SendRadioMessage(uid, args.Message, args.Channel, component.Headset, encryptionID: targetedEncryptionId);
                        args.Channel = null; // prevent duplicate messages from other listeners.
                    }

                    return;
                }

                var sent = false;
                foreach (var faction in transmitStations)
                {
                    if (TryComp<StationDataComponent>(faction, out var stationData)
                        && HasChannelAccess(args.Source, faction, args.Channel))
                    {
                        _radio.SendRadioMessage(uid, args.Message, args.Channel, component.Headset, encryptionID: stationData.UID);
                        sent = true;
                    }
                }

                if (sent)
                    args.Channel = null; // prevent duplicate messages from other listeners.
                return;
                // End Persistence 14

            }
        }
    }

    protected override void OnGotEquipped(EntityUid uid, HeadsetComponent component, GotEquippedEvent args)
    {
        base.OnGotEquipped(uid, component, args);
        if (component.IsEquipped && component.Enabled)
        {
            EnsureComp<WearingHeadsetComponent>(args.Equipee).Headset = uid;
            UpdateRadioChannels(uid, component);
        }
    }

    protected override void OnGotUnequipped(EntityUid uid, HeadsetComponent component, GotUnequippedEvent args)
    {
        base.OnGotUnequipped(uid, component, args);
        RemComp<ActiveRadioComponent>(uid);
        RemComp<WearingHeadsetComponent>(args.Equipee);
    }

    public void SetEnabled(EntityUid uid, bool value, HeadsetComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.Enabled == value)
            return;

        component.Enabled = value;
        Dirty(uid, component);

        if (!value)
        {
            RemCompDeferred<ActiveRadioComponent>(uid);

            if (component.IsEquipped)
                RemCompDeferred<WearingHeadsetComponent>(Transform(uid).ParentUid);
        }
        else if (component.IsEquipped)
        {
            EnsureComp<WearingHeadsetComponent>(Transform(uid).ParentUid).Headset = uid;
            UpdateRadioChannels(uid, component);
        }
    }

    private void OnHeadsetReceive(EntityUid uid, HeadsetComponent component, ref RadioReceiveEvent args)
    {
        // TODO: change this when a code refactor is done
        // this is currently done this way because receiving radio messages on an entity otherwise requires that entity
        // to have an ActiveRadioComponent

        var parent = Transform(uid).ParentUid;

        if (parent.IsValid())
        {
            var relayEvent = new HeadsetRadioReceiveRelayEvent(args);
            RaiseLocalEvent(parent, ref relayEvent);
        }

        if (TryComp(parent, out ActorComponent? actor) && actor != null && actor.PlayerSession != null)
            _netMan.ServerSendMessage(args.ChatMsg, actor.PlayerSession.Channel);
    }
}
