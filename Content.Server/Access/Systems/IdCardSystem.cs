using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.CrewRecords.Systems;
using Content.Server.Kitchen.Components;
using Content.Server.Kitchen.EntitySystems;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;
using Content.Shared.CrewAssignments.Components;
using Content.Shared.CrewRecords.Components;
using Content.Shared.Database;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;
using System.Xml.Linq;

namespace Content.Server.Access.Systems;

public sealed class IdCardSystem : SharedIdCardSystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly MicrowaveSystem _microwave = default!;
    [Dependency] private readonly CrewMetaRecordsSystem _crewMeta = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IdCardComponent, BeingMicrowavedEvent>(OnMicrowaved);
        SubscribeLocalEvent<IdCardComponent, ComponentInit>(OnCompInit);
    }

    private void OnCompInit(EntityUid uid, IdCardComponent id, ComponentInit args)
    {
        if (id.CreatedTime == null)
        {
            id.CreatedTime = DateTime.Now;
        }
        else
        {
            if(_crewMeta.MetaRecords != null && id.FullName != null)
            {
                if (_crewMeta.MetaRecords.TryGetRecord(id.FullName, out var record))
                {
                    if(record != null && id.CreatedTime < record.LatestIDTime)
                    {
                        id.FullName = "*Expired*";
                        id.LocalizedJobTitle = "*Expired*";
                        UpdateEntityName(uid, id);
                    }
                }
            }
        }
        if(id.FullName != "*Expired*" && id.FullName != null && id.FullName != "")
        {
            RebuildJob(uid, id);
        }
        
    }

    private void OnMicrowaved(EntityUid uid, IdCardComponent component, BeingMicrowavedEvent args)
    {
        if (!component.CanMicrowave || !TryComp<MicrowaveComponent>(args.Microwave, out var micro) || micro.Broken)
            return;

        if (TryComp<AccessComponent>(uid, out var access))
        {
            float randomPick = _random.NextFloat();

            // if really unlucky, burn card
            if (randomPick <= 0.15f)
            {
                TryComp(uid, out TransformComponent? transformComponent);
                if (transformComponent != null)
                {
                    _popupSystem.PopupCoordinates(Loc.GetString("id-card-component-microwave-burnt", ("id", uid)),
                     transformComponent.Coordinates, PopupType.Medium);
                    Spawn("FoodBadRecipe",
                        transformComponent.Coordinates);
                }
                _adminLogger.Add(LogType.Action, LogImpact.Medium,
                    $"{ToPrettyString(args.Microwave)} burnt {ToPrettyString(uid):entity}");
                QueueDel(uid);
                return;
            }

            //Explode if the microwave can't handle it
            if (!micro.CanMicrowaveIdsSafely)
            {
                _microwave.Explode((args.Microwave, micro));
                return;
            }

            // If they're unlucky, brick their ID
            if (randomPick <= 0.25f)
            {
                _popupSystem.PopupEntity(Loc.GetString("id-card-component-microwave-bricked", ("id", uid)), uid);

                access.Tags.Clear();
                Dirty(uid, access);

                _adminLogger.Add(LogType.Action, LogImpact.Medium,
                    $"{ToPrettyString(args.Microwave)} cleared access on {ToPrettyString(uid):entity}");
            }
            else
            {
                _popupSystem.PopupEntity(Loc.GetString("id-card-component-microwave-safe", ("id", uid)), uid, PopupType.Medium);
            }

            // Give them a wonderful new access to compensate for everything
            var ids = _prototypeManager.EnumeratePrototypes<AccessLevelPrototype>().Where(x => x.CanAddToIdCard).ToArray();

            if (ids.Length == 0)
                return;

            var random = _random.Pick(ids);

            access.Tags.Add(random.ID);
            Dirty(uid, access);

            _adminLogger.Add(LogType.Action, LogImpact.High,
                    $"{ToPrettyString(args.Microwave)} added {random.ID} access to {ToPrettyString(uid):entity}");

        }
    }

    public void ExpireAllIds(string name)
    {
        var query = EntityQueryEnumerator<IdCardComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if(comp.FullName == name)
            {
                if(comp.CreatedTime < DateTime.Now)
                {
                    comp.FullName = "*Expired*";
                    comp.LocalizedJobTitle = "*Expired*";
                    UpdateEntityName(uid, comp);
                }
            }

        }
    }

    public override void ExpireId(Entity<ExpireIdCardComponent> ent)
    {
        if (ent.Comp.Expired)
            return;

        base.ExpireId(ent);

        if (ent.Comp.ExpireMessage != null)
        {
            _chat.TrySendInGameICMessage(
                ent,
                Loc.GetString(ent.Comp.ExpireMessage),
                InGameICChatType.Speak,
                ChatTransmitRange.Normal,
                true);
        }
    }

    public void BuildID(EntityUid card, string name)
    {
        if(TryComp<IdCardComponent>(card, out var comp))
        {
            comp.FullName = name;
            RebuildJob(card, comp);
            UpdateEntityName(card, comp);
        } 
    }

    public void RebuildJob(EntityUid card, IdCardComponent comp)
    {
        if (comp.FullName == null || comp.stationID == null) return;
        var station = _station.GetStationByID(comp.stationID.Value);
        if (station == null) return;
        if (TryComp<CrewRecordsComponent>(station, out var crewRecords))
        {
            if (crewRecords.TryGetRecord(comp.FullName, out var crewRecord) && crewRecord != null)
            {
                if (TryComp<CrewAssignmentsComponent>(station, out var crewAssignments))
                {
                    if (crewAssignments.TryGetAssignment(crewRecord.AssignmentID, out var crewAssignment) && crewAssignment != null)
                    {
                        comp.LocalizedJobTitle = crewAssignment.Name;
                    }
                }
            }
        }
    }
}
