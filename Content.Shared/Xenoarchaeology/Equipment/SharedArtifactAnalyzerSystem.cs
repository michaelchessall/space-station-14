using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Placeable;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Content.Shared.Xenoarchaeology.Equipment.Components;
using System.Diagnostics.CodeAnalysis;

namespace Content.Shared.Xenoarchaeology.Equipment;

/// <summary>
/// This system is used for managing the artifact analyzer as well as the analysis console.
/// It also handles scanning and ui updates for both systems.
/// </summary>
public abstract class SharedArtifactAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] private readonly SharedDeviceLinkSystem _deviceLink = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArtifactAnalyzerComponent, ItemPlacedEvent>(OnItemPlaced);
        SubscribeLocalEvent<ArtifactAnalyzerComponent, ItemRemovedEvent>(OnItemRemoved);
        SubscribeLocalEvent<ArtifactAnalyzerComponent, NewLinkEvent>(OnNewLinkAnalyzer);
        SubscribeLocalEvent<ArtifactAnalyzerComponent, LinkAttemptEvent>(OnLinkAttemptAnalyzer);
        SubscribeLocalEvent<ArtifactAnalyzerComponent, PortDisconnectedEvent>(OnPortDisconnectedAnalyzer);

        SubscribeLocalEvent<AnalysisConsoleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AnalysisConsoleComponent, NewLinkEvent>(OnNewLinkConsole);
        SubscribeLocalEvent<AnalysisConsoleComponent, LinkAttemptEvent>(OnLinkAttemptConsole);
        SubscribeLocalEvent<AnalysisConsoleComponent, PortDisconnectedEvent>(OnPortDisconnectedConsole);
    }

    private void OnItemPlaced(Entity<ArtifactAnalyzerComponent> ent, ref ItemPlacedEvent args)
    {
        if (!HasComp<XenoArtifactComponent>(args.OtherEntity))
            return;

        if (!ent.Comp.Artifacts.Contains(args.OtherEntity))
            ent.Comp.Artifacts.Add(args.OtherEntity);

        // Newly placed artifact becomes the selected/displayed one.
        ent.Comp.CurrentArtifact = args.OtherEntity;
        Dirty(ent);
    }

    private void OnItemRemoved(Entity<ArtifactAnalyzerComponent> ent, ref ItemRemovedEvent args)
    {
        if (!ent.Comp.Artifacts.Remove(args.OtherEntity) && args.OtherEntity != ent.Comp.CurrentArtifact)
            return;

        // If the displayed artifact was the one removed, fall back to another placed artifact (if any).
        if (args.OtherEntity == ent.Comp.CurrentArtifact)
            ent.Comp.CurrentArtifact = ent.Comp.Artifacts.Count > 0 ? ent.Comp.Artifacts[0] : null;

        Dirty(ent);
    }

    /// <summary>
    /// Cycles the currently displayed artifact on an advanced analyzer to the next/previous
    /// placed artifact, wrapping around at the ends.
    /// </summary>
    public void CycleArtifact(Entity<ArtifactAnalyzerComponent> ent, bool forward)
    {
        var count = ent.Comp.Artifacts.Count;
        if (count <= 1)
            return;

        var index = ent.Comp.CurrentArtifact is { } current
            ? ent.Comp.Artifacts.IndexOf(current)
            : 0;
        if (index < 0)
            index = 0;

        index = (index + (forward ? 1 : -1) + count) % count;
        ent.Comp.CurrentArtifact = ent.Comp.Artifacts[index];
        Dirty(ent);
    }

    private void OnMapInit(Entity<AnalysisConsoleComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<DeviceLinkSourceComponent>(ent, out var source))
            return;

        var linkedEntities = _deviceLink.GetLinkedSinks((ent.Owner, source), ent.Comp.LinkingPort);

        foreach (var sink in linkedEntities)
        {
            if (!TryComp<ArtifactAnalyzerComponent>(sink, out var analyzer))
                continue;

            ent.Comp.AnalyzerEntity = sink;
            analyzer.Console = ent.Owner;
            Dirty(ent);
            Dirty(sink, analyzer);
            break;
        }
    }

    private void OnNewLinkConsole(Entity<AnalysisConsoleComponent> ent, ref NewLinkEvent args)
    {
        if (args.SourcePort != ent.Comp.LinkingPort || !HasComp<ArtifactAnalyzerComponent>(args.Sink))
            return;

        ent.Comp.AnalyzerEntity = args.Sink;
        Dirty(ent);
    }

    private void OnNewLinkAnalyzer(Entity<ArtifactAnalyzerComponent> ent, ref NewLinkEvent args)
    {
        if (args.SinkPort != ent.Comp.LinkingPort || !HasComp<AnalysisConsoleComponent>(args.Source))
            return;

        ent.Comp.Console = args.Source;
        Dirty(ent);
    }

    private void OnLinkAttemptConsole(Entity<AnalysisConsoleComponent> ent, ref LinkAttemptEvent args)
    {
        if (ent.Comp.AnalyzerEntity != null)
            args.Cancel(); // can only link to one device at a time
    }

    private void OnLinkAttemptAnalyzer(Entity<ArtifactAnalyzerComponent> ent, ref LinkAttemptEvent args)
    {
        if (ent.Comp.Console != null)
            args.Cancel(); // can only link to one device at a time
    }

    private void OnPortDisconnectedConsole(Entity<AnalysisConsoleComponent> ent, ref PortDisconnectedEvent args)
    {
        if (args.Port != ent.Comp.LinkingPort || ent.Comp.AnalyzerEntity == null)
            return;

        ent.Comp.AnalyzerEntity = null;
        Dirty(ent);
    }

    private void OnPortDisconnectedAnalyzer(Entity<ArtifactAnalyzerComponent> ent, ref PortDisconnectedEvent args)
    {
        if (args.Port != ent.Comp.LinkingPort || ent.Comp.Console == null)
            return;

        ent.Comp.Console = null;
        Dirty(ent);
    }

    public bool TryGetAnalyzer(Entity<AnalysisConsoleComponent> ent, [NotNullWhen(true)] out Entity<ArtifactAnalyzerComponent>? analyzer)
    {
        analyzer = null;

        var consoleEnt = ent.Owner;
        if (!_powerReceiver.IsPowered(consoleEnt))
            return false;

        if (!TryComp<ArtifactAnalyzerComponent>(ent.Comp.AnalyzerEntity, out var analyzerComp))
            return false;

        if (!_powerReceiver.IsPowered(ent.Comp.AnalyzerEntity.Value))
            return false;

        analyzer = (ent.Comp.AnalyzerEntity.Value, analyzerComp);
        return true;
    }

    public bool TryGetArtifactFromConsole(Entity<AnalysisConsoleComponent> ent, [NotNullWhen(true)] out Entity<XenoArtifactComponent>? artifact)
    {
        artifact = null;

        if (!TryGetAnalyzer(ent, out var analyzer))
            return false;

        if (!TryComp<XenoArtifactComponent>(analyzer.Value.Comp.CurrentArtifact, out var comp))
            return false;

        artifact = (analyzer.Value.Comp.CurrentArtifact.Value, comp);
        return true;
    }

    /// <summary>
    /// Gets every artifact currently placed on the analyzer linked to this console.
    /// For a regular analyzer this is the single placed artifact; for an advanced one it is all of them.
    /// </summary>
    public bool TryGetArtifactsFromConsole(Entity<AnalysisConsoleComponent> ent, out List<Entity<XenoArtifactComponent>> artifacts)
    {
        artifacts = new List<Entity<XenoArtifactComponent>>();

        if (!TryGetAnalyzer(ent, out var analyzer))
            return false;

        // Advanced analyzers extract from all placed artifacts; regular ones only the selected one.
        if (analyzer.Value.Comp.Advanced)
        {
            foreach (var uid in analyzer.Value.Comp.Artifacts)
            {
                if (TryComp<XenoArtifactComponent>(uid, out var comp))
                    artifacts.Add((uid, comp));
            }
        }
        else if (TryComp<XenoArtifactComponent>(analyzer.Value.Comp.CurrentArtifact, out var comp))
        {
            artifacts.Add((analyzer.Value.Comp.CurrentArtifact.Value, comp));
        }

        return artifacts.Count > 0;
    }

    /// <summary>
    /// Gets the 1-based index of the currently displayed artifact and the total artifact count,
    /// for the console's cycling UI. Returns false if there is no analyzer or no artifacts.
    /// </summary>
    public bool TryGetArtifactSelection(Entity<AnalysisConsoleComponent> ent, out int index, out int count, out bool advanced)
    {
        index = 0;
        count = 0;
        advanced = false;

        if (!TryGetAnalyzer(ent, out var analyzer))
            return false;

        advanced = analyzer.Value.Comp.Advanced;
        count = analyzer.Value.Comp.Artifacts.Count;
        if (count == 0)
            return false;

        var current = analyzer.Value.Comp.CurrentArtifact;
        var zeroBased = current is { } c ? analyzer.Value.Comp.Artifacts.IndexOf(c) : 0;
        index = (zeroBased < 0 ? 0 : zeroBased) + 1;
        return true;
    }

    public bool TryGetAnalysisConsole(Entity<ArtifactAnalyzerComponent> ent, [NotNullWhen(true)] out Entity<AnalysisConsoleComponent>? analysisConsole)
    {
        analysisConsole = null;

        if (!TryComp<AnalysisConsoleComponent>(ent.Comp.Console, out var consoleComp))
            return false;

        analysisConsole = (ent.Comp.Console.Value, consoleComp);
        return true;
    }
}
