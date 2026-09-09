using Content.Shared.CharacterInfo;
using Content.Shared.Objectives;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client.CharacterInfo;

public sealed class CharacterInfoSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _players = default!;

    public event Action<CharacterData>? OnCharacterUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CharacterInfoEvent>(OnCharacterInfoEvent);
    }

    public void RequestCharacterInfo()
    {
        var entity = _players.LocalEntity;
        if (entity == null)
        {
            return;
        }

        RaiseNetworkEvent(new RequestCharacterInfoEvent(GetNetEntity(entity.Value)));
    }

    public void UpdateDetailExaminable(string content)
    {
        RaiseNetworkEvent(new UpdateDetailExaminableEvent(content));
    }

    private void OnCharacterInfoEvent(CharacterInfoEvent msg, EntitySessionEventArgs args)
    {
        var entity = GetEntity(msg.NetEntity);
        var data = new CharacterData(entity, msg.JobTitle, msg.Faction, msg.BankBal, msg.Objectives, msg.Briefing, msg.DetailExaminable, Name(entity));

        OnCharacterUpdate?.Invoke(data);
    }

    public List<Control> GetCharacterInfoControls(EntityUid uid)
    {
        var ev = new GetCharacterInfoControlsEvent(uid);
        RaiseLocalEvent(uid, ref ev, true);
        return ev.Controls;
    }

    public readonly record struct CharacterData(
        EntityUid Entity,
        string Job,
        string? Faction,
        string BankBal,
        Dictionary<string, List<ObjectiveInfo>> Objectives,
        string? Briefing,
        string? DetailExaminable,
        string EntityName
    );

    /// <summary>
    /// Event raised to get additional controls to display in the character info menu.
    /// </summary>
    [ByRefEvent]
    public readonly record struct GetCharacterInfoControlsEvent(EntityUid Entity)
    {
        public readonly List<Control> Controls = new();

        public readonly EntityUid Entity = Entity;
    }
}
