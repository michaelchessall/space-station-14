using Content.Shared.Emp;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Chat;
using Content.Shared.Mind;
using Content.Shared.Radio.Components;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using Robust.Shared.Prototypes;
using System.Collections.Generic;
using System.Linq;

namespace Content.Shared.Radio.EntitySystems;

public abstract class SharedHeadsetSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeadsetComponent, InventoryRelayedEvent<GetDefaultRadioChannelEvent>>(OnGetDefault);
        SubscribeLocalEvent<WearingHeadsetComponent, ResolveCustomRadioChannelEvent>(OnResolveCustomChannel);
        SubscribeLocalEvent<HeadsetComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<HeadsetComponent, GotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<HeadsetComponent, EmpPulseEvent>(OnEmpPulse);
    }

    private void OnResolveCustomChannel(EntityUid uid, WearingHeadsetComponent component, ResolveCustomRadioChannelEvent args)
    {
        if (!TryComp(component.Headset, out HeadsetComponent? headset))
            return;

        // Persistence 14: Gather every faction match for a custom hotkey so ambiguous same-slot channels can either target one faction or intentionally fan out across all selected factions.
        var key = char.ToLowerInvariant(args.Key);
        var matches = new List<(ProtoId<RadioChannelPrototype> ChannelId, int StationId)>();

        foreach (var station in GetTransmitStations(uid, headset))
        {
            if (!TryComp<StationDataComponent>(station, out var stationData))
                continue;

            foreach (var (channelId, data) in stationData.RadioData)
            {
                if (!data.Enabled || data.Hotkey == '\0')
                    continue;

                if (char.ToLowerInvariant(data.Hotkey) != key)
                    continue;

                matches.Add((channelId, stationData.UID));
            }
        }

        if (matches.Count == 0)
            return;

        args.Channel = matches[0].ChannelId;

        // If this hotkey maps to several selected factions for the same channel id,
        // leave EncryptionID unset so downstream logic can transmit to all selected factions.
        if (matches.Count == 1)
        {
            args.EncryptionID = matches[0].StationId;
            return;
        }

        var firstChannelId = matches[0].ChannelId;
        if (matches.All(m => m.ChannelId == firstChannelId))
        {
            args.EncryptionID = null;
            return;
        }

        // Different channel ids sharing a key are ambiguous; keep deterministic first match.
        args.EncryptionID = matches[0].StationId;
        // End Persistence 14
    }

    protected IEnumerable<EntityUid> GetTransmitStations(EntityUid wearer, HeadsetComponent headset)
    {
        // Persistence 14: Treat an empty transmit selection as the headset UI's implicit "all factions" mode by resolving every faction available to the wearer.
        if (headset.TransmitTo.Count > 0)
        {
            foreach (var stationId in headset.TransmitTo.Order())
            {
                var station = _station.GetStationByID(stationId);
                if (station != null)
                    yield return station.Value;
            }

            yield break;
        }

        if (!_mind.TryGetMind(wearer, out _, out var mind) || string.IsNullOrWhiteSpace(mind.CharacterName))
            yield break;

        foreach (var station in _station.GetStationsAvailableTo(mind.CharacterName))
        {
            yield return station;
        }
        // End Persistence 14
    }

    private void OnGetDefault(EntityUid uid, HeadsetComponent component, InventoryRelayedEvent<GetDefaultRadioChannelEvent> args)
    {
        if (!component.Enabled || !component.IsEquipped)
        {
            // don't provide default channels from pocket slots.
            return;
        }

        if (TryComp(uid, out EncryptionKeyHolderComponent? keyHolder))
            args.Args.Channel ??= keyHolder.DefaultChannel;
    }

    protected virtual void OnGotEquipped(EntityUid uid, HeadsetComponent component, GotEquippedEvent args)
    {
        component.IsEquipped = args.SlotFlags.HasFlag(component.RequiredSlot);
        Dirty(uid, component);
    }

    protected virtual void OnGotUnequipped(EntityUid uid, HeadsetComponent component, GotUnequippedEvent args)
    {
        component.IsEquipped = false;
        Dirty(uid, component);
    }

    private void OnEmpPulse(Entity<HeadsetComponent> ent, ref EmpPulseEvent args)
    {
        if (ent.Comp.Enabled)
        {
            args.Affected = true;
            args.Disabled = true;
        }
    }
}
