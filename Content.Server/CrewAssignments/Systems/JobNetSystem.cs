using Content.Server._NF.Bank;
using Content.Server.Chat.Managers;
using Content.Server.Lathe.Components;
using Content.Server.Sound;
using Content.Server.Store.Components;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.CrewAssignments;
using Content.Shared.CrewAssignments.Components;
using Content.Shared.CrewRecords.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Lathe;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Station.Components;
using Content.Shared.Store.Components;
using Content.Shared.Store.Events;
using Content.Shared.UserInterface;
using NetCord;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server.CrewAssignments.Systems;

/// <summary>
/// Manages general interactions with a store and different entities,
/// getting listings for stores, and interfacing with the store UI.
/// </summary>
public sealed partial class JobNetSystem : EntitySystem
{
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedCargoSystem _cargo = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JobNetComponent, ActivatableUIOpenAttemptEvent>(OnJobNetOpenAttempt);
        SubscribeLocalEvent<JobNetComponent, BeforeActivatableUIOpenEvent>(BeforeActivatableUiOpen);

        SubscribeLocalEvent<JobNetComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<JobNetComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<JobNetComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<JobNetComponent, OpenJobNetImplantEvent>(OnImplantActivate);
        SubscribeLocalEvent<JobNetComponent, JobNetSelectMessage>(OnSelect);

        InitializeUi();
    }
    private void OnSelect(EntityUid uid, JobNetComponent component, JobNetSelectMessage args)
    {
        var station = _station.GetStationByID(args.ID);
        if(station == null || args.ID == 0)
        {
            component.WorkingFor = 0;
            return;
        }

        if (TryComp<CrewRecordsComponent>(station, out var crewRecord) && crewRecord != null)
        {
            if (crewRecord.TryGetRecord(Name(args.Actor), out var record) && record != null)
            {
                if (TryComp<StationDataComponent>(station, out var stationData))
                {
                    if (TryComp<CrewAssignmentsComponent>(station, out var crewAssignments))
                    {
                        if (crewAssignments.TryGetAssignment(record.AssignmentID, out var assignment) && assignment != null)
                        {
                            if (component.LastWorkedFor != stationData.UID)
                                component.WorkedTime = TimeSpan.Zero;
                            component.WorkingFor = stationData.UID;
                            UpdateUserInterface(args.Actor, uid, component);
                        }
                    }
                }
            }
        }
    }

    private void OnJobNetOpenAttempt(EntityUid uid, JobNetComponent component, ActivatableUIOpenAttemptEvent args)
    {
        if (!_mind.TryGetMind(args.User, out var mind, out _))
            return;

        _popup.PopupEntity("Job Network Not Available.", uid, args.User);
        args.Cancel();
    }

    private void OnMapInit(EntityUid uid, JobNetComponent component, MapInitEvent args)
    {

    }

    private void OnStartup(EntityUid uid, JobNetComponent component, ComponentStartup args)
    {

    }

    private void OnShutdown(EntityUid uid, JobNetComponent component, ComponentShutdown args)
    {

    }

    private void OnImplantActivate(EntityUid uid, JobNetComponent component, OpenJobNetImplantEvent args)
    {
        ToggleUi(args.Performer, uid, component);
    }
    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<JobNetComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if(comp.WorkingFor != null && comp.WorkingFor != 0)
            {
                comp.WorkedTime += TimeSpan.FromSeconds(frameTime);
                if(comp.WorkedTime > TimeSpan.FromMinutes(20))
                {
                    comp.WorkedTime = TimeSpan.Zero;
                    TryPay(comp.Owner, comp);
                }
            }
        }
        base.Update(frameTime);
    }
    public void TryPay(EntityUid user, JobNetComponent component)
    {

        if (component.WorkingFor == null || component.WorkingFor == 0) return;
        var station = _station.GetStationByID(component.WorkingFor.Value);
        if (station == null)
        {
            component.WorkingFor = 0;
            return;
        }
        EntityUid? player = null;
        if(TryComp<TransformComponent>(user, out var comp) && comp != null)
        {
            player = comp.ParentUid;
        }
        if (player == null) return;
        var name = Name(player.Value);
        if (TryComp<CrewRecordsComponent>(station, out var crewRecord) && crewRecord != null)
        {
            if (crewRecord.TryGetRecord(name, out var record) && record != null)
            {
                if (TryComp<StationDataComponent>(station, out var stationData))
                {
                    if (TryComp<CrewAssignmentsComponent>(station, out var crewAssignments))
                    {
                        if (crewAssignments.TryGetAssignment(record.AssignmentID, out var assignment) && assignment != null)
                        {
                            if(assignment.Wage > 0)
                            {
                                if(TryComp<ActorComponent>(player, out var actor) && actor != null && actor.PlayerSession != null)
                                {
                                    var bank = _bank.GetMoneyAccountsComponent();
                                    if (bank == null) return;
                                    if(_cargo.TryGetAccount(station.Value, "Cargo", out var money))
                                    {
                                        if (money < assignment.Wage)
                                        {
                                            _audio.PlayEntity(component.ErrorSound, player.Value, player.Value);
                                            var msg = $"{stationData.StationName} has failed to pay you your ${assignment.Wage} due to insufficient funds.";
                                            if (msg != null)
                                                _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Notifications,
                                                    msg,
                                                    msg,
                                                    station.Value,
                                                    false,
                                                    actor.PlayerSession.Channel
                                                    );
                                            return;
                                        }
                                        if (bank.TryGetAccount(name, out var account) && account != null)
                                        {
                                            _audio.PlayEntity(component.PaySuccessSound, player.Value, player.Value);
                                            account.Balance += assignment.Wage;
                                            _cargo.TryAdjustBankAccount(station.Value, "Cargo", -assignment.Wage);
                                            var msg = $"You have received ${assignment.Wage} for working as a {assignment.Name} for {stationData.StationName}.";
                                            if (msg != null)
                                                _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Notifications,
                                                    msg,
                                                    msg,
                                                    station.Value,
                                                    false,
                                                    actor.PlayerSession.Channel
                                                    );
                                        }
                                    }
                                    else
                                    {
                                        _audio.PlayEntity(component.ErrorSound, player.Value, player.Value);
                                        var msg = $"{stationData.StationName} has failed to pay you your ${assignment.Wage} due to an invalid account.";
                                        if (msg != null)
                                            _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Notifications,
                                                msg,
                                                msg,
                                                station.Value,
                                                false,
                                                actor.PlayerSession.Channel
                                                );
                                        return;
                                    }
                                    
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
