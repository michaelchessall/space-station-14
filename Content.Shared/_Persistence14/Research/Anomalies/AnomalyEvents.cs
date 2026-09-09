using Content.Shared._Persistence14.RandomTable.State;
using Content.Shared.Anomaly.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Persistence14.Research.Anomalies;

[ByRefEvent]
public sealed partial class AnomalyGeneratorAttemptEvent : CancellableEntityEventArgs
{
    public required AnomalyGenerationContext Context;
}

[NetSerializable, Serializable]
public sealed partial class GenerateAnomalyEvent : BoundUserInterfaceMessage { }

public sealed partial class AnomalyGenerationContext
{
    public required EntityUid GeneratorUid;
    public required Entity<AnomalyCapsuleComponent> Capsule;
    public EntityCoordinates? TargetCoordinates = null;
    public bool ForceEnvironmental = false;
    public bool ForceInfectious = false;
}

[ByRefEvent]
public sealed partial class UpdateAnomalyGeneratorUIEvent : EntityEventArgs { }

[Serializable, NetSerializable]
public sealed class AnomalyGeneratorBUIState : BoundUserInterfaceState
{
    public required TimeSpan? GenerateEndTime;
    public bool IsGenerating => GenerateEndTime != null;
    public required TimeSpan GenerateDuration;
    public required TimeSpan? CooldownEndTime;
    public bool IsOnCooldown => CooldownEndTime != null;
    public required TimeSpan CooldownDuration;

    public required bool CanGenerateAnomaly;

    public required FixedPoint2 MaterialAmount;
    public required FixedPoint2 MaterialRequired;
    public required NetEntity? Capsule;
    public bool HasCapsule => Capsule != null;

    public required Dictionary<ProtoId<AnomalyPrototype>, float> AnomalyProbabilities;
    public required bool ForcedEnvironmental;
    public required bool ForcedInfectious;
}
