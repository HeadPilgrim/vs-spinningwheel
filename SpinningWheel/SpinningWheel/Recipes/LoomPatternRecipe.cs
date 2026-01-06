using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SpinningWheel.Recipes
{
    public class LoomPatternRecipe
    {
        public AssetLocation Code { get; set; }
        public bool Enabled { get; set; } = true;

        // Pattern definition (4 slots in 2x2 grid)
        public AssetLocation TopLeft { get; set; }
        public AssetLocation TopRight { get; set; }
        public AssetLocation BottomLeft { get; set; }
        public AssetLocation BottomRight { get; set; }

        // Input/output properties
        public int QuantityPerSlot { get; set; } = 2;
        public AssetLocation OutputType { get; set; }
        public int OutputQuantity { get; set; } = 1;

        /// <summary>
        /// Checks if the provided 4 item stacks match this pattern recipe
        /// </summary>
        public bool Matches(ItemStack topLeft, ItemStack topRight, ItemStack bottomLeft, ItemStack bottomRight, ICoreAPI api = null)
        {
            if (topLeft?.Collectible?.Code == null || topRight?.Collectible?.Code == null ||
                bottomLeft?.Collectible?.Code == null || bottomRight?.Collectible?.Code == null)
                return false;

            bool tlMatch = MatchesSlot(topLeft.Collectible.Code, TopLeft);
            bool trMatch = MatchesSlot(topRight.Collectible.Code, TopRight);
            bool blMatch = MatchesSlot(bottomLeft.Collectible.Code, BottomLeft);
            bool brMatch = MatchesSlot(bottomRight.Collectible.Code, BottomRight);

            bool matches = tlMatch && trMatch && blMatch && brMatch;

            api?.Logger.Notification($"[LoomRecipe] Testing {Code}:");
            api?.Logger.Notification($"  TL: {(tlMatch ? "✓" : "✗")} - Slot: {topLeft.Collectible.Code} | Recipe: {TopLeft}");
            api?.Logger.Notification($"  TR: {(trMatch ? "✓" : "✗")} - Slot: {topRight.Collectible.Code} | Recipe: {TopRight}");
            api?.Logger.Notification($"  BL: {(blMatch ? "✓" : "✗")} - Slot: {bottomLeft.Collectible.Code} | Recipe: {BottomLeft}");
            api?.Logger.Notification($"  BR: {(brMatch ? "✓" : "✗")} - Slot: {bottomRight.Collectible.Code} | Recipe: {BottomRight}");
            api?.Logger.Notification($"  Result: {(matches ? "MATCH" : "NO MATCH")}");

            return matches;
        }

        /// <summary>
        /// Checks if an actual item code matches a pattern slot code
        /// </summary>
        private bool MatchesSlot(AssetLocation actual, AssetLocation pattern)
        {
            // Exact match
            if (pattern.Equals(actual)) return true;

            // Wildcard support (future enhancement)
            // For now, require exact matches
            return false;
        }

        /// <summary>
        /// Gets the output item stack for this recipe
        /// </summary>
        public ItemStack GetOutput(ICoreAPI api)
        {
            if (OutputType == null) return null;

            // Try to get as item first
            Item item = api.World.GetItem(OutputType);
            if (item != null)
            {
                return new ItemStack(item, OutputQuantity);
            }

            // Try to get as block
            Block block = api.World.GetBlock(OutputType);
            if (block != null)
            {
                return new ItemStack(block, OutputQuantity);
            }

            api.Logger.Error($"[SpinningWheel] Pattern recipe output not found: {OutputType}");
            return null;
        }

        /// <summary>
        /// Checks if all 4 input slots have enough quantity to weave
        /// </summary>
        public bool HasSufficientInput(ItemStack topLeft, ItemStack topRight, ItemStack bottomLeft, ItemStack bottomRight)
        {
            return topLeft?.StackSize >= QuantityPerSlot &&
                   topRight?.StackSize >= QuantityPerSlot &&
                   bottomLeft?.StackSize >= QuantityPerSlot &&
                   bottomRight?.StackSize >= QuantityPerSlot;
        }
    }
}
