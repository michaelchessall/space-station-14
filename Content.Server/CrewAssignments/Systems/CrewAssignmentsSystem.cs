using Content.Server.Cargo.Systems;
using Content.Server.DeviceLinking.Systems;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Server.Preferences.Managers;
using Content.Server.Radio.EntitySystems;
using Content.Server.Stack;
using Content.Server.Station.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Cargo;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.CrewAssignments.Components;
using Content.Shared.CrewAssignments.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Components;
using Content.Shared.Paper;
using Content.Shared.Preferences;
using Content.Shared.Station;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Serilog;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Xml.Linq;


namespace Content.Server.CrewAssignments.Systems;

public sealed partial class CrewAssignmentSystem : SharedCrewAssignmentSystem
{
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly AccessReaderSystem _accessReaderSystem = default!;
    [Dependency] private readonly DeviceLinkSystem _linker = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly PaperSystem _paperSystem = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly MetaDataSystem _metaSystem = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly StationSystem _station2 = default!;


    private ISawmill _log = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeConsole();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
    }

    


}
