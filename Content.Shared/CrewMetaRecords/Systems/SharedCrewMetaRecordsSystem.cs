using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.CrewMetaRecords;

public abstract partial class SharedCrewMetaRecordsSystem : EntitySystem
{
    public CrewMetaRecordsComponent? MetaRecords;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrewMetaRecordsComponent, ComponentInit>(OnComponentInit);
    }

    private void OnComponentInit(EntityUid uid, CrewMetaRecordsComponent component, ComponentInit args)
    {
        MetaRecords = component;
    }

}

