using SDL;
using Simple3dRenderer.Objects;
using Simple3dRenderer.Rendering;
using Simple3dRenderer.Textures;

namespace Simple3dRenderer.Lighting{
    public class LightData : ITiledRasterizable<LightData>, ITextured
    {
        private int width;
        private int height;

        public DeepShadowMap shadowMap;

        // NOTE: The ITextured fields are not used by the LightFragmentProcessor,
        // but are kept for interface compatibility.
        public Texture? texture;
        public SDL_Color[,] frameBuffer { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public float[,] depthBuffer { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        float[]? ITiledRasterizable<LightData>.depthBuffer => null;

        public static LightData Create(int width, int height)
        {
            return new LightData()
            {
                width = width,
                height = height,
                shadowMap = new DeepShadowMap(width, height),
            };
        }

        // --- NEW INTERFACE IMPLEMENTATIONS ---

        public void Reset()
        {
            // Clearing for a shadow map means initializing its data structures.
            shadowMap.Clear();
        }

        public LightData CreateThreadLocalState(int tileWidth, int tileHeight)
        {
            // Each thread needs its own private shadow map to write to, to avoid race conditions.
            return new LightData
            {
                width = this.width,
                height = this.height,
                shadowMap = new DeepShadowMap(tileWidth, tileHeight) // IMPORTANT: Local map is tile-sized
            };
        }

        public void MergeTile(LightData tileState, int tileMinX, int tileMinY)
        {
            this.shadowMap.MergeFrom(tileState.shadowMap, tileMinX, tileMinY);
        }

        // --- Existing Methods ---
        public int GetHeight() => height;
        public int GetWidth() => width;
        public Texture? GetTexture() => texture;
        public void SetTexture(Texture? texture) => this.texture = texture;

        public void InitFrame(Scene scene, List<DeepShadowMap> shadowMaps, List<PerspectiveLight> lights)
        {

        }

        public void SyncPerFrameState(LightData otherState)
        {
            
        }
    }
}