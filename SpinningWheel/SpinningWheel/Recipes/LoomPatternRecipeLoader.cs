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

        public void LoadPatternRecipes()
        {
            var assets = api.Assets.GetMany("spinningwheel:recipes/loompatterns/").ToList();

            if (assets.Count == 0)
            {
                api.Logger.Debug("[Immersive Fibercraft] No assets found with 'spinningwheel:recipes/loompatterns/', trying alternative paths");
                assets = api.Assets.GetMany("recipes/loompatterns/", "spinningwheel").ToList();
            }

            if (assets.Count == 0)
            {
                api.Logger.Debug("[Immersive Fibercraft] No assets found with alternative path, trying without trailing slash");
                assets = api.Assets.GetMany("spinningwheel:recipes/loompatterns").ToList();
            }

            if (assets.Count == 0)
            {
                api.Logger.Debug("[Immersive Fibercraft] No pattern recipe assets found (this is normal on client side)");
                return;
            }

            api.Logger.Notification($"[Immersive Fibercraft] Found {assets.Count} asset files in recipes/loompatterns");

            foreach (var asset in assets)
            {
                try
                {
                    LoadRecipesFromAsset(asset);
                }
                catch (Exception ex)
                {
                    api.Logger.Error($"[Immersive Fibercraft] Failed to load pattern recipe from {asset.Name}: {ex.Message}");
                    api.Logger.Error(ex.StackTrace);
                }
            }

            api.Logger.Notification($"[Immersive Fibercraft] Loaded {PatternRecipes.Count} pattern recipes");
        }

        private void LoadRecipesFromAsset(IAsset asset)
        {
            // Try templated format first
            var templated = asset.ToObject<TemplatedRecipeJson>();

            if (templated?.template != null && templated.variants != null)
            {
                // Expand each variant into its own LoomPatternRecipe
                foreach (var variant in templated.variants)
                {
                    var recipe = BuildFromTemplate(templated, variant);
                    if (recipe == null) continue;

                    if (recipe.Enabled)
                    {
                        PatternRecipes.Add(recipe);
                        api.Logger.VerboseDebug($"[Immersive Fibercraft] Loaded templated recipe: {recipe.Code}");
                    }
                    else
                    {
                        api.Logger.VerboseDebug($"[Immersive Fibercraft] Skipped disabled templated recipe: {recipe.Code}");
                    }
                }
                return;
            }

            // Fall back to original single-recipe format
            var json = asset.ToObject<PatternRecipeJson>();
            if (json == null)
            {
                api.Logger.Warning($"[Immersive Fibercraft] Failed to parse pattern recipe: {asset.Name}");
                return;
            }

            var singleRecipe = BuildFromSingle(json);
            if (singleRecipe == null) return;

            if (singleRecipe.Enabled)
            {
                PatternRecipes.Add(singleRecipe);
                api.Logger.VerboseDebug($"[Immersive Fibercraft] Loaded pattern recipe: {singleRecipe.Code}");
            }
            else
            {
                api.Logger.VerboseDebug($"[Immersive Fibercraft] Skipped disabled pattern recipe: {singleRecipe.Code}");
            }
        }

        /// <summary>
        /// Builds a recipe from a templated JSON file + one variant entry.
        /// Currently supports the "checkered" template (primary/secondary alternating diagonals).
        /// Add more template types here as needed.
        /// </summary>
        private LoomPatternRecipe BuildFromTemplate(TemplatedRecipeJson json, VariantJson variant)
        {
            if (json.template != "checkered")
            {
                api.Logger.Warning($"[Immersive Fibercraft] Unknown recipe template '{json.template}' in {json.code} — skipping variant '{variant.id ?? variant.name}'");
                return null;
            }

            // Checkered: primary on diagonal (TL, BR), secondary on anti-diagonal (TR, BL)
            string outputType = json.output.typePattern.Replace("{variant}", variant.name);

            return new LoomPatternRecipe
            {
                Code        = new AssetLocation($"{json.code}-{variant.id ?? variant.name}"),
                Enabled     = json.enabled,
                TopLeft     = new AssetLocation(variant.primary),
                TopRight    = new AssetLocation(variant.secondary),
                BottomLeft  = new AssetLocation(variant.secondary),
                BottomRight = new AssetLocation(variant.primary),
                QuantityPerSlot = json.input.quantityPerSlot,
                OutputType  = new AssetLocation(outputType),
                OutputQuantity = json.output.quantity
            };
        }

        private LoomPatternRecipe BuildFromSingle(PatternRecipeJson json)
        {
            return new LoomPatternRecipe
            {
                Code        = new AssetLocation(json.code),
                Enabled     = json.enabled,
                TopLeft     = new AssetLocation(json.pattern.topLeft),
                TopRight    = new AssetLocation(json.pattern.topRight),
                BottomLeft  = new AssetLocation(json.pattern.bottomLeft),
                BottomRight = new AssetLocation(json.pattern.bottomRight),
                QuantityPerSlot = json.input.quantityPerSlot,
                OutputType  = new AssetLocation(json.output.type),
                OutputQuantity = json.output.quantity
            };
        }

        public LoomPatternRecipe FindMatchingRecipe(ItemStack topLeft, ItemStack topRight, ItemStack bottomLeft, ItemStack bottomRight)
        {
            foreach (var recipe in PatternRecipes)
            {
                if (recipe.Matches(topLeft, topRight, bottomLeft, bottomRight, api))
                    return recipe;
            }
            return null;
        }
    }

    // ── Templated format ─────────────────────────────────────────────────────

    internal class TemplatedRecipeJson
    {
        public string code { get; set; }
        public bool enabled { get; set; } = true;
        public string template { get; set; }           // e.g. "checkered"
        public InputJson input { get; set; }
        public TemplatedOutputJson output { get; set; }
        public List<VariantJson> variants { get; set; }
    }

    internal class TemplatedOutputJson
    {
        public string typePattern { get; set; }        // e.g. "tailorsdelight:checkeredcloth-{variant}"
        public int quantity { get; set; } = 1;
    }

    internal class VariantJson
    {
        public string name { get; set; }       // substituted into typePattern for output item
        public string id { get; set; }         // used for recipe Code, falls back to name if absent
        public string primary { get; set; }
        public string secondary { get; set; }
    }

    // ── Original single-recipe format (kept for backward compat) ─────────────

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