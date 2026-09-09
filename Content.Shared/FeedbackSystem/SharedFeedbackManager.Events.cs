using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using System.Linq;

namespace Content.Shared.FeedbackSystem;

public abstract partial class SharedFeedbackManager : IEntityEventSubscriber
{
    [Dependency] private readonly IConfigurationManager _configManager = null!;

    private void InitSubscriptions()
    {
        _configManager.OnValueChanged(CCVars.FeedbackValidOrigins, OnFeedbackOriginsUpdated, true);
    }

    private void OnFeedbackOriginsUpdated(string newOrigins)
    {
        _validOrigins = newOrigins.Split(' ').ToList();
    }
}
