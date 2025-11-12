using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.CrewRecords.Systems;
using Robust.Shared.Player;
using Serilog;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Content.Shared.CrewRecords.Components;
using Content.Server.Station.Systems;

namespace Content.Server.CrewRecords.Systems;

public sealed partial class CrewRecordSystem : SharedCrewRecordSystem
{
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly StationSystem _station = default!;
    private ISawmill _log = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
    }




}
