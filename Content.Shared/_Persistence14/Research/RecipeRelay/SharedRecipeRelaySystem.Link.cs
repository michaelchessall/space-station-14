using System.Linq;
using Content.Shared._Persistence14.PersistentIdentifier;
using Content.Shared._Persistence14.PersistentIdentifier.Reference;

namespace Content.Shared._Persistence14.Research.RecipeRelay;

public sealed partial class SharedRecipeRelaySystem
{
    /// <summary>
    /// Links the source to the receiver.
    /// </summary>
    public bool TryLinkRelay(Entity<RecipeRelaySourceComponent> sourceEnt, Entity<RecipeRelayReceiverComponent> receiverEnt, EntityUid? user = null)
    {
        if (IsLinked(sourceEnt, receiverEnt))
            return false;

        if (CreatesRecursiveLoop(sourceEnt, receiverEnt))
        {
            if (user is null)
                _popup.PopupEntity(Loc.GetString("creates-loop-link-fail", ("receiver", Name(receiverEnt.Owner))), sourceEnt);
            else
                _popup.PopupEntity(Loc.GetString("creates-loop-link-fail", ("receiver", Name(receiverEnt.Owner))), sourceEnt, user.Value);
            return false;
        }

        if (receiverEnt.Comp.HasSource)
        {
            if (user is null)
                _popup.PopupEntity(Loc.GetString("already-linked-link-fail", ("receiver", Name(receiverEnt.Owner))), sourceEnt);
            else
                _popup.PopupEntity(Loc.GetString("already-linked-link-fail", ("receiver", Name(receiverEnt.Owner))), sourceEnt, user.Value);
            return false;
        }

        // Provide an opportunity for other systems to intercept.
        var ev = new RecipeRelayLinkAttemptEvent()
        {
            Source = sourceEnt,
            Receiver = receiverEnt,
            User = user
        };
        RaiseLocalEvent(sourceEnt, ref ev);
        RaiseLocalEvent(receiverEnt, ref ev);

        if (ev.Cancelled)
        {
            var msg = Loc.TryGetString(ev.CancelMessage, out var message, ("source", Name(sourceEnt.Owner)), ("receiver", Name(receiverEnt.Owner)))
                        ? message : ev.CancelMessage;
            if (user is null)
                _popup.PopupEntity(msg, sourceEnt.Owner);
            else
                _popup.PopupEntity(msg, sourceEnt.Owner, user.Value);
            return false;
        }

        sourceEnt.Comp.Receivers.Add(_pid.EnsureId(receiverEnt));
        receiverEnt.Comp.Source = _pid.EnsureId(sourceEnt);
        Dirty(sourceEnt);
        Dirty(receiverEnt);

        // Broadcast success event
        var successEv = new RecipeRelayLinkSuccessEvent()
        {
            Source = sourceEnt,
            Receiver = receiverEnt,
            User = user
        };
        RaiseLocalEvent(sourceEnt, successEv);
        RaiseLocalEvent(receiverEnt, successEv);
        UpdateSourceUI(sourceEnt, user);
        return true;
    }

