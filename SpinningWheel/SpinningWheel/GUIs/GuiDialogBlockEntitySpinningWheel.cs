using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Cairo;

namespace SpinningWheel.GUIs;

public class GuiDialogBlockEntitySpinningWheel : GuiDialogBlockEntity
{
    long lastRedrawMs;
    string currentOutputText;
    EnumPosFlag screenPos;
    
    protected override double FloatyDialogPosition => 0.6;
    protected override double FloatyDialogAlign => 0.8;

    public override double DrawOrder => 0.2;
    
    public GuiDialogBlockEntitySpinningWheel(string dlgTitle, InventoryBase Inventory, BlockPos bePos, SyncedTreeAttribute tree, ICoreClientAPI capi) : base(dlgTitle, Inventory, bePos, capi)
    {
        if (IsDuplicate) return;
        tree.OnModified.Add(new TreeModifiedListener() { listener = OnAttributesModified } );
        Attributes = tree;
    }
    
    private void OnInventorySlotModified(int slotid)
    {
        // Direct call can cause InvalidOperationException
        
        //Commented out until SetupDialog() exists
        //capi.Event.EnqueueMainThreadTask(SetupDialog, "setupfirepitdlg");
    }
    private void OnAttributesModified()
    {
        if (!IsOpened()) return;
        
        if (capi.ElapsedMilliseconds - lastRedrawMs > 500)
        {
            if (SingleComposer != null) SingleComposer.GetCustomDraw("symbolDrawer").Redraw();
            lastRedrawMs = capi.ElapsedMilliseconds;
        }
    }
    private void OnBgDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
    {
        // 1. Fire
        ctx.Save();
        Matrix m = ctx.Matrix;
        m.Translate(GuiElement.scaled(5), GuiElement.scaled(53));
        m.Scale(GuiElement.scaled(0.25), GuiElement.scaled(0.25));
        ctx.Matrix = m;

        double dy = 210 - 210 * (Attributes.GetFloat("fuelBurnTime", 0) / Attributes.GetFloat("maxFuelBurnTime", 1));
        ctx.Rectangle(0, dy, 200, 210 - dy);
        ctx.Clip();
        LinearGradient gradient = new LinearGradient(0, GuiElement.scaled(250), 0, 0);
        gradient.AddColorStop(0, new Color(1, 1, 0, 1));
        gradient.AddColorStop(1, new Color(1, 0, 0, 1));
        ctx.SetSource(gradient);
        //capi.Gui.Icons.DrawFlame(ctx, 0, false, false);
        gradient.Dispose();
        ctx.Restore();


        // 2. Arrow Right
        ctx.Save();
        m = ctx.Matrix;
        m.Translate(GuiElement.scaled(63), GuiElement.scaled(2));
        m.Scale(GuiElement.scaled(0.6), GuiElement.scaled(0.6));
        ctx.Matrix = m;
        //capi.Gui.Icons.DrawArrowRight(ctx, 2);

        double cookingRel = Attributes.GetFloat("oreCookingTime") / Attributes.GetFloat("maxOreCookingTime", 1);
        ctx.Rectangle(5, 0, 125 * cookingRel, 100);
        ctx.Clip();
        gradient = new LinearGradient(0, 0, 200, 0);
        gradient.AddColorStop(0, new Color(0, 0.4, 0, 1));
        gradient.AddColorStop(1, new Color(0.2, 0.6, 0.2, 1));
        ctx.SetSource(gradient);
        //capi.Gui.Icons.DrawArrowRight(ctx, 0, false, false);
        gradient.Dispose();
        ctx.Restore();
    }
    
    public override void OnGuiOpened()
    {
        base.OnGuiOpened();
        Inventory.SlotModified += OnInventorySlotModified;

        screenPos = GetFreePos("smallblockgui");
        OccupyPos("smallblockgui", screenPos);
        //SetupDialog();
    }
}

