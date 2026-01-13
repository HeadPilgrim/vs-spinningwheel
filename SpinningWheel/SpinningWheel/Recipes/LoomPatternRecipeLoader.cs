using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace SpinningWheel.Recipes
{
    public class LoomPatternRecipeLoader
    {
        private ICoreAPI api;
        public List<LoomPatternRecipe> PatternRecipes { get; private set; } = new List<LoomPatternRecipe>();

        public LoomPatternRecipeLoader(ICoreAPI api)
        {
            this.api = api;
        }

        /// <summary>
        /// Loads all pattern recipes from assets/spinningwheel/recipes/loompatterns/
        /// </summary>
        public void LoadPatternRecipes()
        {
            // Try different path variations to find the assets
            // Use Debug level for fallback attempts to avoid confusing warnings in logs
            var assets = api.Assets.GetMany("spinningwheel:recipes/loompatterns/").ToList();

            if (assets.Count == 0)
            {
                api.Logger.Debug("[SpinningWheel] No assets found with 'spinningwheel:recipes/loompatterns/', trying alternative paths");
                assets = api.Assets.GetMany("recipes/loompatterns/", "spinningwheel").ToList();
            }

            if (assets.Count == 0)
            {
                api.Logger.Debug("[SpinningWheel] No assets found with alternative path, trying without trailing slash");
                assets = api.Assets.GetMany("spinningwheel:recipes/loompatterns").ToList();
            }

            if (assets.Count == 0)
            {
                // Only notify if no recipes found after all attempts - this is expected on client side
                // since pattern recipes are only needed server-side for crafting validation
                api.Logger.Debug("[SpinningWheel] No pattern recipe assets found (this is normal on client side)");
                return;
            }

            api.Logger.Notification($"[SpinningWheel] Found {assets.Count} asset files in recipes/loompatterns");

            foreach (var asset in assets)
            {
                try
                {
                    // Parse JSON asset
                    var json = asset.ToObject<PatternRecipeJson>();

                    if (json == null)
                    {
                        api.Logger.Warning($"[SpinningWheel] Failed to parse pattern recipe: {asset.Name}");
                        continue;
                    }

                    // Convert to LoomPatternRecipe
                    var recipe = new LoomPatternRecipe
                    {
                        Code = new AssetLocation(json.code),
                        Enabled = json.enabled,
                        TopLeft = new AssetLocation(json.pattern.topLeft),
                        TopRight = new AssetLocation(json.pattern.topRight),
                        BottomLeft = new AssetLocation(json.pattern.bottomLeft),
                        BottomRight = new AssetLocation(json.pattern.bottomRight),
                        QuantityPerSlot = json.input.quantityPerSlot,
                        OutputType = new AssetLocation(json.output.type),
                        OutputQuantity = json.output.quantity
                    };

                    if (recipe.Enabled)
                    {
                        PatternRecipes.Add(recipe);
                        api.Logger.VerboseDebug($"[SpinningWheel] Loaded pattern recipe: {recipe.Code}");
                    }
                    else
                    {
                        api.Logger.VerboseDebug($"[SpinningWheel] Skipped disabled pattern recipe: {recipe.Code}");
                    }
                }
                catch (Exception ex)
                {
                    api.Logger.Error($"[SpinningWheel] Failed to load pattern recipe from {asset.Name}: {ex.Message}");
                    api.Logger.Error(ex.StackTrace);
                }
            }

            api.Logger.Notification($"[SpinningWheel] Loaded {PatternRecipes.Count} pattern recipes");
        }

        /// <summary>
        /// Finds a recipe that matches the given 4 item stacks
        /// </summary>
        public LoomPatternRecipe FindMatchingRecipe(ItemStack topLeft, ItemStack topRight, ItemStack bottomLeft, ItemStack bottomRight)
        {
            api.Logger.Notification($"[LoomRecipeLoader] Searching {PatternRecipes.Count} recipes for match");

            foreach (var recipe in PatternRecipes)
            {
                if (recipe.Matches(topLeft, topRight, bottomLeft, bottomRight, api))
                {
                    api.Logger.Notification($"[LoomRecipeLoader] Match found: {recipe.Code}");
                    return recipe;
                }
            }

            api.Logger.Notification("[LoomRecipeLoader] No match found");
            return null;
        }
    }

    // JSON parsing helper classes
    internal class PatternRecipeJson
    {
        public string code { get; set; }
        public bool enabled { get; set; } = true;
        public PatternJson pattern { get; set; }
        public InputJson input { get; set; }
        public OutputJson output { get; set; }
    }

    internal class PatternJson
    {
        public string topLeft { get; set; }
        public string topRight { get; set; }
        public string bottomLeft { get; set; }
        public string bottomRight { get; set; }
    }

    internal class InputJson
    {
        public int quantityPerSlot { get; set; } = 2;
    }

    internal class OutputJson
    {
        public string type { get; set; }
        public int quantity { get; set; } = 1;
    }
}
