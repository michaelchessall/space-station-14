using System.Linq;
using Content.Shared.Access.Systems;
using Content.Shared.Examine;
using Content.Shared.Labels.Components;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Morgue.Components;
using Content.Shared.Storage.Components;
using Robust.Shared.Player;

namespace Content.Shared.Morgue;

public abstract class SharedMorgueSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedIdCardSystem _id = default!;
    [Dependency] private LabelSystem _label = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MorgueComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<MorgueComponent, StorageAfterCloseEvent>(OnClosed);
        SubscribeLocalEvent<MorgueComponent, StorageAfterOpenEvent>(OnOpened);
    }

    /// <summary>
    /// Handles the examination text for looking at a morgue.
    /// </summary>
    private void OnExamine(Entity<MorgueComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        _appearance.TryGetData<MorgueContents>(ent.Owner, MorgueVisuals.Contents, out var contents);

        var text = contents switch
        {
            MorgueContents.HasSoul => "morgue-entity-storage-component-on-examine-details-body-has-soul",
            MorgueContents.HasContents => "morgue-entity-storage-component-on-examine-details-has-contents",
            MorgueContents.HasMob => "morgue-entity-storage-component-on-examine-details-body-has-no-soul",
            _ => "morgue-entity-storage-component-on-examine-details-empty"
        };

        args.PushMarkup(Loc.GetString(text));
    }

    private void OnClosed(Entity<MorgueComponent> ent, ref StorageAfterCloseEvent args)
    {
        CheckContents(ent.Owner, ent.Comp);
    }

    private void OnOpened(Entity<MorgueComponent> ent, ref StorageAfterOpenEvent args)
    {
        CheckContents(ent.Owner, ent.Comp);
    }

    /// <summary>
    /// Updates data in case something died/got deleted in the morgue.
    /// </summary>
    public void CheckContents(EntityUid uid, MorgueComponent? morgue = null, EntityStorageComponent? storage = null, AppearanceComponent? app = null)
    {
        if (!Resolve(uid, ref morgue, ref storage, ref app))
            return;

        if (storage.Contents.ContainedEntities.Count == 0)
        {
            _appearance.SetData(uid, MorgueVisuals.Contents, MorgueContents.Empty, app);
            return;
        }

        var hasMob = false;
        bool hasSoul = false;

        List<string> uniqueNames = new();

        foreach (var ent in storage.Contents.ContainedEntities)
        {
            if (!hasMob && HasComp<MobStateComponent>(ent))
                hasMob = true;

            if (HasComp<ActorComponent>(ent))
                hasSoul = true;

            if (_id.TryGetIdCard(ent, out var idCard)) // If it has an ID Card, use that.
                uniqueNames.Add(idCard.Comp.NameLocId);
            else // If it doesn't have an ID Card, get the entities name
            {
                try
                {
                    var name = Name(ent);
                    uniqueNames.Add(name);
                }
                catch (KeyNotFoundException) { } // If the entity doesn't have a name, don't do anything.
            }
        }

        if (TryComp<LabelComponent>(uid, out var labelComp)) // Update the label to match the name of the stored entities.
            if (uniqueNames.Count > 0)
                _label.Label(uid, ConstructNameLabel(uniqueNames), label: labelComp);
            else _label.Label(uid, null, label: labelComp);

        var appearanceState = hasSoul ? MorgueContents.HasSoul : hasMob ? MorgueContents.HasMob : MorgueContents.HasContents;
        _appearance.SetData(uid, MorgueVisuals.Contents, appearanceState, app);
    }

    private string ConstructNameLabel(List<string> names)
    {
        List<string> converted = new();

        foreach (var name in names)
        {
            if (Loc.TryGetString(name, out var loc))
                converted.Add(loc);
            else
                converted.Add(name);
        }

        return string.Join(", ", converted);
    }
}
