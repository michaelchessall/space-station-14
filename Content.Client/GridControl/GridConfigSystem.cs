using Content.Shared.GridControl.Systems;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Content.Shared.GridControl.Components;

namespace Content.Client.GridControl
{
    [UsedImplicitly]
    public sealed class GridConfigSystem : SharedGridConfigSystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<GridConfigComponent, EntInsertedIntoContainerMessage>(OnIdInserted);
            SubscribeLocalEvent<GridConfigComponent, EntRemovedFromContainerMessage>(OnIdRemoved);
        }

        private void OnIdInserted(EntityUid uid, GridConfigComponent component, EntInsertedIntoContainerMessage args)
        {
            UpdateIDAppearance(uid, true);
        }

        private void OnIdRemoved(EntityUid uid, GridConfigComponent component, EntRemovedFromContainerMessage args)
        {
            UpdateIDAppearance(uid, false);
        }

    }
}
