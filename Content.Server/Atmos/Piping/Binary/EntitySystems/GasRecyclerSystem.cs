using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Binary.Components;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping;
using Content.Shared.Atmos.Piping.Binary;
using Content.Shared.Atmos.Piping.Components;
using Content.Shared.Audio;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server.Atmos.Piping.Binary.EntitySystems
{
    [UsedImplicitly]
    public sealed class GasRecyclerSystem : EntitySystem
    {
        [Dependency] private readonly AppearanceSystem _appearance = default!;
        [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
        [Dependency] private readonly SharedAmbientSoundSystem _ambientSoundSystem = default!;
        [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
        [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<GasRecyclerComponent, AtmosDeviceEnabledEvent>(OnEnabled);
            SubscribeLocalEvent<GasRecyclerComponent, AtmosDeviceUpdateEvent>(OnUpdate);
            SubscribeLocalEvent<GasRecyclerComponent, AtmosDeviceDisabledEvent>(OnDisabled);
            SubscribeLocalEvent<GasRecyclerComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<GasRecyclerComponent, EntRemovedFromContainerMessage>(OnContainerRemoved);
        }

        private void OnEnabled(EntityUid uid, GasRecyclerComponent comp, ref AtmosDeviceEnabledEvent args)
        {
            UpdateAppearance(uid, comp);
        }

        private void OnExamined(Entity<GasRecyclerComponent> ent, ref ExaminedEvent args)
        {
            var comp = ent.Comp;
            if (!Comp<TransformComponent>(ent).Anchored || !args.IsInDetailsRange)
                return;

            if (!_nodeContainer.TryGetNode(ent.Owner, comp.InletName, out PipeNode? inlet))
                return;

            using (args.PushGroup(nameof(GasRecyclerComponent)))
            {
                EntityUid? container = null;
                if (_itemSlots.TryGetSlot(ent.Owner, comp.ContainerSlotId, out var slot) && slot.Item != null)
                {
                    container = slot.Item;
                    args.PushMarkup(Loc.GetString("gas-recycler-container-loaded"));
                }
                else
                {
                    args.PushMarkup(Loc.GetString("gas-recycler-no-container"));
                }

                if (comp.Reacting)
                {
                    args.PushMarkup(Loc.GetString("gas-recycler-reacting"));
                }
                else
                {
                    var recipes = GetApplicableRecipes(comp);
                    var canReact = false;
                    foreach (var recipe in recipes)
                    {
                        if (inlet.Air.GetMoles(recipe.InputGas) > 0 &&
                            inlet.Air.Temperature >= recipe.MinimumTemperature &&
                            inlet.Air.Pressure >= recipe.MinimumPressure)
                        {
                            canReact = true;
                            break;
                        }
                    }

                    if (!canReact && recipes.Any())
                    {
                        var anyRecipe = recipes.First();
                        if (inlet.Air.Pressure < anyRecipe.MinimumPressure)
                        {
                            args.PushMarkup(Loc.GetString("gas-recycler-low-pressure"));
                        }

                        if (inlet.Air.Temperature < anyRecipe.MinimumTemperature)
                        {
                            args.PushMarkup(Loc.GetString("gas-recycler-low-temperature"));
                        }
                    }
                }
            }
        }

        private void OnUpdate(Entity<GasRecyclerComponent> ent, ref AtmosDeviceUpdateEvent args)
        {
            var comp = ent.Comp;
            if (!_nodeContainer.TryGetNodes(ent.Owner, comp.InletName, comp.OutletName, out PipeNode? inlet, out PipeNode? outlet))
            {
                _ambientSoundSystem.SetAmbience(ent, false);
                return;
            }

            var recipes = GetApplicableRecipes(comp);

            var canReact = false;
            foreach (var recipe in recipes)
            {
                if (recipe.Enabled &&
                    inlet.Air.Temperature >= recipe.MinimumTemperature &&
                    inlet.Air.Pressure >= recipe.MinimumPressure)
                {
                    canReact = true;
                    break;
                }
            }

            var removed = inlet.Air.RemoveVolume(PassiveTransferVol(inlet.Air, outlet.Air));

            comp.Reacting = false;

            if (canReact)
            {
                EntityUid? container = null;
                Entity<SolutionComponent>? containerSolution = null;

                if (_itemSlots.TryGetSlot(ent.Owner, comp.ContainerSlotId, out var slot))
                {
                    container = slot.Item;
                    if (container != null && _solutionContainer.TryGetFitsInDispenser(container.Value, out var solution, out _))
                    {
                        containerSolution = (container.Value, solution);
                    }
                }

                foreach (var recipe in recipes)
                {
                    if (!recipe.Enabled)
                        continue;

                    var inputMoles = removed.GetMoles(recipe.InputGas);
                    if (inputMoles <= 0)
                        continue;

                    // Perform the conversion
                    removed.AdjustMoles(recipe.InputGas, -inputMoles);
                    removed.AdjustMoles(recipe.OutputGas, inputMoles * recipe.ConversionRatio);
                    comp.Reacting = true;

                    // Collect scrubbed reagents if container is present
                    if (containerSolution != null && recipe.ScrubbedReagents.Count > 0)
                    {
                        foreach (var (reagentId, ratio) in recipe.ScrubbedReagents)
                        {
                            var reagentAmount = inputMoles * ratio;
                            if (reagentAmount > 0)
                            {
                                var solution = new Solution(reagentId, reagentAmount);
                                _solutionContainer.TryAddSolution(containerSolution.Value, solution);
                            }
                        }
                        _solutionContainer.UpdateChemicals(containerSolution.Value);
                    }
                }
            }

            _atmosphereSystem.Merge(outlet.Air, removed);
            UpdateAppearance(ent, comp);
            _ambientSoundSystem.SetAmbience(ent, comp.Reacting || removed.TotalMoles > 0);
        }

        public float PassiveTransferVol(GasMixture inlet, GasMixture outlet)
        {
            if (inlet.Pressure < outlet.Pressure)
            {
                return 0;
            }
            float overPressConst = 300; // pressure difference (in atm) to get 200 L/sec transfer rate
            float alpha = Atmospherics.MaxTransferRate * _atmosphereSystem.PumpSpeedup() / (float)Math.Sqrt(overPressConst * Atmospherics.OneAtmosphere);
            return alpha * (float)Math.Sqrt(inlet.Pressure - outlet.Pressure);
        }

        private void OnDisabled(EntityUid uid, GasRecyclerComponent comp, ref AtmosDeviceDisabledEvent args)
        {
            comp.Reacting = false;
            UpdateAppearance(uid, comp);
        }

        private void UpdateAppearance(EntityUid uid, GasRecyclerComponent? comp = null)
        {
            if (!Resolve(uid, ref comp, false))
                return;

            _appearance.SetData(uid, PumpVisuals.Enabled, comp.Reacting);
        }

        private IEnumerable<GasRecyclingRecipePrototype> GetApplicableRecipes(GasRecyclerComponent comp)
        {
            var allRecipes = _prototypeManager.EnumeratePrototypes<GasRecyclingRecipePrototype>();

            if (comp.EnabledRecipes.Count > 0)
            {
                // Return only specified recipes
                foreach (var recipeId in comp.EnabledRecipes)
                {
                    if (_prototypeManager.TryIndex<GasRecyclingRecipePrototype>(recipeId, out var recipe))
                        yield return recipe;
                }
            }
            else
            {
                // Return all enabled recipes
                foreach (var recipe in allRecipes)
                {
                    if (recipe.Enabled)
                        yield return recipe;
                }
            }
        }

        private void OnContainerRemoved(Entity<GasRecyclerComponent> ent, ref EntRemovedFromContainerMessage args)
        {
            if (TryComp<SolutionContainerManagerComponent>(args.Entity, out var solMgr))
            {
                foreach (var solutionName in solMgr.Containers)
                {
                    if (_solutionContainer.TryGetSolution((args.Entity, solMgr), solutionName, out var solutionEnt, out var solution))
                    {
                        Dirty(solutionEnt.Value);
                    }
                }
            }
        }
    }
}
