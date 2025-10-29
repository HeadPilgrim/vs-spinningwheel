using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace SpinningWheel.Utilities;

public interface ISpinningWheel
{
    public interface IInSpinningWeehlMeshSupplier
    {
        /// <summary>
        /// Return the mesh you want to be rendered in the firepit. You can return null to signify that you do not wish to use a custom mesh.
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        MeshData GetMeshWhenInSpinningWheel(ItemStack stack, IWorldAccessor world, BlockPos pos);
    }

    public interface IInSpinningWheelRenderer : IRenderer
    {
        /// <summary>
        /// Called every 100ms in case you want to do custom stuff, such as playing a sound after a certain temperature
        /// </summary>
        /// <param name="temperature"></param>
        void OnUpdate(float temperature);

        /// <summary>
        /// Called when the itemstack has been moved to the output slot
        /// </summary>
        void OnCookingComplete();
    }
    
    public interface IInSpinningWheelRendererSupplier
    {
        /// <summary>
        /// Return the renderer that perfroms the rendering of your block/item in the firepit. You can return null to signify that you do not wish to use a custom renderer
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        IInFirepitRenderer GetRendererWhenInFirepit(ItemStack stack, BlockEntityFirepit firepit, bool forOutputSlot);
    }
}