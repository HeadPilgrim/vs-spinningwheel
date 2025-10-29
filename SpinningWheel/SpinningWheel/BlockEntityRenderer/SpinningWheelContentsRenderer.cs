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

        public void SetContents(ItemStack newContentStack, ModelTransform transform)
        {
            contentStackRenderer?.Dispose();
            contentStackRenderer = null;

            this.transform = transform;
            if (transform == null) this.transform = defaultTransform;
            this.transform.EnsureDefaultValues();

            meshref?.Dispose();
            meshref = null;
            
            if (newContentStack == null)
            {
                this.ContentStack = null;
                return;
            }

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

            meshref = api.Render.UploadMultiTextureMesh(ingredientMesh);
            this.ContentStack = newContentStack;
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
            
            
            //rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.StandardShader;
            prog.Use();
            prog.DontWarpVertices = 0;
            prog.AddRenderFlags = 0;
            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
            prog.RgbaTint = ColorUtil.WhiteArgbVec;
            prog.NormalShaded = 1;
            prog.ExtraGodray = 0;
            prog.SsaoAttn = 0;
            prog.AlphaTest = 0.05f;
            prog.OverlayOpacity = 0;

            Vec4f lightrgbs = api.World.BlockAccessor.GetLightRGBs(pos.X, pos.Y, pos.Z);
            prog.RgbaLightIn = lightrgbs;
            prog.ExtraGlow = 15;
            
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