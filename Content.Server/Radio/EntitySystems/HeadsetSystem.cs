using Content.Server.Station.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;
using Content.Shared.CrewAssignments.Components;
using Content.Shared.CrewRecords.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Radio.EntitySystems;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Radio.EntitySystems;

public sealed class HeadsetSystem : SharedHeadsetSystem
{
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
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
            subs.Event<HeadsetMenuInputSelect>(OnSelectInput);
            subs.Event<HeadsetMenuOutputSelect>(OnSelectOutput);
        });
    }

    private void UpdateUserInterface(EntityUid uid, HeadsetComponent component, EntityUid player)
    {
        var name = _accessReader.GetIdName(player);
        List<EntityUid> possibleStations = new();
        if (name != null)
        {
            possibleStations = _station.GetStationsAvailableTo(name);
        }
        Dictionary<int, string> formattedStations = new();
        foreach (var station in possibleStations)
        {
            if (TryComp<StationDataComponent>(station, out var data) && data != null)
            {
                if (data.StationName != null) formattedStations.Add(data.UID, data.StationName);
            }
        }
        var newState = new HeadsetMenuBoundUserInterfaceState(formattedStations, component.TransmitTo, component.RecieveFrom);
        _userInterface.SetUiState(uid, HeadsetMenuUiKey.Key, newState);

    }
    private void UpdateUserInterface(EntityUid uid, HeadsetComponent component, BoundUIOpenedEvent args)
    {
        if (!component.Initialized)
            return;
        var player = args.Actor;
        UpdateUserInterface(uid, component, player);
    }

    private void OnSelectInput(EntityUid uid, HeadsetComponent component, HeadsetMenuInputSelect args)
    {
        if (!component.Initialized)
            return;
        var player = args.Actor;
        component.RecieveFrom = args.Target;
        UpdateUserInterface(uid, component, player);
    }
    private void OnSelectOutput(EntityUid uid, HeadsetComponent component, HeadsetMenuOutputSelect args)
    {
        if (!component.Initialized)
            return;
        var player = args.Actor;
        component.TransmitTo = args.Target;
        UpdateUserInterface(uid, component, player);
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

        if (TryComp<StationDataComponent>(faction, out var sD) && sD != null)
        {
            if (sD.RadioData.ContainsKey(channel.ID))
            {
                if (sD.RadioData.TryGetValue(channel.ID, out var data) && data != null)
                {
                    if (!data.Enabled) return false;
                    if (data.Access.Count <= 0) return true;
                    var name = _accessReader.GetIdName(player);
                    if (name != null)
                    {
                        if (sD.Owners.Contains(name)) return true;
                        if (TryComp<CrewRecordsComponent>(faction, out var crewRecords) && crewRecords != null)
                        {
                            if (crewRecords.TryGetRecord(name, out var crewRecord) && crewRecord != null)
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
        }
        return false;
    }

    private void OnSpeak(EntityUid uid, WearingHeadsetComponent component, EntitySpokeEvent args)
    {
        if (args.Channel != null)
        {
            if (TryComp<HeadsetComponent>(component.Headset, out var headsetComp) && headsetComp != null)
            {
                if (!args.Channel.Encrypted && headsetComp.TransmitTo == 0)
                {
                    _radio.SendRadioMessage(uid, args.Message, args.Channel, component.Headset);
                    args.Channel = null; // prevent duplicate messages from other listeners.
                    return;
                }
                else
                {
                    if (headsetComp.TransmitTo == 0) return;
                    var faction = _station.GetStationByID(headsetComp.TransmitTo);
                    if (faction != null)
                    {
                        if (HasChannelAccess(args.Source, faction.Value, args.Channel))
                        {
                            _radio.SendRadioMessage(uid, args.Message, args.Channel, component.Headset);
                            args.Channel = null; // prevent duplicate messages from other listeners.
                            return;
                        }
                    }

                }

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