    /// <summary>
    /// Attempts to unlink the source and receiver.
    /// </summary>
    public bool TryUnlinkRelay(Entity<RecipeRelaySourceComponent> sourceEnt, Entity<RecipeRelayReceiverComponent> receiverEnt, EntityUid? user = null)
    {
        if (!IsLinked(sourceEnt, receiverEnt))
            return false;

        // Provide an opportunity for other systems to intercept.
        var ev = new RecipeRelayUnlinkAttemptEvent()
        {
            Source = sourceEnt,
            Receiver = receiverEnt,
            User = user
        };
        RaiseLocalEvent(sourceEnt, ref ev);
        RaiseLocalEvent(receiverEnt, ref ev);

        if (ev.Cancelled)
        {
            var msg = Loc.TryGetString(ev.CancelMessage, out var message, ("source", ToPrettyString(sourceEnt.Owner)), ("receiver", ToPrettyString(receiverEnt.Owner)))
                        ? message : ev.CancelMessage;
            if (user is null)
                _popup.PopupEntity(msg, sourceEnt.Owner);
            else
                _popup.PopupEntity(msg, sourceEnt.Owner, user.Value);
            return false;
        }

        sourceEnt.Comp.Receivers.Remove(_pid.EnsureId(receiverEnt));
        receiverEnt.Comp.Source = PersistentIdentifierSystem.EmptyId;
        Dirty(sourceEnt);
        Dirty(receiverEnt);

        // Broadcast success event
        var successEv = new RecipeRelayUnlinkSuccessEvent()
        {
            Source = sourceEnt,
            Receiver = receiverEnt,
            User = user
        };
        RaiseLocalEvent(sourceEnt, successEv);
        RaiseLocalEvent(receiverEnt, successEv);
        UpdateSourceUI(sourceEnt, user);
        return true;
    }

    /// <summary>
    /// Unlinks all receivers from a given source.
    /// </summary>
    public void ClearSourceLinks(Entity<RecipeRelaySourceComponent> sourceEnt, EntityUid? user = null)
    {
        foreach (var linkedReceiver in sourceEnt.Comp.Receivers.ToArray())
        {
            if (!_pid.TryResolveId(linkedReceiver, out var receiverEnt) ||
                !TryComp<RecipeRelayReceiverComponent>(receiverEnt.Owner, out var receiver))
            {
                sourceEnt.Comp.Receivers.Remove(linkedReceiver); // Stale reference, remove
                continue;
            }

            TryUnlinkRelay(sourceEnt, (receiverEnt.Owner, receiver), user);
            // Dirtying and Events handled by unlink method
        }
    }

    /// <summary>
    /// If linked, unlinks the source and receiver.<br/>
    /// If unlinked, links the source and receiver.
    /// </summary>
    public void ToggleRelayLink(Entity<RecipeRelaySourceComponent> sourceEnt, Entity<RecipeRelayReceiverComponent> receiverEnt, EntityUid? user = null)
    {
        if (IsLinked(sourceEnt, receiverEnt))
            TryUnlinkRelay(sourceEnt, receiverEnt, user);
        else
            TryLinkRelay(sourceEnt, receiverEnt, user);
        // Dirtying and events handled in Unlink and Link methods
    }

    /// <summary>
    /// Returns the current state of a link between the source and receiver.
    /// </summary>
    public bool IsLinked(Entity<RecipeRelaySourceComponent> sourceEnt, Entity<RecipeRelayReceiverComponent> receiverEnt)
    {
        return sourceEnt.Comp.Receivers.Contains(_pid.EnsureId(receiverEnt));
    }

    /// <summary>
    /// Checks whether allowing a given link to form would create a recursive loop.
    /// </summary>
    private bool CreatesRecursiveLoop(Entity<RecipeRelaySourceComponent> sourceEnt, Entity<RecipeRelayReceiverComponent> receiverEnt)
    {
        if (sourceEnt.Owner == receiverEnt.Owner)
            return true; // Obviously recursive...

        var visited = new HashSet<PersistentEntityReference>
        {
            _pid.EnsureId(receiverEnt.Owner)
        };
        var source = sourceEnt;

        while (source.Comp.AllowRecursiveRelay)
        {
            var pid = _pid.EnsureId(source.Owner);
            if (visited.Contains(pid))
                return true;
            visited.Add(pid);

            if (!TryComp<RecipeRelayReceiverComponent>(source.Owner, out var sourceReceiver) ||
                !_pid.TryResolveId(sourceReceiver.Source, out var nextSource) ||
                !TryComp<RecipeRelaySourceComponent>(nextSource.Owner, out var nextSourceComp))
                return false;

            source = (nextSource.Owner, nextSourceComp);
        }

        return false;
    }
}