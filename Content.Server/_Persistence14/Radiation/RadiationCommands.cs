using Content.Server.Administration;
using Content.Shared.Radiation.Components;
using Robust.Shared.Console;

namespace Content.Server._Persistence14.Radiation;

[AdminCommand(Shared.Administration.AdminFlags.Admin | Shared.Administration.AdminFlags.Debug)]
public sealed partial class AddRadiationCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entityManager = default!;

    public string Command => "addrad";
    public string Description => "Adds radiation intensity to the target (even if they aren't radioactive already).";
    public string Help => "addrad <EntityUid> <Intensity>";

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

        if (!float.TryParse(args[1], out var intensity) || intensity < 0f)
        {
            shell.WriteError($"Invalid numeric value: {args[1]}");
            return;
        }

        var ent = _entityManager.GetEntity(netEntity);

        if (_entityManager.TryGetComponent<RadiationSourceComponent>(ent, out var source))
        {
            source.Intensity += intensity;
            return;
        }

        source = _entityManager.AddComponent<RadiationSourceComponent>(ent);
        source.Intensity = intensity;
        return;
    }
}

[AdminCommand(Shared.Administration.AdminFlags.Admin | Shared.Administration.AdminFlags.Debug)]
public sealed partial class ClearRadiationCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entityManager = default!;

    public string Command => "clearrad";
    public string Description => "Removes the radiation source component (and thus all radiation) from an entity.";
    public string Help => "clearrad <EntityUid>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        if (!NetEntity.TryParse(args[0], out var netEntity))
        {
            shell.WriteError($"Invalid Entity: {args[0]}");
            return;
        }

        var ent = _entityManager.GetEntity(netEntity);
        _entityManager.RemoveComponent<RadiationSourceComponent>(ent);
    }
}