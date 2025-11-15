using Content.Server.Chat.Systems;
using Content.Server.Containers;
using Content.Server.CrewRecords.Systems;
using Content.Server.Hands.Systems;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Chat;
using Content.Shared.Construction;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Coordinates;
using Content.Shared.CrewAssignments.Components;
using Content.Shared.CrewMetaRecords;
using Content.Shared.CrewRecords.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Hands.Components;
using Content.Shared.Roles;
using Content.Shared.Station.Components;
using Content.Shared.StationRecords;
using Content.Shared.Throwing;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Toolshed.TypeParsers;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static Content.Shared.Access.Components.IdCardConsoleComponent;
using static Content.Shared.Access.Components.IdPrinterConsoleComponent;

namespace Content.Server.Access.Systems;

[UsedImplicitly]
public sealed class IdPrinterConsoleSystem : SharedIdPrinterConsoleSystem
{
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly AccessSystem _access = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly CrewMetaRecordsSystem _crewMeta = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly HandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IdPrinterConsoleComponent, ComponentStartup>(UpdateUserInterface);
        SubscribeLocalEvent<IdPrinterConsoleComponent, PrintID>(Print);
    }

    private void Print(EntityUid uid, IdPrinterConsoleComponent component, PrintID args)
    {
        if(args.Actor is not { Valid: true } player)
            return;
        var name = Name(player);
        if(_crewMeta.MetaRecords != null && _crewMeta.MetaRecords.CrewMetaRecords.ContainsKey(name))
        {
            _crewMeta.DevalidateID(name);
        }
        var iD = _entityManager.SpawnAtPosition("PassengerIDCard", player.ToCoordinates());

        if (!_hands.TryPickupAnyHand(player, iD))
           _transform.SetLocalRotation(iD, Angle.Zero); // Orient these to grid north instead of map north
        _idCard.BuildID(iD, name);

    }
    private void UpdateUserInterface(EntityUid uid, IdPrinterConsoleComponent component, EntityEventArgs args)
    {
        IdPrinterConsoleBoundUserInterfaceState newState = new();
        _userInterface.SetUiState(uid, IdPrinterConsoleUiKey.Key, newState);
    }

}
