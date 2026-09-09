using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Shared._Persistence14.Cargo;

[AdminCommand(AdminFlags.Admin | AdminFlags.Debug)]
public sealed class AddBountyCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLogManager = default!;

    public string Command => "bountyadd";
    public string Description => "Adds a specific CargoBountyPrototype, by ID, to a trade station.";
    public string Help => "bountyadd <stationUid> <bountyPrototypeId>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Help);
            return;
        }

        if (!NetEntity.TryParse(args[0], out var netEntity))
        {
            shell.WriteError($"Invalid Entity: {args[0]}");
            return;
        }

        var station = _entityManager.GetEntity(netEntity);
        if (!_entityManager.TryGetComponent<StationCargoBountyDatabaseComponent>(station, out var databaseComponent))
        {
            shell.WriteError($"Entity {station} has no bounty database.");
            return;
        }

        var cargo = _entityManager.System<CargoSystem>();

        if (!cargo.TryAddBounty(station, args[1]))
        {
            shell.WriteError($"Failed to add bounty '{args[1]}'");
            return;
        }

        cargo.UpdateBountyConsoles();
        shell.WriteLine($"Added bounty '{args[1]}' to {station}");

        var player = shell.Player;
        _adminLogManager.Add(Database.LogType.AdminCommands, Database.LogImpact.Medium, $"{(player == null ? "LOCALHOST" : player.Channel.UserName)} added bounty {args[1]} to station {station}.");
    }
}

[AdminCommand(AdminFlags.Admin | AdminFlags.Debug)]
public sealed class ClearBountiesCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLogManager = default!;

    public string Command => "bountyclear";
    public string Description => "Clears all bounties from a trade station.";
    public string Help => "bountyclear <stationUid>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        if (!NetEntity.TryParse(args[0], out var netEntity))
        {
            shell.WriteError($"Invalid entity: {args[0]}");
            return;
        }

        var station = _entityManager.GetEntity(netEntity);
        if (!_entityManager.TryGetComponent<StationCargoBountyDatabaseComponent>(station, out var databaseComponent))
        {
            shell.WriteError($"Entity {station} has no bounty database.");
            return;
        }

        databaseComponent.Bounties.Clear();
        _entityManager.System<CargoSystem>().UpdateBountyConsoles();

        shell.WriteLine($"Cleared bounties from station {station}");

        var player = shell.Player;
        _adminLogManager.Add(Database.LogType.AdminCommands, Database.LogImpact.Medium, $"{(player == null ? "LOCALHOST" : player.Channel.UserName)} cleared all bounties from station {station}");
    }
}