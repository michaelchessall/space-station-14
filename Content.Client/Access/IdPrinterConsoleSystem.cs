using Content.Shared.Access.Systems;
using JetBrains.Annotations;

namespace Content.Client.Access
{
    [UsedImplicitly]
    public sealed class IdPrinterConsoleSystem : SharedIdPrinterConsoleSystem
    {
        // one day, maybe bound user interfaces can be shared too.
        // then this doesn't have to be like this.
        // I hate this.                         -the lonely commenter
    }
}
