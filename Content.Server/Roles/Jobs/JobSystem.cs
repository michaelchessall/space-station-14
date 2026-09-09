using Content.Server.Chat.Managers;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Player;

namespace Content.Server.Roles.Jobs;

/// <summary>
///     Handles the job data on mind entities.
/// </summary>
public sealed class JobSystem : SharedJobSystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly RoleSystem _roles = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAddedEvent);
        SubscribeLocalEvent<RoleRemovedEvent>(OnRoleRemovedEvent);
    }

    private void OnRoleAddedEvent(RoleAddedEvent args)
    {
        MindOnDoGreeting(args.MindId, args.Mind, args);

        if (args.RoleTypeUpdate)
            _roles.RoleUpdateMessage(args.Mind);
    }

    private void OnRoleRemovedEvent(RoleRemovedEvent args)
    {
        if (args.RoleTypeUpdate)
            _roles.RoleUpdateMessage(args.Mind);
    }

    private void MindOnDoGreeting(EntityUid mindId, MindComponent component, RoleAddedEvent args)
    {
        if (args.Silent)
            return;

        if (!_player.TryGetSessionById(component.UserId, out var session))
            return;

        if (!MindTryGetJob(mindId, out var prototype))
            return;

        _chat.DispatchServerMessage(session, "You have entered the Threshold as a Colonist. You have been given a bank account with $50 to get you started.");

        if (prototype.RequireAdminNotify)
            _chat.DispatchServerMessage(session, Loc.GetString("job-greet-important-disconnect-admin-notify"));

        _chat.DispatchServerMessage(session, "You are entitled to a paying job from Nanotrasen but you are also free to choose your own destiny. ");
    }

    public void MindAddJob(EntityUid mindId, string jobPrototypeId)
    {
        if (MindHasJobWithId(mindId, jobPrototypeId))
            return;

        _roles.MindAddJobRole(mindId, null, false, jobPrototypeId);
    }
}
