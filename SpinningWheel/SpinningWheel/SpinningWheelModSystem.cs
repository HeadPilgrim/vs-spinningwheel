using SpinningWheel.BlockEntities;
using SpinningWheel.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common;

namespace SpinningWheel;

public class SpinningWheelModSystem : ModSystem

{
    // Called on server and client
    // Useful for registering block/entity classes on both sides
    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass("BlockSpinningWheel", typeof(BlockSpinningWheel));
        api.RegisterBlockEntityClass("BlockEntitySpinningWheel", typeof(BlockEntitySpinningWheel));
        Mod.Logger.Notification("Registered block and block entity for headpilgrim-spinningwheel");
        Mod.Logger.Notification("Mod Started Successfully");
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        Mod.Logger.Notification("Hello from template mod server side: " + Lang.Get("spinningwheel:hello"));
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        Mod.Logger.Notification("Hello from template mod client side: " + Lang.Get("spinningwheel:hello"));
    }
}