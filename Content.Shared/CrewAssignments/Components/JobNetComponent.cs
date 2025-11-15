using Content.Shared._NF.Bank.Events;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CrewAssignments.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class JobNetComponent : Component
{
    [DataField]
    public SoundSpecifier PaySuccessSound = new SoundPathSpecifier("/Audio/Effects/kaching.ogg");
    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");
    [DataField]
    public int? WorkingFor;
    [DataField]
    public TimeSpan WorkedTime = TimeSpan.Zero;
    [DataField]
    public int LastWorkedFor = 0;
}
