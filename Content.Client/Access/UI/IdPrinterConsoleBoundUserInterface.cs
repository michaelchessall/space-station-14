using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CCVar;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.CrewManifest;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using static Content.Shared.Access.Components.IdCardConsoleComponent;
using static Content.Shared.Access.Components.IdPrinterConsoleComponent;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.Access.UI
{
    public sealed class IdPrinterConsoleBoundUserInterface : BoundUserInterface
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IConfigurationManager _cfgManager = default!;
        private readonly SharedIdPrinterConsoleSystem _idPrinterConsoleSystem = default!;

        private IdPrinterConsoleWindow? _window;

        // CCVar.
        private int _maxNameLength;
        private int _maxIdJobLength;

        public IdPrinterConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
            _idPrinterConsoleSystem = EntMan.System<SharedIdPrinterConsoleSystem>();

        }

        protected override void Open()
        {
            base.Open();

         

            _window = new IdPrinterConsoleWindow(this, _prototypeManager)
            {
                Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName
            };


            _window.OnClose += Close;
            _window.OpenCentered();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
                return;

            _window?.Dispose();
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);
            var castState = (IdPrinterConsoleBoundUserInterfaceState) state;
            _window?.UpdateState(castState);
        }

        public void Print()
        {
            SendMessage(new PrintID());

        }

    }
}
