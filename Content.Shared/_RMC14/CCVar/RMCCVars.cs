using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._RMC14.CCVar;

[CVarDefs]
public sealed partial class RMCCVars : CVars
{
    // Persistence: Chat stacking from RMC14 - pull/7587
    public static readonly CVarDef<int> RMCChatRepeatHistory =
        CVarDef.Create("rmc.chat_repeat_history", 4, CVar.REPLICATED | CVar.SERVER);
}
