using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace SpinningWheel.Inventories;

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
        return new ItemSlotSurvival(this);
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
    
    public override bool CanContain(ItemSlot sinkSlot, ItemSlot sourceSlot)
    {
        // Output slot (slot 1) can only be taken from, not put into
        if (sinkSlot == slots[1]) return false;
    
        // Input slot (slot 0) only accepts spinnable items
        if (sinkSlot == slots[0])
        {
            return sourceSlot?.Itemstack?.ItemAttributes?.KeyExists("spinningProps") == true;
        }
    
        return base.CanContain(sinkSlot, sourceSlot);
    }
    
    public override void DidModifyItemSlot(ItemSlot slot, ItemStack extractedStack = null)
    {
        base.DidModifyItemSlot(slot, extractedStack);
    
        // Force mark dirty when slot changes
        Api?.World?.BlockAccessor?.GetBlockEntity(Pos)?.MarkDirty(true);
    }
}
