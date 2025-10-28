using SpinningWheel.BlockEntities;
using SpinningWheel.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common;

namespace SpinningWheel;

public class SpinningWheelModSystem : ModSystem
{
    private ICoreClientAPI clientApi;
    
    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass("BlockSpinningWheel", typeof(BlockSpinningWheel));
        api.RegisterBlockEntityClass("BlockEntitySpinningWheel", typeof(BlockEntitySpinningWheel));
        Mod.Logger.Notification("Registered block and block entity for headpilgrim-spinningwheel");
        Mod.Logger.Notification("Mod Started Successfully");
    }
    
    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        
        clientApi = api;
        
        api.Input.RegisterHotKey("openspinningwheel", "Open Spinning Wheel GUI", 
            GlKeys.F, HotkeyType.GUIOrOtherControls);
        
        api.Input.SetHotKeyHandler("openspinningwheel", OnOpenSpinningWheelHotkey);
    }
    
    private bool OnOpenSpinningWheelHotkey(KeyCombination keyCombination)
    {
        EntityPlayer player = clientApi.World.Player.Entity; // Use clientApi instead
        
        // Check if player is mounted on a spinning wheel
        if (player?.MountedOn?.MountSupplier is BlockEntitySpinningWheel spinningWheel)
        {
            spinningWheel.OpenGui(clientApi.World.Player);
            return true; // Hotkey handled
        }
        
        return false; // Hotkey not handled
    }
}