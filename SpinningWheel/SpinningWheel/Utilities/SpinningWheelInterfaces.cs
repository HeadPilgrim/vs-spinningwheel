using SpinningWheel.BlockEntities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace SpinningWheel.Utilities
{
    public interface IInSpinningWheelMeshSupplier
    {
        /// <summary>
        /// Return the mesh you want to be rendered in the spinning wheel. You can return null to signify that you do not wish to use a custom mesh.
        /// </summary>
        MeshData GetMeshWhenInSpinningWheel(ItemStack stack, IWorldAccessor world, BlockPos pos);
    }

    public class InSpinningWheelProps
    {
        public ModelTransform Transform;
    }

    public interface IInSpinningWheelRenderer : IRenderer
    {
        /// <summary>
        /// Called every 100ms in case you want to do custom stuff
        /// </summary>
        void OnUpdate(float progress);

        /// <summary>
        /// Called when the itemstack has been moved to the output slot
        /// </summary>
        void OnSpinningComplete();
    }

    public interface IInSpinningWheelRendererSupplier
    {
        /// <summary>
        /// Return the renderer that performs the rendering of your item in the spinning wheel. You can return null to signify that you do not wish to use a custom renderer
        /// </summary>
        IInSpinningWheelRenderer GetRendererWhenInSpinningWheel(ItemStack stack, BlockEntitySpinningWheel spinningWheel, bool forOutputSlot);
    }
}