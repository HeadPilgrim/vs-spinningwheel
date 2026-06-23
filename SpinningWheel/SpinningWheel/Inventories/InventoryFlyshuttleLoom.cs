using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using SpinningWheel.BlockEntities;

namespace SpinningWheel.Inventories;

#nullable disable



public class InventoryFlyshuttleLoom: InventoryBase, ISlotProvider
{
    
    public WeavingMode CurrentMode { get; set; } = WeavingMode.Normal;
    public bool IgnoreSuitability { get; set; } = false;
    
    ItemSlot[] slots;
    public ItemSlot[] Slots { get { return slots; } }

    public InventoryFlyshuttleLoom(string inventoryID, ICoreAPI api) : base(inventoryID, api)
    {
        // slots 0-2 = normal input (twine/thread) - 3 slots to allow up to 96 twine (32x3)
        // slot 3 = output (cloth/fabric) - shared between normal and pattern weaving
        // slots 4-7 = pattern grid (2x2: top-left, top-right, bottom-left, bottom-right)
        slots = GenEmptySlots(8);
    }

    public InventoryFlyshuttleLoom(string className, string instanceID, ICoreAPI api) : base(className, instanceID, api)
    {
        slots = GenEmptySlots(8);
    }

    public override int Count
    {
        get { return 8; }
    }

    public override ItemSlot this[int slotId]
    {
        get
        {
            if (slotId < 0 || slotId >= Count) return null;
            return slots[slotId];
        }
        set
        {
            if (slotId < 0 || slotId >= Count) throw new ArgumentOutOfRangeException(nameof(slotId));
            if (value == null) throw new ArgumentNullException(nameof(value));
            slots[slotId] = value;
        }
    }

    public override void FromTreeAttributes(ITreeAttribute tree)
    {
        slots = SlotsFromTreeAttributes(tree, slots);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        SlotsToTreeAttributes(slots, tree);
    }

    protected override ItemSlot NewSlot(int i)
    {
        // Slots 0-2 = normal input (only accepts weavable items)
        // Slot 3 = output (read-only, shared between modes)
        // Slots 4-7 = pattern grid (accepts vanilla flaxtwine and colored twine)
        if (i >= 0 && i <= 2)
        {
            return new ItemSlotWeavingInput(this);
        }
        else if (i == 3)
        {
            return new ItemSlotOutput(this);
        }
        else if (i >= 4 && i <= 7)
        {
            return new ItemSlotPatternInput(this);
        }
        return new ItemSlot(this);
    }
    
    public override WeightedSlot GetBestSuitedSlot(ItemSlot sourceSlot, ItemStackMoveOperation op = null, List<ItemSlot> skipSlots = null)
    {
        if (sourceSlot?.Inventory == this)
            return base.GetBestSuitedSlot(sourceSlot, op, skipSlots);

        string domain = sourceSlot?.Itemstack?.Collectible?.Code?.Domain;
        string path = sourceSlot?.Itemstack?.Collectible?.Code?.Path;

        bool isTwine = sourceSlot?.Itemstack != null && (
            (domain == "game" && path == "flaxtwine") ||
            (domain == "tailorsdelight" && path?.StartsWith("twine-") == true) ||
            (domain == "wool" && path?.StartsWith("twine-wool-") == true));

        bool isWeavable = sourceSlot?.Itemstack?.ItemAttributes?.KeyExists("weavingProps") == true;

        WeightedSlot best = new WeightedSlot();

        if (CurrentMode == WeavingMode.Normal && isWeavable)
        {
            for (int i = 0; i <= 2; i++)
            {
                if (skipSlots != null && skipSlots.Contains(slots[i])) continue;
                float suitability = GetSuitability(sourceSlot, slots[i], op?.CurrentPriority == EnumMergePriority.AutoMerge);
                if (suitability > best.weight)
                {
                    best.slot = slots[i];
                    best.weight = suitability;
                }
            }
        }
        else if (CurrentMode == WeavingMode.Pattern && isTwine)
        {
            for (int i = 4; i <= 7; i++)
            {
                if (skipSlots != null && skipSlots.Contains(slots[i])) continue;
                float suitability = GetSuitability(sourceSlot, slots[i], op?.CurrentPriority == EnumMergePriority.AutoMerge);
                if (suitability > best.weight)
                {
                    best.slot = slots[i];
                    best.weight = suitability;
                }
            }
        }

        return best;
    }
    public override float GetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
    {
        // Never gate items already in this inventory
        if (sourceSlot?.Inventory == this)
            return base.GetSuitability(sourceSlot, targetSlot, isMerge);

        // Never gate if target slot already has an item
        if (targetSlot?.Itemstack != null)
            return base.GetSuitability(sourceSlot, targetSlot, isMerge);

        string domain = sourceSlot?.Itemstack?.Collectible?.Code?.Domain;
        string path = sourceSlot?.Itemstack?.Collectible?.Code?.Path;

        bool isTwine = sourceSlot?.Itemstack != null && (
            (domain == "game" && path == "flaxtwine") ||
            (domain == "tailorsdelight" && path?.StartsWith("twine-") == true) ||
            (domain == "wool" && path?.StartsWith("twine-wool-") == true));

        bool isWeavable = sourceSlot?.Itemstack?.ItemAttributes?.KeyExists("weavingProps") == true;

        if (targetSlot == slots[0] || targetSlot == slots[1] || targetSlot == slots[2])
        {
            if (CurrentMode == WeavingMode.Normal && isWeavable)
                return 4f;
            return 0f;
        }

        if (targetSlot == slots[4] || targetSlot == slots[5] ||
            targetSlot == slots[6] || targetSlot == slots[7])
        {
            if (CurrentMode == WeavingMode.Pattern && isTwine)
                return 4f;
            return 0f;
        }

        if (targetSlot == slots[3])
            return 0f;

        return base.GetSuitability(sourceSlot, targetSlot, isMerge);
    }

