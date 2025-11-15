using Content.Shared.Access.Components;
using Content.Shared.Containers.ItemSlots;
using JetBrains.Annotations;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;

namespace Content.Shared.Access.Systems
{
    [UsedImplicitly]
    public abstract class SharedIdPrinterConsoleSystem : EntitySystem
    {
        [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
        [Dependency] private readonly ILogManager _log = default!;

        public const string Sawmill = "idconsole";
        protected ISawmill _sawmill = default!;

        public override void Initialize()
        {
            base.Initialize();
            _sawmill = _log.GetSawmill(Sawmill);
        }

        [Serializable, NetSerializable]
        private sealed class IdPrinterConsoleComponentState : ComponentState
        {

            public IdPrinterConsoleComponentState()
            {
            }
        }
    }
}
