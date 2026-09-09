using Content.Server._NF.Bank;
using Content.Server.Access.Systems;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Codewords;
using Content.Server.CrewManifest;
using Content.Server.CrewRecords.Systems;
using Content.Server.Database;
using Content.Server.DoAfter;
using Content.Server.Interaction;
using Content.Server.NameIdentifier;
using Content.Shared.Access.Components;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.CrewAssignments;
using Content.Shared.CrewAssignments.Components;
using Content.Shared.CrewAssignments.Prototypes;
using Content.Shared.CrewAssignments.Systems;
using Content.Shared.CrewRecords.Components;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Precursor;
using Content.Shared.Station.Components;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;
using static Content.Shared.Access.Components.AccessOverriderComponent;
using static Content.Shared.Access.Systems.SharedAccessOverriderSystem;

namespace Content.Server.CrewAssignments.Systems;

/// <summary>
/// Manages general interactions with a store and different entities,
/// getting listings for stores, and interfacing with the store UI.
/// </summary>
public sealed partial class JobNetSystem : SharedJobNetSystem
{
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly CrewManifestSystem _crewManifest = default!;
    [Dependency] private readonly IdCardSystem _card = default!;
    [Dependency] private readonly CodewordSystem _codeword = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private readonly SharedCuffableSystem _cuffable = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly NameIdentifierSystem _nameIdentifier = default!;
    [Dependency] private readonly IGameTiming _timing2 = default!;
    public override void ReagentObjectiveComplete(JobNetComponent component, ProtoId<PrecursorObjectivePrototype> objective)
    {
        if (_proto.TryIndex(objective, out PrecursorObjectivePrototype? proto) && proto != null)
        {
            component.PrecursorObjectives.Remove(objective);
            AwardPrecursor(component.Owner, component, proto.Reward);
            EntityUid? player = null;
            var comp = Transform(component.Owner);
            player = comp.ParentUid;
            if (player == null) return;
            if (TryComp<ActorComponent>(player, out var actor) && actor != null && actor.PlayerSession != null)
            {
                var msg = $"Precusor Objective Complete! {proto.Reward} precursor gained.";
                _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Notifications,
                    msg,
                    msg,
                    player.Value,
                    false,
                    actor.PlayerSession.Channel
                    );
            }
        }
    }
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JobNetComponent, ActivatableUIOpenAttemptEvent>(OnJobNetOpenAttempt);
        SubscribeLocalEvent<JobNetComponent, BeforeActivatableUIOpenEvent>(BeforeActivatableUiOpen);

        SubscribeLocalEvent<JobNetComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<JobNetComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<JobNetComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<JobNetComponent, ImplantImplantedEvent>(OnJobNetImplantImplanted);
        SubscribeLocalEvent<JobNetComponent, OpenJobNetImplantEvent>(OnImplantActivate);
        SubscribeLocalEvent<JobNetComponent, JobNetSelectMessage>(OnSelect);
        SubscribeLocalEvent<JobNetComponent, JobNetPurchaseMessage>(OnPurchase);
        SubscribeLocalEvent<JobNetComponent, JobNetSelectRogueNetMessage>(OnSelectRogueNet);
        SubscribeLocalEvent<JobNetComponent, JobNetPurchasePrecursorMessage>(OnPurchasePrecursor);
        SubscribeLocalEvent<JobNetComponent, JobNetSubmitHuntMessage>(OnSubmitHunt);
        SubscribeLocalEvent<JobNetComponent, JobNetSubmitHuntedMessage>(OnSubmitHunted);
        SubscribeLocalEvent<JobNetComponent, JobNetDealerLabelMessage>(OnDealerLabel);
        SubscribeLocalEvent<PrecursorExtractorComponent, AfterInteractEvent>(AfterInteractOn);
        SubscribeLocalEvent<PrecursorExtractorComponent, PrecursorExtractorDoAfterEvent>(OnDoAfter);
        

        InitializeUi();
    }

    public void CompleteDealerBounty(EntityUid uid, JobNetComponent component)
    {
        if (component.DealerBounty == null) return;
        var ts = _cargo.GetTradeStationByID(component.DealerBounty.TradeStationUID);
        if (ts == null) return;
        AwardPrecursor(uid, component, 500);
        EntityUid? player = null;
        var comp = Transform(uid);
        player = comp.ParentUid;
        if (player == null) return;
        if (TryComp<ActorComponent>(player, out var actor) && actor != null && actor.PlayerSession != null)
        {
            var msg = $"You have completed the dealer bounty for {Name(ts.Value)}. 500 precursor gained.";
            _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Notifications,
                msg,
                msg,
                player.Value,
                false,
                actor.PlayerSession.Channel
                );
        }
        component.DealerBounty = null;
        UpdateUserInterface(player, uid, component);
    }
    public JobNetComponent? GetJobNetByName(string name)
    {
        var query = EntityQueryEnumerator<JobNetComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var tranform = Transform(uid);
            var parent = tranform.ParentUid;
            if (Name(parent) == name)
            {
                return comp;
            }
        }
        return null;
    }

    private void OnDealerLabel(Entity<JobNetComponent> ent, ref JobNetDealerLabelMessage args)
    {
        if (ent.Comp.DealerBounty == null) return;
        if (ent.Comp.NextPrintTime > _timing2.CurTime) return;
        EntityUid? player = null;
        if (TryComp<TransformComponent>(ent.Owner, out var comp) && comp != null)
        {
            player = comp.ParentUid;
        }
        if (!TryComp<ActorComponent>(player, out var actor) || actor == null || actor.PlayerSession == null) return;
        var query = EntityQueryEnumerator<CargoTelepadComponent>();
        var found = false;
        var userMapPos = _transform.GetMapCoordinates(player.Value);
        var ts = _cargo.GetTradeStationByID(ent.Comp.DealerBounty.TradeStationUID);
        if (ts == null) return;
        while (query.MoveNext(out var telepad, out var telepadcomp))
        {
            var targetMapPos = _transform.GetMapCoordinates(telepad);
            var calculatedDistance = targetMapPos.Position - userMapPos.Position;
            var total = calculatedDistance.Length();
            if (total <= 3)
            {
                var teleTransform = Transform(telepad);
                var newEntity = Spawn("PaperCargoBountyManifest", teleTransform.Coordinates);
                ent.Comp.NextPrintTime = _timing2.CurTime + TimeSpan.FromSeconds(10);
                _cargo.SetupBountyLabel(newEntity, ts.Value, ent.Comp.DealerBounty);
                if(TryComp<CargoBountyLabelComponent>(newEntity, out var cbl))
                {
                    if(cbl != null)
                    {
                        cbl.DealerName = Name(player.Value);
                    }
                }
                found = true;

                break;
            }
        }
        if (!found)
        {
            _audio.PlayEntity(ent.Comp.ErrorSound, player.Value, player.Value);
            var msg = $"You must be next to a telepad to teleport the label.";
            if (msg != null)
                _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Notifications,
                    msg,
                    msg,
                    player.Value,
                    false,
                    actor.PlayerSession.Channel
                    );
            return;
        }
        UpdateUserInterface(args.Actor, ent.Owner, ent.Comp);
    }

    private void AwardPrecursor(EntityUid uid, JobNetComponent component, int amount)
    {
        component.Precursor += amount;
        component.XP += amount;
        EntityUid? player = null;
        var comp = Transform(uid);
        player = comp.ParentUid;
        if (player == null) return;
        _audio.PlayEntity(component.PaySuccessSound, player.Value, player.Value);
        var meta = _meta.GetMetaRecordsComponent();
        if (meta != null)
            meta.SectorChaos += amount;
        if (TryComp<ActorComponent>(player, out var actor) && actor != null && actor.PlayerSession != null)
        {
            var msg = $"You have been awarded {amount} precursor.";
            _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Notifications,
                msg,
                msg,
                player.Value,
                false,
                actor.PlayerSession.Channel
                );
        }
        if (_proto.TryIndex(component.RogueLevel, out var rogueLevel))
        {
            if (rogueLevel.Next != null && _proto.TryIndex(rogueLevel.Next, out var nextLevel))
            {
                if (component.XP >= nextLevel.Cost)
                {
                    component.RogueLevel = nextLevel.ID;
                }
            }
        }
    }
    private void OnSubmitHunted(Entity<JobNetComponent> ent, ref JobNetSubmitHuntedMessage args)
    {
        if (args.ID == "") return;
        var query = EntityQueryEnumerator<JobNetComponent>();
        var player = Transform(ent).ParentUid;
        if (player == EntityUid.Invalid) return;
        if (!TryComp<ActorComponent>(player, out var actor) || actor.PlayerSession == null) return;
        while (query.MoveNext(out var uid, out var comp))
        {
            if(comp.KillTarget == Name(player))
            {
                if(args.ID == comp.SecretPhrase)
                {
                    Compromise(uid, comp);
                    HuntedCompleted(ent.Owner, ent.Comp);
                    return;
                }
            }
        }
    }

    private void OnSubmitHunt(Entity<JobNetComponent> ent, ref JobNetSubmitHuntMessage args)
    {
        if (args.ID == "") return;
        if (ent.Comp.KillTarget == null) return;
        var query = EntityQueryEnumerator<JobNetComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var tx = Transform(uid);
            var parent = tx.ParentUid;
            if (Name(parent) == ent.Comp.KillTarget)
            {
                if (args.ID == comp.SecretPhrase)
                {
                    HuntCompleted(ent.Owner, ent.Comp);
                    Compromise(uid, comp);
                    return;
                }
            }
        }
    }

    private void HuntCompleted(EntityUid uid, JobNetComponent component)
    {
        EntityUid? player = null;
        var comp = Transform(uid);
        player = comp.ParentUid;
        if (player == null) return;
        if (TryComp<ActorComponent>(player, out var actor) && actor != null && actor.PlayerSession != null)
        {
            var msg = $"You have completed the hunt.";
            _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Notifications,
                msg,
                msg,
                player.Value,
                false,
                actor.PlayerSession.Channel
                );
        }
        component.KillTarget = null;
        AwardPrecursor(uid, component, 500);
        UpdateUserInterface(player, uid, component);
    }
    private void HuntedCompleted(EntityUid uid, JobNetComponent component)
    {
        EntityUid? player = null;
        var comp = Transform(uid);
        player = comp.ParentUid;
        if (player == null) return;
        if (TryComp<ActorComponent>(player, out var actor) && actor != null && actor.PlayerSession != null)
        {
            var msg = $"You have compromised the person hunting you.";
            _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Notifications,
                msg,
                msg,
                player.Value,
                false,
                actor.PlayerSession.Channel
                );
        }
        AwardPrecursor(uid, component, 500);
        UpdateUserInterface(player, uid, component);
    }

    private void Compromise(EntityUid uid, JobNetComponent component)
    {
        EntityUid? player = null;
        var comp = Transform(uid);
        player = comp.ParentUid;
        if (player != null)
        {
            if(TryComp<ActorComponent>(player, out var actor) && actor != null && actor.PlayerSession != null)
            {
                var msg = $"You have been compromised. You must protect your secret phrase!";
                _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Notifications,
                    msg,
                    msg,
                    player.Value,
                    false,
                    actor.PlayerSession.Channel
                    );
            }
        }
        component.KillTarget = null;
        component.NetworkType = RogueNetworkType.None;
        component.RogueLevel = "RogueLevel1";
        component.XP = 0;
        component.Precursor = Math.Max(0, component.Precursor-500);
        UpdateUserInterface(player, uid, component);
    }



    private void AfterInteractOn(EntityUid uid, PrecursorExtractorComponent component, AfterInteractEvent args)
    {
        if (args.Target == null || !TryComp(args.Target, out ImplantedComponent? implanted))
            return;

        if (!_interactionSystem.InRangeUnobstructed(args.User, (EntityUid)args.Target))
            return;

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.DoAfter, new PrecursorExtractorDoAfterEvent(), uid, target: args.Target, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfterSystem.TryStartDoAfter(doAfterEventArgs);
    }

    private void OnDoAfter(EntityUid uid, PrecursorExtractorComponent component, PrecursorExtractorDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;
        if(!TryComp<ActorComponent>(args.User, out var actor) || actor.PlayerSession == null)
            return;

        if (args.Args.Target != null)
        {
            var target = (EntityUid)args.Args.Target;
            bool vulnerable = false;
            if(TryComp<CuffableComponent>(target, out var cuffable) && cuffable != null)
            {
                if (_cuffable.IsCuffed((target,cuffable)))
                {
                    vulnerable = true;
                }
            }
            
            if (TryComp<MobStateComponent>(target, out var mobState))
            {
                if (mobState.CurrentState == MobState.Dead)
                {
                    vulnerable = true;
                }
                if (mobState.CurrentState == MobState.Critical)
                {
                    vulnerable = true;
                }
            }
            if (vulnerable)
            {
                if (TryComp<ImplantedComponent>(target, out var implanted))
                {
                    foreach (var implant in implanted.ImplantContainer.ContainedEntities)
                    {
                        if (TryComp<JobNetComponent>(implant, out var comp))
                        {
                            var msg = $"The secret phrase for {Name(target)} is: {comp.SecretPhrase}";
                            _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Notifications,
                            msg,
                            msg,
                            uid,
                            false,
                            actor.PlayerSession.Channel
                            );
                        }
                    }
                }
            }
            else
            {
                var msg = $"This target is not vulnerable to precursor extraction.";
                _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Notifications,
                    msg,
                    msg,
                    uid,
                    false,
                    actor.PlayerSession.Channel
                    );
            }

            args.Handled = true;
        }
    }

    private void OnPurchase(EntityUid uid, JobNetComponent component, JobNetPurchaseMessage args)
    {
        ProtoId<NetworkLevelPrototype> currentLevel = "NetworkLevel1";
        if (_meta.MetaRecords == null) return;
        if (_meta.MetaRecords.TryGetRecord(Name(args.Actor), out var record) && record != null)
        {
            currentLevel = record.Level;
        }
        else return;
        _proto.Resolve(currentLevel, out var currentProto);
        if (currentProto == null) return;
        if (currentProto.Next == null) return;
        _proto.Resolve(currentProto.Next, out var nextProto);
        if (nextProto == null) return;
        int cost = nextProto.Cost;
        if (_bank.TryGetBalance(args.Actor, out var balance))
        {
            if (cost > balance) return;
            if (_bank.TryBankWithdraw(args.Actor, cost))
            {
                record.Level = nextProto.ID;
            }
        }
        UpdateUserInterface(args.Actor, uid, component);

    }

    private void OnSelect(EntityUid uid, JobNetComponent component, JobNetSelectMessage args)
    {
        var station = _station.GetStationByID(args.ID);
        if (station == null || args.ID == 0)
        {
            var currentWorkingFor = component.WorkingFor;
            component.WorkingFor = 0;
            if (currentWorkingFor != 0 && currentWorkingFor != null)
            {
                var sId = _station.GetStationByID(currentWorkingFor.Value);
                if (sId != null) _crewManifest.BuildCrewManifest(sId.Value);
            }
            if (component.WorkingFor != 0 && component.WorkingFor != null)
            {
                var sId = _station.GetStationByID(component.WorkingFor.Value);
                if (sId != null) _crewManifest.BuildCrewManifest(sId.Value);
            }
            _card.UpdateIDAssignment(Name(args.Actor), args.ID);
            UpdateUserInterface(args.Actor, uid, component);
            return;
        }

        if (TryComp<CrewRecordsComponent>(station, out var crewRecord) && crewRecord != null)
        {
            if (crewRecord.TryGetRecord(Name(args.Actor), out var record) && record != null)
            {
                if (TryComp<StationDataComponent>(station, out var stationData))
                {
                    if (TryComp<CrewAssignmentsComponent>(station, out var crewAssignments))
                    {
                        if (crewAssignments.TryGetAssignment(record.AssignmentID, out var assignment) && assignment != null)
                        {
                            if (component.LastWorkedFor != stationData.UID)
                                component.WorkedTime = TimeSpan.Zero;
                            var currentWorkingFor = component.WorkingFor;
                            component.WorkingFor = stationData.UID;
                            if (currentWorkingFor != 0 && currentWorkingFor != null)
                            {
                                var sId = _station.GetStationByID(currentWorkingFor.Value);
                                if (sId != null) _crewManifest.BuildCrewManifest(sId.Value);
                            }
                            if (component.WorkingFor != 0 && component.WorkingFor != null)
                            {
                                var sId = _station.GetStationByID(component.WorkingFor.Value);
                                if (sId != null) _crewManifest.BuildCrewManifest(sId.Value);
                            }
                            _card.UpdateIDAssignment(Name(args.Actor), args.ID);
                            UpdateUserInterface(args.Actor, uid, component);
                        }
                    }
                }
            }
        }

    }

    private void OnPurchasePrecursor(EntityUid uid, JobNetComponent component, JobNetPurchasePrecursorMessage args)
    {
        EntityUid? player = null;
        if (TryComp<TransformComponent>(uid, out var comp) && comp != null)
        {
            player = comp.ParentUid;
        }
        if (!TryComp<ActorComponent>(player, out var actor) || actor == null || actor.PlayerSession == null) return;
        var prod = _proto.Index<CargoProductPrototype>(args.ID);
        if (prod == null) return;
        int requiredLevel = 0;
        if (prod.Group == "syndicatemarket") requiredLevel = 1;
        if (prod.Group == "syndicatemarket2") requiredLevel = 2;
        if (prod.Group == "syndicatemarket3") requiredLevel = 3;
        if (prod.Group == "syndicatemarket4") requiredLevel = 4;
        var level = _proto.Index(component.RogueLevel);
        if(level.ItemLevel < requiredLevel)
        {
            _audio.PlayEntity(component.ErrorSound, player.Value, player.Value);
            var msg = $"You do not have the rogue level required to purchase this.";
            if (msg != null)
                _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Notifications,
                    msg,
                    msg,
                    player.Value,
                    false,
                    actor.PlayerSession.Channel
                    );
            return;
        }
        if(prod.Cost > component.Precursor)
        {
            _audio.PlayEntity(component.ErrorSound, player.Value, player.Value);
            var msg = $"You have insufficent stored precursor. You need {prod.Cost-component.Precursor} more precursor.";
            if (msg != null)
                _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Notifications,
                    msg,
                    msg,
                    player.Value,
                    false,
                    actor.PlayerSession.Channel
                    );
            return;
        }
        var query = EntityQueryEnumerator<CargoTelepadComponent>();
        var found = false;
        var userMapPos = _transform.GetMapCoordinates(player.Value);
        while (query.MoveNext(out var telepad, out var telepadcomp))
        {
            var targetMapPos = _transform.GetMapCoordinates(telepad);
            var calculatedDistance = targetMapPos.Position - userMapPos.Position;
            var total = calculatedDistance.Length();
            if(total <= 3)
            {
                var teleTransform = Transform(telepad);
                var newEntity = Spawn(prod.Product, teleTransform.Coordinates);
                _audio.PlayEntity(component.PaySuccessSound, player.Value, player.Value);
                component.Precursor -= prod.Cost;
                found = true;
                break;
            }
        }
        if(!found)
        {
            _audio.PlayEntity(component.ErrorSound, player.Value, player.Value);
            var msg = $"You must be next to a telepad to make purchases.";
            if (msg != null)
                _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Notifications,
                    msg,
                    msg,
                    player.Value,
                    false,
                    actor.PlayerSession.Channel
                    );
            return;
        }
        UpdateUserInterface(args.Actor, uid, component);
    }

    private void OnSelectRogueNet(EntityUid uid, JobNetComponent component, JobNetSelectRogueNetMessage args)
    {
        if (component.NetworkType != RogueNetworkType.None) return;
        if (component.RogueLevel == "RogueLevel1") return;
        component.NetworkType = args.Net;
        UpdateUserInterface(args.Actor, uid, component);
    }

    private void OnJobNetOpenAttempt(EntityUid uid, JobNetComponent component, ActivatableUIOpenAttemptEvent args)
    {
        if (!_mind.TryGetMind(args.User, out var mind, out _))
            return;

        _popup.PopupEntity("Job Network Not Available.", uid, args.User);
        args.Cancel();
    }

    private void OnMapInit(EntityUid uid, JobNetComponent component, MapInitEvent args)
    {

    }

    private void OnStartup(EntityUid uid, JobNetComponent component, ComponentStartup args)
    {
        var currentWorkingFor = component.WorkingFor;
        if (currentWorkingFor != 0 && currentWorkingFor != null)
        {
            var sId = _station.GetStationByID(currentWorkingFor.Value);
            if (sId == null) return;
            var jobNetEnabled = _station.GetJobNetStatus(sId.Value);
            if (!jobNetEnabled)
            {
                component.WorkingFor = 0;
            }
            else
            {
                _crewManifest.BuildCrewManifest(sId.Value);
            }
        }
    }

    private void OnShutdown(EntityUid uid, JobNetComponent component, ComponentShutdown args)
    {
        var currentWorkingFor = component.WorkingFor;
        component.WorkingFor = 0;
        if (currentWorkingFor != 0 && currentWorkingFor != null)
        {
            var sId = _station.GetStationByID(currentWorkingFor.Value);
            if (sId != null) _crewManifest.BuildCrewManifest(sId.Value);
        }
        component.WorkingFor = currentWorkingFor;
    }

    private void OnImplantActivate(EntityUid uid, JobNetComponent component, OpenJobNetImplantEvent args)
    {
        ToggleUi(args.Performer, uid, component);
    }

    private void OnJobNetImplantImplanted(Entity<JobNetComponent> ent, ref ImplantImplantedEvent args)
    {
        var mob = args.Implanted;
        _bank.EnsureAccount(Name(mob), 50);
    }

    public void TryAssignRogueObjective(EntityUid user, JobNetComponent component)
    {
        component.KillTarget = null;
        component.DealerBounty = null;
        if (component.NetworkType == RogueNetworkType.BountyHunter || component.NetworkType == RogueNetworkType.Assassin)
        {
            var query = EntityQueryEnumerator<JobNetComponent>();
            List<EntityUid> possibleTargets = new();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (comp == component) continue;
                if (component.NetworkType == comp.NetworkType || comp.NetworkType == RogueNetworkType.None) continue;
                possibleTargets.Add(uid);
            }
            if (possibleTargets.Count < 1) return;
            var chosenTarget = _random.Pick(possibleTargets);
            var tf = Transform(chosenTarget);
            var parent = tf.ParentUid;
            component.KillTarget = Name(parent);

        }
        else if (component.NetworkType == RogueNetworkType.Dealer)
        {
            List<CargoBountyPrototype> possible = new();
            var query = EntityQueryEnumerator<TradeStationComponent>();
            int tradeStationUID = 0;
            List<TradeStationComponent> possibleTrade = new();
            while (query.MoveNext(out var uid, out var comp))
            {
                if(TryComp<StationMemberComponent>(uid, out var sm))
                {
                    possibleTrade.Add(comp);
                }
            }
            if (possibleTrade.Count < 1) return;
            var chosenUID = _random.Pick(possibleTrade).UID;
            foreach (var proto in _proto.EnumeratePrototypes<CargoBountyPrototype>())
            {
                if(proto.Group == "PrecursorBounty")
                {
                    possible.Add(proto);
                }
            }
            if (possible.Count < 1) return;
            var chosen = _random.Pick(possible);
            _nameIdentifier.GenerateUniqueName(user, "Bounty", out var randomVal);
            var newBounty = new CargoBountyData(chosen, randomVal);
            newBounty.TradeStationUID = chosenUID;
            component.DealerBounty = newBounty;
        }

    }
    public void TryAssignPrecursorObjective(EntityUid user, JobNetComponent component)
    {
        component.PrecursorObjectives.Clear();
        var precursorObjectives = _proto.EnumeratePrototypes<PrecursorObjectivePrototype>();
        var form = precursorObjectives.ToList().Shuffle();
        component.PrecursorObjectives.AddRange(form.First().ID);
    }
    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<JobNetComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.WorkingFor != null && comp.WorkingFor != 0)
            {
                comp.WorkedTime += TimeSpan.FromSeconds(frameTime);
                if (comp.WorkedTime > TimeSpan.FromMinutes(20))
                {
                    comp.WorkedTime = TimeSpan.Zero;
                    TryPay(comp.Owner, comp);
                }
            }

            comp.PrecursorResetTime -= TimeSpan.FromSeconds(frameTime);
            if (comp.PrecursorResetTime <= TimeSpan.Zero)
            {
                var words = _codeword.GenerateCodewords("PersistenceCodewordGenerator");
                comp.SecretPhrase = string.Join(" ", words);
                comp.PrecursorResetTime = TimeSpan.FromMinutes(45);
                TryAssignPrecursorObjective(uid, comp);
            }
            comp.RogueNetResetTime -= TimeSpan.FromSeconds(frameTime);
            if (comp.NetworkType != RogueNetworkType.None && comp.RogueNetResetTime <= TimeSpan.Zero)
            {
                comp.RogueNetResetTime = TimeSpan.FromMinutes(180);
                TryAssignRogueObjective(uid, comp);
            }


        }
        base.Update(frameTime);
    }
    public void TryPay(EntityUid user, JobNetComponent component)
    {

        if (component.WorkingFor == null || component.WorkingFor == 0) return;
        var station = _station.GetStationByID(component.WorkingFor.Value);
        if (station == null)
        {
            component.WorkingFor = 0;
            return;
        }
        EntityUid? player = null;
        if (TryComp<TransformComponent>(user, out var comp) && comp != null)
        {
            player = comp.ParentUid;
        }
        if (player == null) return;
        var name = Name(player.Value);
        if (TryComp<CrewRecordsComponent>(station, out var crewRecord) && crewRecord != null)
        {
            if (crewRecord.TryGetRecord(name, out var record) && record != null)
            {
                if (TryComp<StationDataComponent>(station, out var stationData))
                {
                    if (TryComp<CrewAssignmentsComponent>(station, out var crewAssignments))
                    {
                        if (crewAssignments.TryGetAssignment(record.AssignmentID, out var assignment) && assignment != null)
                        {
                            if (assignment.Wage > 0)
                            {
                                if (TryComp<ActorComponent>(player, out var actor) && actor != null && actor.PlayerSession != null)
                                {
                                    record.LastPaid = DateTime.Now;
                                    var bank = _bank.GetMoneyAccountsComponent();
                                    if (bank == null) return;
                                    if (_cargo.TryGetAccount(station.Value, "Cargo", out var money))
                                    {
                                        if (money < assignment.Wage)
                                        {
                                            _audio.PlayEntity(component.ErrorSound, player.Value, player.Value);
                                            var msg = $"{stationData.StationName} has failed to pay you your ${assignment.Wage} due to insufficient funds.";
                                            if (msg != null)
                                                _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Notifications,
                                                    msg,
                                                    msg,
                                                    station.Value,
                                                    false,
                                                    actor.PlayerSession.Channel
                                                    );
                                            return;
                                        }
                                        if (bank.TryGetAccount(name, out var account) && account != null)
                                        {
                                            _audio.PlayEntity(component.PaySuccessSound, player.Value, player.Value);
                                            account.Balance += assignment.Wage;
                                            _cargo.TryAdjustBankAccount(station.Value, "Cargo", -assignment.Wage);
                                            var msg = $"You have received ${assignment.Wage} for working as a {assignment.Name} for {stationData.StationName}.";
                                            if (msg != null)
                                                _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Notifications,
                                                    msg,
                                                    msg,
                                                    station.Value,
                                                    false,
                                                    actor.PlayerSession.Channel
                                                    );
                                            _bank.DirtyMoneyAccountsComponent();
                                        }
                                    }
                                    else
                                    {
                                        _audio.PlayEntity(component.ErrorSound, player.Value, player.Value);
                                        var msg = $"{stationData.StationName} has failed to pay you your ${assignment.Wage} due to an invalid account.";
                                        if (msg != null)
                                            _chatManager.ChatMessageToOne(Shared.Chat.ChatChannel.Notifications,
                                                msg,
                                                msg,
                                                station.Value,
                                                false,
                                                actor.PlayerSession.Channel
                                                );
                                        return;
                                    }

                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
