using Robust.Shared.Prototypes;

namespace Content.Server.NPC.Queries.Queries;

/// <summary>
/// Returns nearby entities that match all of the specified components.
/// </summary>
public sealed partial class ComponentQuery : UtilityQuery
{
    [DataField("components", required: true)]
    public ComponentRegistry Components = default!;
}

/// <summary>
/// Persistence: Returns nearby entities that match any of the specified components
/// </summary>
public sealed partial class ComponentQueryAny : UtilityQuery
{
    [DataField("components", required: true)]
    public ComponentRegistry Components = default!;
}
