using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.Events;

/// <summary>
///     Set order in database as approved.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationModificationAddOwner : BoundUserInterfaceMessage
{
    public string Owner;

    public StationModificationAddOwner(string owner)
    {
        Owner = owner;
    }
}
