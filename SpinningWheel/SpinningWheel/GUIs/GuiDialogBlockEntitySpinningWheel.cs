using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SpinningWheel.GUIs
{
    public class GuiDialogBlockEntitySpinningWheel : GuiDialogBlockEntity
    {
        long lastRedrawMs;

        protected override double FloatyDialogPosition => 0.75;

        public GuiDialogBlockEntitySpinningWheel(string DialogTitle, InventoryBase Inventory, BlockPos BlockEntityPosition, ICoreClientAPI capi)
            : base(DialogTitle, Inventory, BlockEntityPosition, capi)
        {
            if (IsDuplicate) return;

            capi.World.Player.InventoryManager.OpenInventory(Inventory);

            SetupDialog();
        }

        private void OnInventorySlotModified(int slotid)
        {
            // Direct call can cause InvalidOperationException
            capi.Event.EnqueueMainThreadTask(SetupDialog, "setupspinningwheeldlg");
        }

        void SetupDialog()
        {
            ItemSlot? hoveredSlot = capi.World.Player.InventoryManager.CurrentHoveredSlot;
            if (hoveredSlot != null && hoveredSlot.Inventory == Inventory)
            {
                capi.Input.TriggerOnMouseLeaveSlot(hoveredSlot);
            }
            else
            {
                hoveredSlot = null;
            }

            ElementBounds spinningWheelBounds = ElementBounds.Fixed(0, 0, 200, 90);
            
            // Input slot on the left (fiber/flax)
            ElementBounds inputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 30, 1, 1);
            // Output slot on the right (thread/yarn)
            ElementBounds outputSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 153, 30, 1, 1);

            // Padding around everything
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(spinningWheelBounds);

            // Dialog bounds
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle)
                .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

            ClearComposers();
            SingleComposer = capi.Gui
                .CreateCompo("blockentityspinningwheel" + BlockEntityPosition, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddDynamicCustomDraw(spinningWheelBounds, new DrawDelegateWithBounds(OnBgDraw), "symbolDrawer")
                    .AddItemSlotGrid(Inventory, SendInvPacket, 1, new int[] { 0 }, inputSlotBounds, "inputSlot")
                    .AddItemSlotGrid(Inventory, SendInvPacket, 1, new int[] { 1 }, outputSlotBounds, "outputslot")
                .EndChildElements()
                .Compose()
            ;

            lastRedrawMs = capi.ElapsedMilliseconds;

            if (hoveredSlot != null)
            {
                SingleComposer.OnMouseMove(new MouseEvent(capi.Input.MouseX, capi.Input.MouseY));
            }
        }

        float inputSpinTime;
        float maxSpinTime;
        
        public void Update(float inputSpinTime, float maxSpinTime)
        {
            this.inputSpinTime = inputSpinTime;
            this.maxSpinTime = maxSpinTime;

            if (!IsOpened()) return;

            if (capi.ElapsedMilliseconds - lastRedrawMs > 500)
            {
                if (SingleComposer != null) SingleComposer.GetCustomDraw("symbolDrawer").Redraw();
                lastRedrawMs = capi.ElapsedMilliseconds;
            }
        }

        private void OnBgDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            double top = 30;

            // Arrow Right (spinning progress indicator)
            ctx.Save();
            Matrix m = ctx.Matrix;
            m.Translate(GuiElement.scaled(63), GuiElement.scaled(top + 2));
            m.Scale(GuiElement.scaled(0.6), GuiElement.scaled(0.6));
            ctx.Matrix = m;
            capi.Gui.Icons.DrawArrowRight(ctx, 2);

            double dx = maxSpinTime > 0 ? inputSpinTime / maxSpinTime : 0;

            ctx.Rectangle(GuiElement.scaled(5), 0, GuiElement.scaled(125 * dx), GuiElement.scaled(100));
            ctx.Clip();
            LinearGradient gradient = new LinearGradient(0, 0, GuiElement.scaled(200), 0);
            gradient.AddColorStop(0, new Color(0.4, 0.2, 0.6, 1)); // Purple-ish for spinning
            gradient.AddColorStop(1, new Color(0.6, 0.4, 0.8, 1));
            ctx.SetSource(gradient);
            capi.Gui.Icons.DrawArrowRight(ctx, 0, false, false);
            gradient.Dispose();
            ctx.Restore();
        }

        private void SendInvPacket(object p)
        {
            capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, p);
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Inventory.SlotModified += OnInventorySlotModified;
        }

        public override void OnGuiClosed()
        {
            Inventory.SlotModified -= OnInventorySlotModified;

            SingleComposer.GetSlotGrid("inputSlot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("outputslot").OnGuiClosed(capi);

            base.OnGuiClosed();
        }
    }
}