    public override ItemSlot GetAutoPushIntoSlot(BlockFacing atBlockFace, ItemSlot fromSlot)
    {
        if (CurrentMode == WeavingMode.Normal)
        {
            // Push into input slots 0-2 for weavable items
            if (fromSlot?.Itemstack?.ItemAttributes?.KeyExists("weavingProps") == true)
            {
                for (int i = 0; i <= 2; i++)
                {
                    if (slots[i].Empty || slots[i].CanTakeFrom(fromSlot))
                        return slots[i];
                }
            }
        }
        else if (CurrentMode == WeavingMode.Pattern)
        {
            // Push into pattern slots 4-7 for twine
            string domain = fromSlot?.Itemstack?.Collectible?.Code?.Domain;
            string path = fromSlot?.Itemstack?.Collectible?.Code?.Path;

            bool isTwine = fromSlot?.Itemstack != null && (
                (domain == "game" && path == "flaxtwine") ||
                (domain == "tailorsdelight" && path?.StartsWith("twine-") == true) ||
                (domain == "wool" && path?.StartsWith("twine-wool-") == true));

            if (isTwine)
            {
                for (int i = 4; i <= 7; i++)
                {
                    if (slots[i].Empty || slots[i].CanTakeFrom(fromSlot))
                        return slots[i];
                }
            }
        }

        return null;
    }
}

// Custom slot for input that only accepts weavable items
public class ItemSlotWeavingInput : ItemSlotSurvival
{
    public ItemSlotWeavingInput(InventoryBase inventory) : base(inventory)
    {
    }

    public override bool CanHold(ItemSlot sourceSlot)
    {
        bool result = sourceSlot?.Itemstack?.ItemAttributes?.KeyExists("weavingProps") == true
            ? base.CanHold(sourceSlot)
            : false;
        return result;
    }

    public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
    {
        bool result = sourceSlot?.Itemstack?.ItemAttributes?.KeyExists("weavingProps") == true
            ? base.CanTakeFrom(sourceSlot, priority)
            : false;
        return result;
    }

    public override int GetRemainingSlotSpace(ItemStack forItemstack)
    {
        // Reject items without weavingProps
        if (forItemstack?.ItemAttributes?.KeyExists("weavingProps") != true)
        {
            return 0; // No space for non-weavable items
        }

        return base.GetRemainingSlotSpace(forItemstack);
    }
}

// Custom slot for pattern inputs - accepts vanilla flaxtwine and colored twine from tailorsdelight or wool mods
public class ItemSlotPatternInput : ItemSlotSurvival
{
    public ItemSlotPatternInput(InventoryBase inventory) : base(inventory)
    {
    }

    public override bool CanHold(ItemSlot sourceSlot)
    {
        if (sourceSlot?.Itemstack != null)
        {
            string domain = sourceSlot.Itemstack.Collectible.Code.Domain;
            string path = sourceSlot.Itemstack.Collectible.Code.Path;

            // Accept vanilla flaxtwine, tailorsdelight:twine-{color}, or wool:twine-wool-{color}
            if ((domain == "game" && path == "flaxtwine") ||
                (domain == "tailorsdelight" && path.StartsWith("twine-")) ||
                (domain == "wool" && path.StartsWith("twine-wool-")))
            {
                return base.CanHold(sourceSlot);
            }
        }
        return false;
    }

    public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
    {
        if (sourceSlot?.Itemstack != null)
        {
            string domain = sourceSlot.Itemstack.Collectible.Code.Domain;
            string path = sourceSlot.Itemstack.Collectible.Code.Path;

            if ((domain == "game" && path == "flaxtwine") ||
                (domain == "tailorsdelight" && path.StartsWith("twine-")) ||
                (domain == "wool" && path.StartsWith("twine-wool-")))
            {
                return base.CanTakeFrom(sourceSlot, priority);
            }
        }
        return false;
    }

    public override int GetRemainingSlotSpace(ItemStack forItemstack)
    {
        // Reject items that aren't twine (vanilla or colored)
        if (forItemstack != null)
        {
            string domain = forItemstack.Collectible.Code.Domain;
            string path = forItemstack.Collectible.Code.Path;

            if ((domain == "game" && path == "flaxtwine") ||
                (domain == "tailorsdelight" && path.StartsWith("twine-")) ||
                (domain == "wool" && path.StartsWith("twine-wool-")))
            {
                return base.GetRemainingSlotSpace(forItemstack);
            }
        }
        return 0; // No space for non-twine items
    }
}

// Note: ItemSlotOutput is already defined in InventorySpinningWheel.cs and can be reused
