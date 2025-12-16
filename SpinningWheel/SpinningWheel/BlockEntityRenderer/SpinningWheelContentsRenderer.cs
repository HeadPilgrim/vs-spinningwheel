using System.Linq;
using SpinningWheel.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace SpinningWheel.BLockEntityRenderer
{
    public class SpinningWheelContentsRenderer : IRenderer
    {
        MultiTextureMeshRef meshref;
        ICoreClientAPI api;
        BlockPos pos;
        public ItemStack ContentStack;
        int textureId;
        Matrixf ModelMat = new Matrixf();

        ModelTransform transform;
        ModelTransform defaultTransform;

        public IInSpinningWheelRenderer contentStackRenderer;

        public double RenderOrder
        {
            get { return 0.5; }
        }

        public int RenderRange
        {
            get { return 24; }
        }

        public SpinningWheelContentsRenderer(ICoreClientAPI api, BlockPos pos)
        {
            this.api = api;
            this.pos = pos;
            
            // Default transform for items in the spinning wheel
            transform = new ModelTransform().EnsureDefaultValues();
            transform.Origin.X = 0.5f;
            transform.Origin.Y = 0.5f;
            transform.Origin.Z = 0.5f;
            transform.Rotation.X = 0;
            transform.Rotation.Y = 0;
            transform.Rotation.Z = 0;
            transform.Translation.X = 0.5f;
            transform.Translation.Y = 0.4f;
            transform.Translation.Z = 0.5f;
            transform.ScaleXYZ.X = 0.75f;
            transform.ScaleXYZ.Y = 0.75f;
            transform.ScaleXYZ.Z = 0.75f;

            defaultTransform = transform;
        }

        internal void SetChildRenderer(ItemStack contentStack, IInSpinningWheelRenderer renderer)
        {
            this.ContentStack = contentStack;
            meshref?.Dispose();
            meshref = null;
            
            contentStackRenderer = renderer;
        }

        Vec4f colorTint = ColorUtil.WhiteArgbVec; // Add this field

        public void SetContents(ItemStack newContentStack, ModelTransform transform, Vec4f fiberColor)
        {
            contentStackRenderer?.Dispose();
            contentStackRenderer = null;

            this.transform = transform;
            if (transform == null) this.transform = defaultTransform;
            this.transform.EnsureDefaultValues();

            meshref?.Dispose();
            meshref = null;
            
            // Store the color tint
            this.colorTint = fiberColor;
            
            if (newContentStack == null)
            {
                this.ContentStack = null;
                return;
            }

            api.Logger.Debug($"[Renderer] Setting color tint: R={colorTint.R}, G={colorTint.G}, B={colorTint.B}");

            MeshData ingredientMesh;
            if (newContentStack.Class == EnumItemClass.Item)
            {
                api.Tesselator.TesselateItem(newContentStack.Item, out ingredientMesh);
                textureId = api.ItemTextureAtlas.Positions[newContentStack.Item.FirstTexture.Baked.TextureSubId].atlasTextureId;
            }
            else
            {
                api.Tesselator.TesselateBlock(newContentStack.Block, out ingredientMesh);
                textureId = api.BlockTextureAtlas.Positions[newContentStack.Block.Textures.FirstOrDefault().Value.Baked.TextureSubId].atlasTextureId;
            }

            // Apply color tint directly to mesh vertices
            TintMeshVertices(ingredientMesh, fiberColor);
            api.Logger.Debug($"[Renderer] Tinted {ingredientMesh.Rgba?.Length / 4 ?? 0} vertices");

            meshref = api.Render.UploadMultiTextureMesh(ingredientMesh);
            this.ContentStack = newContentStack;
        }

        private void TintMeshVertices(MeshData mesh, Vec4f color)
        {
            if (mesh.Rgba == null || mesh.Rgba.Length == 0)
            {
                api.Logger.Debug("[Renderer] No RGBA data in mesh!");
                return;
            }
            
            api.Logger.Debug($"[Renderer] Tinting mesh with R={color.R}, G={color.G}, B={color.B}");
            
            for (int i = 0; i < mesh.Rgba.Length; i += 4)
            {
                // Multiply existing vertex color by tint color
                mesh.Rgba[i] = (byte)(mesh.Rgba[i] * color.R);         // R
                mesh.Rgba[i + 1] = (byte)(mesh.Rgba[i + 1] * color.G); // G
                mesh.Rgba[i + 2] = (byte)(mesh.Rgba[i + 2] * color.B); // B
                // Keep alpha the same: mesh.Rgba[i + 3]
            }
        }

        private Vec4f GetFiberColor(ItemStack contentStack)
        {
            // Default to white (no tint)
            Vec4f baseColor = ColorUtil.WhiteArgbVec;

            if (contentStack?.Item == null) return baseColor;

            string itemCode = contentStack.Item.Code.ToString();

            // Parse color from item code (e.g., "fibers-generic-gray" -> gray)
            if (itemCode.Contains("fibers-"))
            {
                // Extract color name from code
                string[] parts = itemCode.Split('-');
                if (parts.Length >= 3)
                {
                    string colorName = parts[2]; // e.g., "gray", "black", "brown"
                    return GetColorFromName(colorName);
                }
            }

            // Alternative: Check item attributes for color
            if (contentStack.Item.Attributes?.KeyExists("fiberColor") == true)
            {
                string colorHex = contentStack.Item.Attributes["fiberColor"].AsString();
                double[] colorArray = ColorUtil.Hex2Doubles(colorHex);
                return new Vec4f((float)colorArray[0], (float)colorArray[1], (float)colorArray[2], (float)colorArray[3]);
            }

            return baseColor;
        }

        private Vec4f GetColorFromName(string colorName)
        {
            // Map color names to RGB values
            switch (colorName.ToLower())
            {
                case "white":
                    return new Vec4f(1.0f, 1.0f, 1.0f, 1.0f);
                case "gray":
                case "grey":
                    return new Vec4f(0.5f, 0.5f, 0.5f, 1.0f);
                case "black":
                    return new Vec4f(0.2f, 0.2f, 0.2f, 1.0f);
                case "brown":
                    return new Vec4f(0.6f, 0.4f, 0.2f, 1.0f);
                case "redbrown":
                    return new Vec4f(0.7f, 0.3f, 0.2f, 1.0f);
                case "lightbrown":
                    return new Vec4f(0.8f, 0.7f, 0.5f, 1.0f);
                case "yellow":
                    return new Vec4f(1.0f, 1.0f, 0.5f, 1.0f);
                case "plain":
                    return new Vec4f(0.9f, 0.85f, 0.75f, 1.0f);
                default:
                    return ColorUtil.WhiteArgbVec;
            }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (contentStackRenderer != null)
            {
                contentStackRenderer.OnRenderFrame(deltaTime, stage);
                return;
            }

            if (meshref == null) return;
    
            IRenderAPI rpi = api.Render;
            Vec3d camPos = api.World.Player.Entity.CameraPos;

            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.StandardShader;
            prog.Use();
            prog.DontWarpVertices = 0;
            prog.AddRenderFlags = 0;
            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
    
            // Remove this - not working:
            // prog.RgbaTint = colorTint;
    
            prog.RgbaTint = ColorUtil.WhiteArgbVec; // Set to white (no shader tint)
    
            prog.NormalShaded = 1;
            prog.ExtraGodray = 0;
            prog.SsaoAttn = 0;
            prog.AlphaTest = 0.05f;
            prog.OverlayOpacity = 0;

            Vec4f lightrgbs = api.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
            prog.RgbaLightIn = lightrgbs;
            prog.ExtraGlow = 0;
    
            prog.ModelMatrix = ModelMat
                    .Identity()
                    .Translate(pos.X - camPos.X + transform.Translation.X, pos.Y - camPos.Y + transform.Translation.Y, pos.Z - camPos.Z + transform.Translation.Z)
                    .Translate(transform.Origin.X, transform.Origin.Y, transform.Origin.Z)
                    .RotateX(transform.Rotation.X * GameMath.DEG2RAD)
                    .RotateY(transform.Rotation.Y * GameMath.DEG2RAD)
                    .RotateZ(transform.Rotation.Z * GameMath.DEG2RAD)
                    .Scale(transform.ScaleXYZ.X, transform.ScaleXYZ.Y, transform.ScaleXYZ.Z)
                    .Translate(-transform.Origin.X, -transform.Origin.Y, -transform.Origin.Z)
                    .Values
                ;

            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            rpi.RenderMultiTextureMesh(meshref, "tex");

            prog.Stop();
        }

        public void Dispose()
        {
            api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            meshref?.Dispose();
            contentStackRenderer?.Dispose();
        }
    }
}