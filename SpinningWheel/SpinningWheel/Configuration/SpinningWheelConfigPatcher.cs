using System.Linq;
using SpinningWheel.ModConfig;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace SpinningWheel.Configuration
{
    /// <summary>
    /// Handles runtime patching of recipes and spinning properties based on config settings
    /// </summary>
    public class SpinningWheelConfigPatcher
    {
        private readonly ICoreAPI api;
        private readonly ModConfig.ModConfig config;

        public SpinningWheelConfigPatcher(ICoreAPI api, ModConfig.ModConfig config)
        {
            this.api = api;
            this.config = config;
        }

        /// <summary>
        /// Applies all config-based patches to recipes and items
        /// </summary>
        public void ApplyAllPatches()
        {
            // Disable twine recipes if configured
            if (config.DisableTwineGridRecipes)
            {
                DisableTwineRecipes();
            }

            // Patch spinning properties based on config
            PatchSpinningProperties();

            // Patch weaving properties based on config
            PatchWeavingProperties();
        }

        /// <summary>
        /// Disables twine grid recipes based on config setting
        /// </summary>
        private void DisableTwineRecipes()
        {
            int disabledCount = 0;
            int recipeCount = api.World.GridRecipes.Count;

            // Skip if no recipes available yet - this can happen in early lifecycle stages
            if (recipeCount == 0)
            {
                api.Logger.Debug("[SpinningWheel] GridRecipes not yet loaded, skipping recipe disabling");
                return;
            }

            api.Logger.Debug($"[SpinningWheel] Scanning {recipeCount} grid recipes for twine recipes to disable");

            // Iterate through all recipes and check their output
            foreach (var recipe in api.World.GridRecipes)
            {
                if (recipe?.Output?.ResolvedItemstack == null) continue;
                
                string outputCode = recipe.Output.ResolvedItemstack.Collectible.Code.Path;
                string outputDomain = recipe.Output.ResolvedItemstack.Collectible.Code.Domain;
                string recipePath = recipe.Name.Path;
                string recipeDomain = recipe.Name.Domain;
                bool shouldDisable = false;
                
                // Check if output is flax twine (vanilla)
                if (outputCode == "flaxtwine" && outputDomain == "game" && recipeDomain == "game")
                {
                    shouldDisable = true;
                    api.Logger.Notification($"[SpinningWheel] Found vanilla flax twine recipe: {recipe.Name} -> {recipe.Output.ResolvedItemstack.GetName()}");
                }
                // Check if output is wool twine (from Wool mod) - only from the twine.json file
                else if (outputCode.StartsWith("twine-") && outputDomain == "wool" && 
                         recipeDomain == "wool" && recipePath.Contains("recipes/grid/twine"))
                {
                    shouldDisable = true;
                    api.Logger.Notification($"[SpinningWheel] Found wool twine recipe: {recipe.Name} -> {recipe.Output.ResolvedItemstack.GetName()}");
                }
                // Check if output is flax twine from Floral Zones Caribbean mod (cotton -> twine)
                else if (outputCode == "flaxtwine" && outputDomain == "game" && 
                         recipeDomain == "floralzonescaribbeanregion" && recipePath.Contains("recipes/grid/twine"))
                {
                    shouldDisable = true;
                    api.Logger.Notification($"[SpinningWheel] Found Floral Zones cotton twine recipe: {recipe.Name} -> {recipe.Output.ResolvedItemstack.GetName()}");
                }
                // Check if output is flax twine from pemmican mod (papyrus -> twine, algae -> twine)
                else if (outputCode == "flaxtwine" && outputDomain == "game" && 
                         recipeDomain == "pemmican" && 
                         (recipePath.Contains("papyrus") || recipePath.Contains("algae")))
                {
                    shouldDisable = true;
                    api.Logger.Notification($"[SpinningWheel] Found pemmican twine recipe: {recipe.Name} -> {recipe.Output.ResolvedItemstack.GetName()}");
                }
                
                if (shouldDisable)
                {
                    recipe.Enabled = false;
                    recipe.ShowInCreatedBy = false;
                    disabledCount++;
                }
            }

            if (disabledCount > 0)
            {
                api.Logger.Notification($"[SpinningWheel] Successfully disabled {disabledCount} twine recipes");
            }
            else
            {
                // This is not necessarily an error - the mods that provide these recipes may not be installed
                api.Logger.Debug("[SpinningWheel] No twine recipes found to disable (expected if related mods are not installed)");
            }
        }

        /// <summary>
        /// Patches spinning properties for all spinnable items based on config values
        /// </summary>
        private void PatchSpinningProperties()
        {
            int patchedCount = 0;
            int totalSpinnable = 0;

            // Skip if collectibles aren't loaded yet
            if (api.World.Collectibles == null || !api.World.Collectibles.Any())
            {
                api.Logger.Debug("[SpinningWheel] Collectibles not yet loaded, skipping spinning property patching");
                return;
            }

            foreach (var collectible in api.World.Collectibles)
            {
                if (collectible?.Attributes == null) continue;
                if (!collectible.Attributes.KeyExists("spinningProps")) continue;

                totalSpinnable++;
                var spinningPropsToken = collectible.Attributes["spinningProps"];
                string code = collectible.Code.Path;
                string domain = collectible.Code.Domain;

                // Apply patches based on item type
                if (TryPatchFlaxFibers(spinningPropsToken, code, domain) ||
                    TryPatchCotton(spinningPropsToken, code, domain) ||
                    TryPatchWoolFibers(spinningPropsToken, code, domain) ||
                    TryPatchWoolTwine(spinningPropsToken, code, domain) ||
                    TryPatchPapyrus(spinningPropsToken, code, domain) ||
                    TryPatchAlgae(spinningPropsToken, code, domain))
                {
                    patchedCount++;
                }
            }

            api.Logger.Notification($"[SpinningWheel] Found {totalSpinnable} total spinnable items");
            if (patchedCount > 0)
            {
                api.Logger.Notification($"[SpinningWheel] Patched spinning properties for {patchedCount} items based on config");
            }
            else if (totalSpinnable > 0)
            {
                // Only warn if there are spinnable items but none were patched (unexpected)
                api.Logger.Debug("[SpinningWheel] Found spinnable items but none matched config patterns");
            }
        }

        #region Individual Item Patchers

        private bool TryPatchFlaxFibers(JsonObject spinningPropsToken, string code, string domain)
        {
            if (code == "flaxfibers" && domain == "game")
            {
                spinningPropsToken.Token["spinTime"] = config.FlaxSpinTime;
                spinningPropsToken.Token["inputQuantity"] = config.FlaxInputQuantity;
                spinningPropsToken.Token["outputQuantity"] = config.FlaxOutputQuantity;
                return true;
            }
            return false;
        }

        private bool TryPatchCotton(JsonObject spinningPropsToken, string code, string domain)
        {
            // Log all cotton items we encounter for debugging
            if (code.Contains("cotton"))
            {
                api.Logger.Debug($"[SpinningWheel] Found cotton-related item: {domain}:{code}");
            }
            
            // Match any cotton fiber from floralzonescaribbeanregion
            if (code.StartsWith("cotton-") && code.Contains("-fiber") && domain == "floralzonescaribbeanregion")
            {
                spinningPropsToken.Token["spinTime"] = config.CottonSpinTime;
                spinningPropsToken.Token["inputQuantity"] = config.CottonInputQuantity;
                spinningPropsToken.Token["outputQuantity"] = config.CottonOutputQuantity;
                api.Logger.Notification($"[SpinningWheel] Patched cotton spinning properties: {domain}:{code}");
                return true;
            }
            return false;
        }

        private bool TryPatchWoolFibers(JsonObject spinningPropsToken, string code, string domain)
        {
            if (code.StartsWith("fibers-") && domain == "wool")
            {
                spinningPropsToken.Token["spinTime"] = config.WoolFiberSpinTime;
                spinningPropsToken.Token["inputQuantity"] = config.WoolFiberInputQuantity;
                spinningPropsToken.Token["outputQuantity"] = config.WoolFiberOutputQuantity;
                return true;
            }
            return false;
        }

        private bool TryPatchWoolTwine(JsonObject spinningPropsToken, string code, string domain)
        {
            if (code.StartsWith("twine-wool-") && domain == "wool")
            {
                spinningPropsToken.Token["spinTime"] = config.WoolTwineSpinTime;
                spinningPropsToken.Token["inputQuantity"] = config.WoolTwineInputQuantity;
                spinningPropsToken.Token["outputQuantity"] = config.WoolTwineOutputQuantity;
                return true;
            }
            return false;
        }

        private bool TryPatchPapyrus(JsonObject spinningPropsToken, string code, string domain)
        {
            if (code == "papyrus-fiber" && domain == "pemmican")
            {
                spinningPropsToken.Token["spinTime"] = config.PapyrusSpinTime;
                spinningPropsToken.Token["inputQuantity"] = config.PapyrusInputQuantity;
                spinningPropsToken.Token["outputQuantity"] = config.PapyrusOutputQuantity;
                return true;
            }
            return false;
        }

        private bool TryPatchAlgae(JsonObject spinningPropsToken, string code, string domain)
        {
            if (code == "algae-chem" && domain == "pemmican")
            {
                spinningPropsToken.Token["spinTime"] = config.AlgaeSpinTime;
                spinningPropsToken.Token["inputQuantity"] = config.AlgaeInputQuantity;
                spinningPropsToken.Token["outputQuantity"] = config.AlgaeOutputQuantity;
                return true;
            }
            return false;
        }

        #endregion

        #region Weaving Properties Patching

        /// <summary>
        /// Patches weaving properties for all weavable items based on config values
        /// </summary>
        private void PatchWeavingProperties()
        {
            int patchedCount = 0;
            int totalWeavable = 0;

            // Skip if collectibles aren't loaded yet
            if (api.World.Collectibles == null || !api.World.Collectibles.Any())
            {
                api.Logger.Debug("[SpinningWheel] Collectibles not yet loaded, skipping weaving property patching");
                return;
            }

            foreach (var collectible in api.World.Collectibles)
            {
                if (collectible?.Attributes == null) continue;
                if (!collectible.Attributes.KeyExists("weavingProps")) continue;

                totalWeavable++;
                var weavingPropsToken = collectible.Attributes["weavingProps"];
                string code = collectible.Code.Path;
                string domain = collectible.Code.Domain;

                // Apply patches based on item type
                if (TryPatchFlaxTwineWeaving(weavingPropsToken, code, domain) ||
                    TryPatchWoolTwineWeaving(weavingPropsToken, code, domain) ||
                    TryPatchTailorsDelightThreadWeaving(weavingPropsToken, code, domain))
                {
                    patchedCount++;
                }
            }

            api.Logger.Notification($"[SpinningWheel] Found {totalWeavable} total weavable items");
            if (patchedCount > 0)
            {
                api.Logger.Notification($"[SpinningWheel] Patched weaving properties for {patchedCount} items based on config");
            }
        }

        private bool TryPatchFlaxTwineWeaving(JsonObject weavingPropsToken, string code, string domain)
        {
            // Match vanilla flax twine (flaxtwine)
            if (code == "flaxtwine" && domain == "game")
            {
                weavingPropsToken.Token["inputQuantity"] = config.FlaxTwineWeaveInputQuantity;
                weavingPropsToken.Token["outputQuantity"] = config.FlaxTwineWeaveOutputQuantity;
                return true;
            }
            return false;
        }

        private bool TryPatchWoolTwineWeaving(JsonObject weavingPropsToken, string code, string domain)
        {
            // Match wool twine from Wool & More mod (twine-wool-*)
            if (code.StartsWith("twine-wool-") && domain == "wool")
            {
                weavingPropsToken.Token["inputQuantity"] = config.WoolTwineWeaveInputQuantity;
                weavingPropsToken.Token["outputQuantity"] = config.WoolTwineWeaveOutputQuantity;
                return true;
            }
            return false;
        }

        private bool TryPatchTailorsDelightThreadWeaving(JsonObject weavingPropsToken, string code, string domain)
        {
            // Match twine/thread from Tailor's Delight mod (twine-*)
            if (code.StartsWith("twine-") && domain == "tailorsdelight")
            {
                weavingPropsToken.Token["inputQuantity"] = config.TailorsDelightThreadWeaveInputQuantity;
                weavingPropsToken.Token["outputQuantity"] = config.TailorsDelightThreadWeaveOutputQuantity;
                return true;
            }
            return false;
        }

        #endregion
    }
}