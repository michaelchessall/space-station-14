using Content.Server.Administration;
using Content.Server.Database;
using Content.Server.Preferences.Managers;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Administration;
using Content.Shared.Preferences;
using Robust.Server.Player;
using Robust.Shared.Console;
using Serilog;
using System.Linq;

namespace Content.Server._NF.Bank.Commands;

/// <summary>
/// Command that allows administrators to check a player's bank balance using their username.
/// Ported from Monolith.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class CheckBankBalance : IConsoleCommand
{
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] protected readonly EntityManager EntityManager = default!;
    public string Command => "checkbalance";
    public string Description => "Check a characters's bank balance by character name.";
    public string Help => "checkbalance <charactername>";

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine("Usage: checkbalance <username>");
            return;
        }

        var username = args[0];

        Type type = typeof(MoneyAccountsComponent);
        var components = EntityManager.GetAllComponents(type, true);
        if (components.Count() < 1)
        {
            shell.WriteLine($" No MoneyAccountsComponents found.");
            return;

        }
        var accName = username;
        var (uid, component) = components.First();
        MoneyAccountsComponent? accounts = (MoneyAccountsComponent?)component;
        if (!accounts!.TryGetAccount(accName, out var account))
        {
            shell.WriteLine($"Balance: {account!.Balance}");
            return;
        }
        else
        {
            shell.WriteLine($"No account found for {accName}");
        }
    }
}
