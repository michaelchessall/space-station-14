using Content.Server.Chat.Systems;
using Content.Shared.Administration;
using Content.Shared.Chat;
using Robust.Shared.Console;
using Robust.Shared.Enums;

namespace Content.Server.Chat.Commands
{
    [AnyCommand]
    internal sealed class SayCommand : LocalizedEntityCommands
    {
        [Dependency] private readonly ChatSystem _chatSystem = default!;
        public override string Command => "say";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (shell.Player is not { } player)
            {
                shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
                return;
            }

            if (player.Status != SessionStatus.InGame)
                return;

            if (player.AttachedEntity is not { } playerEntity)
            {
                shell.WriteError(Loc.GetString($"shell-must-be-attached-to-entity"));
                return;
            }

            if (args.Length < 1)
                return;

            var message = string.Join(" ", args).Trim();
            if (string.IsNullOrEmpty(message))
                return;

            // Lets any system override the transmit range for this specific speaker (e.g. to
            // suppress the visible bubble for something whose speech is meant to be
            // heard/relayed some other way instead) without this file needing to know why.
            var rangeEv = new GetSpeechTransmitRangeEvent(ChatTransmitRange.Normal);
            EntityManager.EventBus.RaiseLocalEvent(playerEntity, rangeEv);

            _chatSystem.TrySendInGameICMessage(playerEntity, message, InGameICChatType.Speak, rangeEv.Range, false, shell, player);
        }
    }
}
