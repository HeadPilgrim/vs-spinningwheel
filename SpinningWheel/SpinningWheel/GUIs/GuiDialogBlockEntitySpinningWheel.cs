using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace SpinningWheel.GUIs
{
    public class GuiDialogBlockEntitySpinningWheel : GuiDialogBlockEntity
    {
        private float inputSpinTime;
        private float maxSpinTime;
        private bool isSpinning;
        private long lastUpdateMs;

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

            lastUpdateMs = capi.ElapsedMilliseconds;

            if (hoveredSlot != null)
            {
                SingleComposer.OnMouseMove(new MouseEvent(capi.Input.MouseX, capi.Input.MouseY));
            }
        }
        
        public void Update(float inputSpinTime, float maxSpinTime)
        {
            bool hasChanged = false;
            
            // Check if values have actually changed
            if (System.Math.Abs(this.inputSpinTime - inputSpinTime) > 0.01f)
            {
                this.inputSpinTime = inputSpinTime;
                hasChanged = true;
            }
            
            if (System.Math.Abs(this.maxSpinTime - maxSpinTime) > 0.01f)
            {
                this.maxSpinTime = maxSpinTime;
                hasChanged = true;
            }

            // Determine if we're actively spinning
            bool wasSpinning = isSpinning;
            isSpinning = inputSpinTime > 0 && maxSpinTime > 0;

            if (!IsOpened()) return;

            // Redraw conditions:
            // 1. Values changed
            // 2. Started or stopped spinning
            // 3. Currently spinning and enough time has passed (smooth updates)
            long now = capi.ElapsedMilliseconds;
            bool shouldRedraw = hasChanged || 
                               (wasSpinning != isSpinning) || 
                               (isSpinning && now - lastUpdateMs > 50); // 20 FPS for smooth animation

            if (shouldRedraw)
            {
                if (SingleComposer != null)
                {
                    SingleComposer.GetCustomDraw("symbolDrawer").Redraw();
                }
                lastUpdateMs = now;
            }
        }

        private void OnBgDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            // Slots are at Y=30, and slot size is typically around 50 pixels
            // So we want the bar centered vertically with the slots
            double slotTop = 30;
            double slotSize = 50; // Standard slot height
            double barHeight = 20;
            
            // Center the bar vertically with the slots
            double top = slotTop + (slotSize - barHeight) / 2; // This centers it
            
            double left = 63;
            double barWidth = 75;

            // Calculate progress (0.0 to 1.0)
            double progress = maxSpinTime > 0 ? System.Math.Min(inputSpinTime / maxSpinTime, 1.0) : 0;

            ctx.Save();
            
            // Draw background/border
            ctx.SetSourceRGBA(0.2, 0.2, 0.2, 1);
            ctx.Rectangle(
                GuiElement.scaled(left), 
                GuiElement.scaled(top), 
                GuiElement.scaled(barWidth), 
                GuiElement.scaled(barHeight)
            );
            ctx.Fill();
            
            // Draw progress fill
            if (progress > 0.001)
            {
                LinearGradient gradient = new LinearGradient(
                    GuiElement.scaled(left), 0, 
                    GuiElement.scaled(left + barWidth), 0
                );
                
                // Dynamic color based on progress
                if (progress < 0.3)
                {
                    gradient.AddColorStop(0, new Color(0.8, 0.3, 0.3, 1)); // Reddish - just started
                    gradient.AddColorStop(1, new Color(0.9, 0.4, 0.4, 1));
                }
                else if (progress < 0.7)
                {
                    gradient.AddColorStop(0, new Color(0.9, 0.7, 0.3, 1)); // Orange/yellow - mid progress
                    gradient.AddColorStop(1, new Color(1.0, 0.8, 0.4, 1));
                }
                else
                {
                    gradient.AddColorStop(0, new Color(0.3, 0.8, 0.3, 1)); // Green - almost done
                    gradient.AddColorStop(1, new Color(0.4, 0.9, 0.4, 1));
                }
                
                ctx.SetSource(gradient);
                ctx.Rectangle(
                    GuiElement.scaled(left + 2), 
                    GuiElement.scaled(top + 2), 
                    GuiElement.scaled((barWidth - 4) * progress), 
                    GuiElement.scaled(barHeight - 4)
                );
                ctx.Fill();
                gradient.Dispose();
            }
            
            // Draw border/outline
            ctx.SetSourceRGBA(0.6, 0.6, 0.6, 1);
            ctx.LineWidth = GuiElement.scaled(1);
            ctx.Rectangle(
                GuiElement.scaled(left), 
                GuiElement.scaled(top), 
                GuiElement.scaled(barWidth), 
                GuiElement.scaled(barHeight)
            );
            ctx.Stroke();
            
            // Draw percentage text (centered in the bar)
            if (isSpinning && maxSpinTime > 0)
            {
                string progressText = $"{(progress * 100):F0}%";
                
                ctx.SetSourceRGBA(1, 1, 1, 0.9);
                ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Bold);
                ctx.SetFontSize(GuiElement.scaled(11));
                
                TextExtents extents = ctx.TextExtents(progressText);
                ctx.MoveTo(
                    GuiElement.scaled(left + barWidth/2) - extents.Width / 2, 
                    GuiElement.scaled(top + barHeight/2) + extents.Height / 2
                );
                ctx.ShowText(progressText);
            }
            
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