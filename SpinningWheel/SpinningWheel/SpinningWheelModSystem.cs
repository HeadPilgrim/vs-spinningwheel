using SpinningWheel.BlockEntities;
using SpinningWheel.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using System;

namespace SpinningWheel;

public class SpinningWheelModSystem : ModSystem
{
    private ICoreClientAPI clientApi;
    public static SpinningWheelConfig Config { get; private set; }

    // Called on server and client
    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass("BlockSpinningWheel", typeof(BlockSpinningWheel));
        api.RegisterBlockEntityClass("BlockEntitySpinningWheel", typeof(BlockEntitySpinningWheel));
        
        Mod.Logger.Notification("Registered block and block entity for headpilgrim-spinningwheel");
        Mod.Logger.Notification("Mod Started Successfully");
    }
    
    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        TryToLoadConfig(api);
    }
    
    private void TryToLoadConfig(ICoreAPI api)
    {
        try
        {
            Config = api.LoadModConfig<SpinningWheelConfig>("spinningwheelconfig.json");
            if (Config == null)
            {
                Config = new SpinningWheelConfig();
            }
            api.StoreModConfig(Config, "spinningwheelconfig.json");
            
            Mod.Logger.Notification($"Spinning Wheel Config Loaded - Require Tailor Class: {Config.RequireTailorClass}");
        }
        catch (Exception e)
        {
            Mod.Logger.Error("Could not load spinning wheel config! Loading default settings instead.");
            Mod.Logger.Error(e);
            Config = new SpinningWheelConfig();
        }
    }
    
    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        
        clientApi = api;
        
        // Client creates default config if server hasn't sent one yet
        if (Config == null)
        {
            Config = new SpinningWheelConfig();
        }
        
        api.Input.RegisterHotKey("openspinningwheel", "Open Spinning Wheel GUI", 
            GlKeys.E, HotkeyType.GUIOrOtherControls);
        
        api.Input.SetHotKeyHandler("openspinningwheel", OnOpenSpinningWheelHotkey);
    }
    
    private bool OnOpenSpinningWheelHotkey(KeyCombination keyCombination)
    {
        EntityPlayer player = clientApi.World.Player.Entity;
        
        if (player?.MountedOn?.MountSupplier is BlockEntitySpinningWheel spinningWheel)
        {
            spinningWheel.OpenGui(clientApi.World.Player);
            return true;
        }
        
        return false;
    }
}