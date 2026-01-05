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
        // slot 0 = input (twine/thread)
        // slot 1 = output (cloth/fabric)
        slots = GenEmptySlots(2);
    }

    public InventoryFlyshuttleLoom(string className, string instanceID, ICoreAPI api) : base(className, instanceID, api)
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
        // Slot 0 = input (only accepts weavable items)
        // Slot 1 = output (read-only)
        if (i == 0)
        {
            return new ItemSlotWeavingInput(this);
        }
        else
        {
            return new ItemSlotOutput(this);
        }
    }

    public override float GetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
    {
        if (targetSlot == slots[0] &&
            sourceSlot?.Itemstack?.ItemAttributes?.KeyExists("weavingProps") == true)
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
