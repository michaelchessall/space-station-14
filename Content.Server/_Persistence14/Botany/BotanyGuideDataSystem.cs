using Content.Shared._Persistence14.Botany;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Botany;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Persistence14.Botany;


public sealed class BotanyGuideDataSystem : SharedBotanyGuideDataSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(PrototypeManagerReload);
        _player.PlayerStatusChanged += OnPlayerStatusChanged;

        InitializeServerRegistry();
    }

    private void InitializeServerRegistry()
    {
        var changeset = new PlantNutrientGuideChangeset(new Dictionary<string, PlantNutrientGuideEntry>(), new HashSet<string>());
        foreach (var proto in PrototypeManager.EnumeratePrototypes<PlantNutrientPrototype>())
        {
            var entry = new PlantNutrientGuideEntry(proto, PrototypeManager, EntityManager.EntitySysManager);
            changeset.GuideEntries.Add(proto.ID, entry);
            Registry[proto.ID] = entry;
        }

        var ev = new PlantNutrientGuideRegistryChangedEvent(changeset);
        RaiseNetworkEvent(ev);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.Connected)
            return;

        var sendEv = new PlantNutrientGuideRegistryChangedEvent(new PlantNutrientGuideChangeset(Registry, new HashSet<string>()));
        RaiseNetworkEvent(sendEv, e.Session);
    }

    private void PrototypeManagerReload(PrototypesReloadedEventArgs obj)
    {
        if (!obj.ByType.TryGetValue(typeof(PlantNutrientPrototype), out var PlantNutrients))
            return;

        var changeset = new PlantNutrientGuideChangeset(new Dictionary<string, PlantNutrientGuideEntry>(), new HashSet<string>());

        foreach (var (id, proto) in PlantNutrients.Modified)
        {
            var plantNutrientProto = (PlantNutrientPrototype)proto;
            var entry = new PlantNutrientGuideEntry(plantNutrientProto, PrototypeManager, EntityManager.EntitySysManager);
            changeset.GuideEntries.Add(id, entry);
            Registry[id] = entry;
        }

        var ev = new PlantNutrientGuideRegistryChangedEvent(changeset);
        RaiseNetworkEvent(ev);
    }

    public override void ReloadAllPlantNutrientPrototypes()
    {
        InitializeServerRegistry();
    }
}
