namespace Content.Shared._Persistence14.Research.Anomalies;

public abstract partial class AnomalyCapsuleModuleSystem<TComp> : EntitySystem where TComp : Component
{
    public override void Initialize()
    {
        SubscribeLocalEvent<TComp, AnomalyGeneratorAttemptEvent>(OnGenerationAttempt);
    }

    protected abstract void OnGenerationAttempt(Entity<TComp> module, ref AnomalyGeneratorAttemptEvent args);
}