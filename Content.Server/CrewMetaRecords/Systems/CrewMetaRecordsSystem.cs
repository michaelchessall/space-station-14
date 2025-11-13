using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Systems;
using Content.Shared.CrewMetaRecords;
using Content.Shared.CrewRecords.Components;
using Content.Shared.CrewRecords.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Serilog;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace Content.Server.CrewRecords.Systems;

public sealed partial class CrewMetaRecordsSystem : SharedCrewMetaRecordsSystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly StationSystem _station = default!;
    private ISawmill _log = default!;

    public bool CharacterNameExists(string name)
    {
        if (_gameTicker.RunLevel != GameRunLevel.InRound) return false;
        return MetaRecords != null && MetaRecords.CrewMetaRecords.ContainsKey(name);
    }

    public void JoinFirstTime(ICommonSession session)
    {
        _gameTicker!.MakeJoinGamePersistent(session);
    }


}
