using System.Linq;
using Content.Shared._Persistence14.PersistentIdentifier;
using Content.Shared._Persistence14.PersistentIdentifier.Reference;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Robust.Shared.Player;

namespace Content.Shared._Persistence14.Research.RecipeRelay;

public sealed partial class SharedRecipeRelaySystem : EntitySystem
{
    [Dependency] private readonly PersistentIdentifierSystem _pid = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RecipeRelaySourceComponent, ComponentStartup>(OnSourceStartup);
        SubscribeLocalEvent<RecipeRelayReceiverComponent, ComponentStartup>(OnReceiverStartup);

        SubscribeLocalEvent<RecipeRelayToggleReceieverMessage>(OnToggleReceiverMessage);
        SubscribeLocalEvent<RecipeRelaySourceComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<RecipeRelaySourceComponent, BoundUIOpenedEvent>(OnUiOpened);
    }

    /// <summary>
    /// Attempts to retrieve the recipe container on an entity. Uses any available recipe relay connections.<br/><br/>
    /// 
    /// Returns true if a <see cref="RecipeContainerComponent"/> was successfully found, otherwise false.
    /// </summary>
    public bool TryGetRecipeContainer(EntityUid uid, out Entity<RecipeContainerComponent> container, RecipeContainerComponent? selfContainer = null)
    {
        container = default!;

        if (TryComp<RecipeRelayReceiverComponent>(uid, out var receiver) &&
            TryGetRelaySource((uid, receiver), out var sourceContainer))
        {
            container = sourceContainer;
            return true;
        }

        if (!Resolve(uid, ref selfContainer))
            return false;

        container = (uid, selfContainer);
        return true;
    }


    /// <summary>
    /// Attempts to retrieve a <see cref="RecipeContainerComponent"/> through a connected relay.
    /// Applies recursively if <see cref="RecipeRelaySourceComponent.AllowRecursiveRelay"/> is true.
    /// </summary>
    private bool TryGetRelaySource(Entity<RecipeRelayReceiverComponent> receiverEnt, out Entity<RecipeContainerComponent> container)
    {
        container = default!;

        // Invalid source ID
        if (!_pid.TryResolveId(receiverEnt.Comp.Source, out var sourceEnt) ||
            !TryComp<RecipeRelaySourceComponent>(sourceEnt.Owner, out var source))
            return false;

        // Check recursively connected relay sources.
        if (source.AllowRecursiveRelay &&
            TryComp<RecipeRelayReceiverComponent>(sourceEnt.Owner, out var sourceReceiver))
            return TryGetRelaySource((sourceEnt.Owner, sourceReceiver), out container);

        // Final source isn't a container...
        if (!TryComp<RecipeContainerComponent>(sourceEnt.Owner, out var sourceContainer))
            return false;

        container = (sourceEnt.Owner, sourceContainer);
        return true;
    }

    private void OnSourceStartup(Entity<RecipeRelaySourceComponent> source, ref ComponentStartup args) => EnsureSourceIntegrity(source);
    private void OnReceiverStartup(Entity<RecipeRelayReceiverComponent> receiver, ref ComponentStartup args) => EnsureReceiverIntegrity(receiver);

    /// <summary>
    /// Ensures all is well inside a relay source.
    /// </summary>
    /// <param name="source"></param>
    private void EnsureSourceIntegrity(Entity<RecipeRelaySourceComponent> source)
    {
        foreach (var receiverId in source.Comp.Receivers.ToArray())
        {
            if (!_pid.TryResolveId(receiverId, out var receiverEnt) ||
                !TryComp<RecipeRelayReceiverComponent>(receiverEnt.Owner, out var receiver) ||
                receiver.Source != _pid.EnsureId(source.Owner))
            {
                source.Comp.Receivers.Remove(receiverId);
                continue;
            }
        }

        Dirty(source);
    }

    /// <summary>
    /// Ensures all is well inside a relay receiver.
    /// </summary>
    private void EnsureReceiverIntegrity(Entity<RecipeRelayReceiverComponent> receiver)
    {
        if (!_pid.TryResolveId(receiver.Comp.Source, out var sourceEnt) ||
            !TryComp<RecipeRelaySourceComponent>(sourceEnt.Owner, out var source) ||
            !source.Receivers.Contains(_pid.EnsureId(receiver.Owner)))
        {
            receiver.Comp.Source = PersistentIdentifierSystem.EmptyId;
            Dirty(receiver);
        }
    }

    private void OnToggleReceiverMessage(RecipeRelayToggleReceieverMessage message)
    {
        var sourceUid = GetEntity(message.Entity);
        var receiverUid = GetEntity(message.Receiver);

        if (!TryComp<RecipeRelaySourceComponent>(sourceUid, out var sourceComp) ||
            !TryComp<RecipeRelayReceiverComponent>(receiverUid, out var receiverComp))
            return;

        ToggleRelayLink((sourceUid, sourceComp), (receiverUid, receiverComp), message.Actor);
    }

    public void UpdateSourceUI(Entity<RecipeRelaySourceComponent> sourceEnt, EntityUid? user)
    {
        var (uid, comp) = sourceEnt;
        var sourceKey = _pid.EnsureId(uid, out var sourceIdEnt);
        var sourceId = sourceIdEnt.Comp;
        var sourceTransform = Transform(uid);
        if (sourceTransform is null)
            return;

        var validReceivers = new List<RecipeRelayReceiverState>();

        var query = EntityQueryEnumerator<RecipeRelayReceiverComponent, TransformComponent>();
        var source = (uid, comp, sourceTransform, sourceId);
        while (query.MoveNext(out var receiverUid, out var receiverComp, out var receiverTransform))
        {
            var receiver = (receiverUid, receiverComp, receiverTransform);
            if (!IsValidReceiver(source, receiver, user, out var canChangeState, out var tooltip))
                continue;

            var connected = receiverComp.Source == sourceKey;
            validReceivers.Add(new RecipeRelayReceiverState(GetNetEntity(receiverUid), Name(receiverUid), connected, canChangeState, tooltip));
        }

        var state = new RecipeRelaySourceBoundUserInterfaceState(validReceivers);
        _ui.SetUiState(uid, RecipeRelaySourceUIKey.Main, state);
    }

    private bool IsValidReceiver(
        Entity<RecipeRelaySourceComponent, TransformComponent, PersistentIdentifierComponent> source,
        Entity<RecipeRelayReceiverComponent, TransformComponent> receiver, EntityUid? user, out bool canChangeState, out string? blockedTooltip)
    {
        canChangeState = true;
        blockedTooltip = null;
        if (source.Comp2.MapID != receiver.Comp2.MapID)
            return false; // Can never link across maps.

        // TODO: It feels very odd that this is duplicated twice... these events are too similar.

        // Receiver and source are already connected
        if (receiver.Comp1.Source == source.Comp3.Id)
        {
            var args = new RecipeRelayUnlinkAttemptEvent
            {
                Source = source,
                Receiver = receiver,
                User = user
            };
            RaiseLocalEvent(source, ref args);
            RaiseLocalEvent(receiver, ref args);
            canChangeState = !args.Cancelled;
            blockedTooltip = args.BlockedTooltip;
            return args.AllowDisplay;
        }

        // Receiver and source are already
        if (!receiver.Comp1.HasSource)
        {
            var args = new RecipeRelayLinkAttemptEvent
            {
                Source = source,
                Receiver = receiver,
                User = user
            };
            RaiseLocalEvent(source, ref args);
            RaiseLocalEvent(receiver, ref args);
            canChangeState = !args.Cancelled;
            blockedTooltip = args.BlockedTooltip;
            return args.AllowDisplay;
        }

        // Reaching this point implies that receiver is already connected and that it is not connected to this source.
        return false;
    }

    private void OnPowerChanged(Entity<RecipeRelaySourceComponent> source, ref PowerChangedEvent args)
    {
        if (args.Powered)
            return;

        _ui.CloseUi(source.Owner, RecipeRelaySourceUIKey.Main);
    }

    private void OnUiOpened(Entity<RecipeRelaySourceComponent> source, ref BoundUIOpenedEvent args)
    {
        UpdateSourceUI(source, args.Actor);
    }
}