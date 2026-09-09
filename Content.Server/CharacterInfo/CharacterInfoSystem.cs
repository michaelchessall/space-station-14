using Content.Server._NF.Bank;
using Content.Server.CrewAssignments.Systems;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;
using Content.Shared.CCVar;
using Content.Shared.CharacterInfo;
using Content.Shared.DetailExaminable;
using Content.Shared.Objectives;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Server.CharacterInfo;

public sealed class CharacterInfoSystem : EntitySystem
{
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly MindSystem _minds = default!;
    [Dependency] private readonly RoleSystem _roles = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly JobNetSystem _jobNet = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RequestCharacterInfoEvent>(OnRequestCharacterInfoEvent);
        SubscribeNetworkEvent<UpdateDetailExaminableEvent>(OnUpdateDetailExaminableEvent);
    }

    private void OnRequestCharacterInfoEvent(RequestCharacterInfoEvent msg, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue
            || args.SenderSession.AttachedEntity != GetEntity(msg.NetEntity))
            return;

        var entity = args.SenderSession.AttachedEntity.Value;

        var objectives = new Dictionary<string, List<ObjectiveInfo>>();

        var (jobTitle, faction) = _jobNet.GetJobNetStrings(entity); // Persistence: Job & faction names from implant
        if (jobTitle == null)
            jobTitle = Loc.GetString("character-info-off-duty");

        _bank.TryGetBalance(entity, out var bankBal);

        string? briefing = null;
        if (_minds.TryGetMind(entity, out var mindId, out var mind))
        {
            // Get objectives
            foreach (var objective in mind.Objectives)
            {
                var info = _objectives.GetInfo(objective, mindId, mind);
                if (info == null)
                    continue;

                // group objectives by their issuer
                var issuer = Comp<ObjectiveComponent>(objective).LocIssuer;
                if (!objectives.ContainsKey(issuer))
                    objectives[issuer] = new List<ObjectiveInfo>();
                objectives[issuer].Add(info.Value);
            }

            if (_jobs.MindTryGetJobName(mindId, out var jobName))
                jobTitle = jobName;

            // Get briefing
            briefing = _roles.MindGetBriefing(mindId);
        }

        var detailExaminable = EnsureComp<DetailExaminableComponent>(entity, out var detail) ? detail.Content : Loc.GetString("flavor-text-placeholder");

        RaiseNetworkEvent(new CharacterInfoEvent(
            GetNetEntity(entity),
            jobTitle,
            faction,
            "$" + bankBal.ToString(),
            objectives,
            briefing,
            detailExaminable),
            args.SenderSession
        );

        Dirty(entity, detail);
    }

    private void OnUpdateDetailExaminableEvent(UpdateDetailExaminableEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } entity)
            return;

        string newContent = "";
        var maxFlavorTextLength = _cfg.GetCVar(CCVars.MaxFlavorTextLength);
        if (msg.Content.Length > maxFlavorTextLength)
        {
            newContent = FormattedMessage.RemoveMarkupOrThrow(msg.Content)[..maxFlavorTextLength];
        }
        else
        {
            newContent = FormattedMessage.RemoveMarkupOrThrow(msg.Content);
        }

        var detail = EnsureComp<DetailExaminableComponent>(entity);
        detail.Content = newContent;
        Dirty(entity, detail);
    }

    //     var maxFlavorTextLength = configManager.GetCVar(CCVars.MaxFlavorTextLength);
    //         if (FlavorText.Length > maxFlavorTextLength)
    //     {
    //         flavortext = FormattedMessage.RemoveMarkupOrThrow(FlavorText)[..maxFlavorTextLength];
    //     }
    // else
    // {
    //     flavortext = FormattedMessage.RemoveMarkupOrThrow(FlavorText);
    // }
}
