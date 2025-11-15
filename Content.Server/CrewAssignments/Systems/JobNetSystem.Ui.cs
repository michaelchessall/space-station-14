using System.Linq;
using Content.Server.Actions;
using Content.Server.Administration.Logs;
using Content.Server.Stack;
using Content.Server.Station.Systems;
using Content.Shared.Actions;
using Content.Shared.CrewAssignments;
using Content.Shared.CrewAssignments.Components;
using Content.Shared.CrewRecords.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Station.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server.CrewAssignments.Systems;

public sealed partial class JobNetSystem
{
    [Dependency] private readonly IAdminLogManager _admin = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly ActionUpgradeSystem _actionUpgrade = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private void InitializeUi()
    {
        SubscribeLocalEvent<JobNetComponent, JobNetRequestUpdateInterfaceMessage>(OnRequestUpdate);
    }

    


    public void ToggleUi(EntityUid user, EntityUid jobnetEnt, JobNetComponent? component = null)
    {
        if (!Resolve(jobnetEnt, ref component))
            return;

        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        if (!_ui.TryToggleUi(jobnetEnt, JobNetUiKey.Key, actor.PlayerSession))
            return;

        UpdateUserInterface(user, jobnetEnt, component);
    }


    public void CloseUi(EntityUid uid, JobNetComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        _ui.CloseUi(uid, JobNetUiKey.Key);
    }

    public void UpdateUserInterface(EntityUid? user, EntityUid jobnet, JobNetComponent? component = null)
    {
        if (!Resolve(jobnet, ref component) || user == null || component == null)
            return;

        Dictionary<int, string> possibleStations = new Dictionary<int, string>();
        var stations = _station.GetStationsSet();
        string? assignmentName = null;
        int? wage = null;
        int selectedstation = 0;
        TimeSpan remainingTime = TimeSpan.FromMinutes(20) - component.WorkedTime;
        foreach (var station in stations)
        {
            if(TryComp<CrewRecordsComponent>(station, out var crewRecord) && crewRecord != null)
            {
                if(crewRecord.TryGetRecord(Name(user.Value), out var record) && record != null)
                {
                    if(TryComp<StationDataComponent>(station, out var stationData))
                    {
                        if (stationData.StationName == null) return;
                        possibleStations.Add(stationData.UID, stationData.StationName);
                        if(component.WorkingFor != null && component.WorkingFor != 0)
                        {
                            if(stationData.UID == component.WorkingFor)
                            {
                                if(TryComp<CrewAssignmentsComponent>(station, out var crewAssignments))
                                {
                                    if(crewAssignments.TryGetAssignment(record.AssignmentID, out var assignment) && assignment != null)
                                    {
                                        assignmentName = assignment.Name;
                                        wage = assignment.Wage;
                                        selectedstation = stationData.UID;
                                    }
                                }
                            }
                        }
                    }
                }
            }


        }
        var state = new JobNetUpdateState(possibleStations, assignmentName, wage, selectedstation, remainingTime);
        _ui.SetUiState(jobnet, JobNetUiKey.Key, state);
    }

    private void OnRequestUpdate(EntityUid uid, JobNetComponent component, JobNetRequestUpdateInterfaceMessage args)
    {
        UpdateUserInterface(args.Actor, GetEntity(args.Entity), component);
    }

    private void BeforeActivatableUiOpen(EntityUid uid, JobNetComponent component, BeforeActivatableUIOpenEvent args)
    {
        UpdateUserInterface(args.User, uid, component);
    }
}
