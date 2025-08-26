using System.Numerics;

using SDL;

using Simple3dRenderer.Lighting;

using Simple3dRenderer.Objects;

using Simple3dRenderer.Textures;


namespace Simple3dRenderer.Rendering

{

    public class FrameData : ITiledRasterizable<FrameData>, ITextured

    {
        public Vector3 AmbientColor;          // linear 0..1 (e.g. new(0.03f))

        public Vector3 CameraPosition;        // world space camera position

        public float SpecularStrength;        // e.g. 0.5f

        public float Shininess;               // e.g. 32..128

        private readonly int width;

        private readonly int height;

        private SDL_Color backgroundColor;
        private Texture? currentTexture;

        public required List<DeepShadowMap> Maps { get; set; }

        public required List<PerspectiveLight> Lights { get; set; }

        public readonly SDL_Color[] FrameBuffer;
        public readonly float[] depthBuffer;

        public void InitFrame(Scene scene, List<DeepShadowMap> shadowMaps, List<PerspectiveLight> lights)

        {

            this.backgroundColor = scene.backgroundColor;

            this.AmbientColor = scene.ambientLight;

            this.CameraPosition = scene.camera.Position;

            this.Lights = lights;

            this.Maps = shadowMaps;

            // update Shininess/SpecularStrength from a material system here

        }



        // Modified Create method
        public static FrameData Create(int width, int height)
        {
            var data = new FrameData(width, height)
            {
                Maps = new List<DeepShadowMap>(),
                Lights = new List<PerspectiveLight>(),
                SpecularStrength = 1f,
                Shininess = 250,
            };
            data.backgroundColor = new SDL_Color { r = 0, g = 0, b = 0, a = 255 };
            data.Reset();
            return data;
        }

        // Private constructor to enforce initialization
        private FrameData(int w, int h)
        {
            width = w;
            height = h;
            FrameBuffer = new SDL_Color[w * h];
            depthBuffer = new float[w * h];
        }

        // Modified CreateThreadLocalState
        public FrameData CreateThreadLocalState(int tileWidth, int tileHeight)
        {
            var localData = new FrameData(tileWidth, tileHeight)
            {
                // Copy shared properties
                AmbientColor = this.AmbientColor,
                backgroundColor = this.backgroundColor,
                CameraPosition = this.CameraPosition,
                currentTexture = this.currentTexture,
                Shininess = this.Shininess,
                SpecularStrength = this.SpecularStrength,
                Maps = this.Maps,
                Lights = this.Lights
            };
            return localData;
        }

        public void SyncPerFrameState(FrameData mainState)
        {
            // Copy all the properties that are set by InitFrame
            this.CameraPosition = mainState.CameraPosition;
            this.Lights = mainState.Lights;
            this.Maps = mainState.Maps;
            this.AmbientColor = mainState.AmbientColor;
            this.Shininess = mainState.Shininess;
            this.SpecularStrength = mainState.SpecularStrength;
        }

        // Modified MergeTile
        public void MergeTile(FrameData tileState, int tileMinX, int tileMinY)
        {
            int tileHeight = tileState.height;
            int tileWidth = tileState.width;

            for (int y = 0; y < tileHeight; y++)
            {
                for (int x = 0; x < tileWidth; x++)
                {
                    int globalX = tileMinX + x;
                    int globalY = tileMinY + y;

                    if (globalX < this.width && globalY < this.height)
                    {
                        int localIndex = y * tileWidth + x;
                        int globalIndex = globalY * this.width + globalX;

                        if (tileState.depthBuffer[localIndex] < this.depthBuffer[globalIndex])
                        {
                            this.FrameBuffer[globalIndex] = tileState.FrameBuffer[localIndex];
                            this.depthBuffer[globalIndex] = tileState.depthBuffer[localIndex];
                        }
                    }
                }
            }
        }

        // Modified Reset
        public void Reset()
        {
            Array.Fill(depthBuffer, float.MaxValue);
            Array.Fill(FrameBuffer, backgroundColor);
        }



        public int GetHeight()

        {

            return height;

        }


        public int GetWidth()

        {

            return width;

        }


        public Texture? GetTexture()

        {

            return currentTexture;

        }


        public void SetTexture(Texture? texture)

        {

            currentTexture = texture;

        }

    }

}