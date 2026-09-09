using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Persistence14.Research.RecipeRelay;

public sealed partial class SharedRecipeRelaySystem
{
    /// <summary>
    /// Attempts to transfer a recipe from one recipe container to another.
    /// </summary>
    public bool TryTransferRecipe(EntityUid from, EntityUid to,
        ProtoId<LatheRecipePrototype> recipeId, int count = -1,
        PermanentRecipeBehavior permanentRecipeBehavior = PermanentRecipeBehavior.None)
    {
        if (!TryGetRecipeContainer(from, out var fromContainer) ||
            !TryGetRecipeContainer(to, out var toContainer) ||
            fromContainer.Owner == toContainer.Owner) // Relays end up pointing to same container.
            return false;

        // Handle Permanent Recipes
        if (fromContainer.Comp.PermanentRecipes.Contains(recipeId))
        {
            switch (permanentRecipeBehavior)
            {
                case PermanentRecipeBehavior.Copy:
                    if (toContainer.Comp.PermanentRecipes.Add(recipeId))
                        Dirty(toContainer);
                    return true; // Copy doesn't care that its already there.
                case PermanentRecipeBehavior.Transfer:
                    if (toContainer.Comp.PermanentRecipes.Add(recipeId))
                    {
                        fromContainer.Comp.PermanentRecipes.Remove(recipeId);
                        Dirty(fromContainer);
                        Dirty(toContainer);
                        return true;
                    }
                    return false;
            }
        }

        if (!fromContainer.Comp.UnlockedRecipes.TryGetValue(recipeId, out var qty))
            return false;

        if (count < 0 || count > qty)
            count = qty;

        var current = 0;
        if (toContainer.Comp.UnlockedRecipes.TryGetValue(recipeId, out var curr))
            current = curr;
        toContainer.Comp.UnlockedRecipes[recipeId] = current + count;
        fromContainer.Comp.UnlockedRecipes[recipeId] -= count;
        if (fromContainer.Comp.UnlockedRecipes[recipeId] <= 0)
            fromContainer.Comp.UnlockedRecipes.Remove(recipeId);

        Dirty(fromContainer);
        Dirty(toContainer);
        return true;
    }

    /// <summary>
    /// Attempts to add an unlockable recipe to the container.
    /// </summary>
    public bool TryAddUnlockRecipe(EntityUid uid, ProtoId<LatheRecipePrototype> recipeId, int count = 1)
    {
        if (!TryGetRecipeContainer(uid, out var container))
            return false;

        var current = 0;
        if (container.Comp.UnlockedRecipes.TryGetValue(recipeId, out var curr))
            current = curr;
        container.Comp.UnlockedRecipes[recipeId] = current + count;
        return true;
    }

    /// <summary>
    /// Attempts to remove a specific quantity of an unlocked recipe from a container. 
    /// When count is negative (by default) all of the specified recipe are removed.
    /// </summary>
    public bool TryRemoveUnlockRecipe(EntityUid uid, ProtoId<LatheRecipePrototype> recipeId, int count = -1, bool allowOverdraw = true)
        => TryRemoveUnlockRecipe(uid, recipeId, out _, count, allowOverdraw);
    /// <summary>
    /// Attempts to remove a specific quantity of an unlocked recipe from a container. 
    /// When count is negative (by default) all of the specified recipe are removed. 
    /// Provides any overflow as an output variable
    /// </summary>
    public bool TryRemoveUnlockRecipe(EntityUid uid, ProtoId<LatheRecipePrototype> recipeId, out int overflow, int count = -1, bool allowOverdraw = true)
    {
        overflow = 0;
        if (!TryGetRecipeContainer(uid, out var container))
            return false;

        if (!container.Comp.UnlockedRecipes.TryGetValue(recipeId, out var qty))
            return false;

        if (count >= qty)
        {
            overflow = count - qty;
            if (count > qty && !allowOverdraw) return false;
            container.Comp.UnlockedRecipes.Remove(recipeId);
            Dirty(container);
            return true;
        }

        if (count < 0)
        {
            container.Comp.UnlockedRecipes.Remove(recipeId);
            Dirty(container);
            return true;
        }

        container.Comp.UnlockedRecipes[recipeId] -= count;
        Dirty(container);
        return true;
    }

    /// <summary>
    /// Removes all recipes from the container. Has options for removing just permanent or just unlocked recipes.
    /// </summary>
    public void ClearRecipes(EntityUid uid, bool clearPermanent = true, bool clearUnlocked = true)
    {
        if (!TryGetRecipeContainer(uid, out var container))
            return; // No container to clear

        if (clearPermanent) container.Comp.PermanentRecipes.Clear();
        if (clearUnlocked) container.Comp.UnlockedRecipes.Clear();
        Dirty(container);
    }

    /// <summary>
    /// Copies the recipes from one container to another.
    /// </summary>
    public void CopyTo(Entity<RecipeContainerComponent> root, Entity<RecipeContainerComponent> copy)
    {
        foreach (var perm in copy.Comp.PermanentRecipes)
        {
            root.Comp.PermanentRecipes.Add(perm);
        }

        foreach (var (key, qty) in copy.Comp.UnlockedRecipes)
        {
            var current = 0;
            if (root.Comp.UnlockedRecipes.TryGetValue(key, out var curr))
                current = curr;
            root.Comp.UnlockedRecipes[key] = current + qty;
        }

        Dirty(root);
    }

    public enum PermanentRecipeBehavior
    {
        None,
        Copy,
        Transfer
    }
}