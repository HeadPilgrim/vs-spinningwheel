using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace SpinningWheel.Inventories;

#nullable disable

public class InventorySpinningWheel: InventoryBase, ISlotProvider
{
    ItemSlot[] slots;
    public ItemSlot[] Slots { get { return slots; } }

    public InventorySpinningWheel(string inventoryID, ICoreAPI api) : base(inventoryID, api)
    {
        // slot 0 = input
        // slot 1 = output
        slots = GenEmptySlots(2);
    }

    public InventorySpinningWheel(string className, string instanceID, ICoreAPI api) : base(className, instanceID, api)
    {
        slots = GenEmptySlots(2);
    }

    public override int Count
    {
        get { return 2; }
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
        // Slot 0 = input (only accepts spinnable items)
        // Slot 1 = output (read-only)
        if (i == 0)
        {
            return new ItemSlotSpinningInput(this);
        }
        else
        {
            return new ItemSlotOutput(this);
        }
    }

    public override float GetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
    {
        if (targetSlot == slots[0] && 
            sourceSlot?.Itemstack?.ItemAttributes?.KeyExists("spinningProps") == true)
        {
            return 4f;
        }

        return base.GetSuitability(sourceSlot, targetSlot, isMerge);
    }

    public override ItemSlot GetAutoPushIntoSlot(BlockFacing atBlockFace, ItemSlot fromSlot)
    {
        return slots[0];
    }
}

// Custom slot for input that only accepts spinnable items
public class ItemSlotSpinningInput : ItemSlotSurvival
{
    public ItemSlotSpinningInput(InventoryBase inventory) : base(inventory)
    {
    }

    public override bool CanHold(ItemSlot sourceSlot)
    {
        if (sourceSlot?.Itemstack?.ItemAttributes?.KeyExists("spinningProps") == true)
        {
            return base.CanHold(sourceSlot);
        }
        return false;
    }

    public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
    {
        if (sourceSlot?.Itemstack?.ItemAttributes?.KeyExists("spinningProps") == true)
        {
            return base.CanTakeFrom(sourceSlot, priority);
        }
        return false;
    }
    
    public override int GetRemainingSlotSpace(ItemStack forItemstack)
    {
        // Reject items without spinningProps
        if (forItemstack?.ItemAttributes?.KeyExists("spinningProps") != true)
        {
            return 0; // No space for non-spinnable items
        }
        
        return base.GetRemainingSlotSpace(forItemstack);
    }
}

// Custom slot for output (read-only, can only take out)
public class ItemSlotOutput : ItemSlotSurvival
{
    public ItemSlotOutput(InventoryBase inventory) : base(inventory)
    {
    }

    public override bool CanHold(ItemSlot sourceSlot)
    {
        return false; // Can't put anything into output slot
    }

    public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
    {
        return false; // Can't put anything into output slot
    }
    
    public override int GetRemainingSlotSpace(ItemStack forItemstack)
    {
        return 0; // Output slot never accepts items
    }
}