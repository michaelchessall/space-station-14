using Content.Server.GameTicking.Rules.Components;
using Content.Server.StationEvents.Components;
using Content.Server.StationRecords;
using Content.Server.StationRecords.Systems;
using Content.Shared.StationRecords;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Random;

namespace Content.Server.StationEvents.Events;

public sealed class ClericalErrorRule : StationEventSystem<ClericalErrorRuleComponent>
{
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;

    protected override void Started(EntityUid uid, ClericalErrorRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var chosenStation))
            return;

        if (!TryComp<StationRecordsComponent>(chosenStation, out var stationRecords))
            return;

        // var recordCount = stationRecords.Records.Keys.Count;

        // if (recordCount == 0)
        //     return;
        // disabled for serialisation
        return;
    }
}
