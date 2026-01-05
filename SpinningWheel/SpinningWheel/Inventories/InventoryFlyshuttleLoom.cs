using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace SpinningWheel.Inventories;

#nullable disable

public class InventoryFlyshuttleLoom: InventoryBase, ISlotProvider
{
    ItemSlot[] slots;
    public ItemSlot[] Slots { get { return slots; } }

    public InventoryFlyshuttleLoom(string inventoryID, ICoreAPI api) : base(inventoryID, api)
    {
        // slots 0-2 = input (twine/thread) - 3 slots to allow up to 96 twine (32x3)
        // slot 3 = output (cloth/fabric)
        slots = GenEmptySlots(4);
    }

    public InventoryFlyshuttleLoom(string className, string instanceID, ICoreAPI api) : base(className, instanceID, api)
    {
        slots = GenEmptySlots(4);
    }

    public override int Count
    {
        get { return 4; }
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
        // Slots 0-2 = input (only accepts weavable items)
        // Slot 3 = output (read-only)
        if (i >= 0 && i <= 2)
        {
            return new ItemSlotWeavingInput(this);
        }
        else if (i == 3)
        {
            return new ItemSlotOutput(this);
        }
        return new ItemSlot(this);
    }

    public override float GetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
    {
        // Accept weavable items in any of the 3 input slots
        if ((targetSlot == slots[0] || targetSlot == slots[1] || targetSlot == slots[2]) &&
            sourceSlot?.Itemstack?.ItemAttributes?.KeyExists("weavingProps") == true)
        {
            return 4f;
        }

        return base.GetSuitability(sourceSlot, targetSlot, isMerge);
    }

    public override ItemSlot GetAutoPushIntoSlot(BlockFacing atBlockFace, ItemSlot fromSlot)
    {
        // Try to push into input slots in order (0, 1, 2)
        // Return the first slot that can accept the item
        for (int i = 0; i <= 2; i++)
        {
            if (slots[i].Empty || slots[i].CanTakeFrom(fromSlot))
            {
                return slots[i];
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
        if (sourceSlot?.Itemstack?.ItemAttributes?.KeyExists("weavingProps") == true)
        {
            return base.CanHold(sourceSlot);
        }
        return false;
    }

    public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
    {
        if (sourceSlot?.Itemstack?.ItemAttributes?.KeyExists("weavingProps") == true)
        {
            return base.CanTakeFrom(sourceSlot, priority);
        }
        return false;
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

// Note: ItemSlotOutput is already defined in InventorySpinningWheel.cs and can be reused
