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

        public required List<DeepShadowMap> maps;
        public required List<PerspectiveLight> lights;

        private int width;
        private int height;

        private SDL_Color backgroundColor;

        public SDL_Color[,] FrameBuffer { get; private set; }
        public float[,] depthBuffer { get; private set; }

        public Texture? currentTexture;



        // --- NEW: InitFrame Method ---
        public void InitFrame(Scene scene, List<DeepShadowMap> shadowMaps, List<PerspectiveLight> lights)
        {
            this.backgroundColor = scene.backgroundColor;
            this.AmbientColor = scene.ambientLight;
            this.CameraPosition = scene.camera.Position;
            this.lights = lights;
            this.maps = shadowMaps;
            // You could also update Shininess/SpecularStrength from a material system here
        }

        public static FrameData Create(int width, int height)
        {
            var data = new FrameData()
            {
                width = width,
                height = height,
                FrameBuffer = new SDL_Color[height, width],
                depthBuffer = new float[height, width],
                maps = new List<DeepShadowMap>(), // Initialize to empty lists
                lights = new List<PerspectiveLight>(),
                // Set some sensible defaults
                SpecularStrength = 1f,
                Shininess = 250,

            };

            data.backgroundColor = new SDL_Color { r = 0, g = 0, b = 0, a = 255 };
            data.Reset();
            return data;
        }



        public FrameData CreateThreadLocalState(int tileWidth, int tileHeight)
        {
            var localData = new FrameData
            {
                // --- FIX ---
                // Allocate buffers with the actual tile dimensions.
                FrameBuffer = new SDL_Color[tileHeight, tileWidth],
                depthBuffer = new float[tileHeight, tileWidth],

                // Use the tile dimensions for width/height properties as well,
                // so Reset() works correctly on the smaller buffer.
                width = tileWidth,
                height = tileHeight,

                // These properties are shared and can be copied by reference or value
                AmbientColor = this.AmbientColor,
                backgroundColor = this.backgroundColor,
                CameraPosition = this.CameraPosition,
                currentTexture = this.currentTexture,
                Shininess = this.Shininess,
                SpecularStrength = this.SpecularStrength,
                maps = this.maps,
                lights = this.lights
            };
            return localData;
        }


        public void MergeTile(FrameData tileState, int tileMinX, int tileMinY)
        {
            // NO CHANGE NEEDED HERE!
            // With the above fix, this method will now receive a correctly-sized
            // tileState, and these GetLength calls will return the tile dimensions.
            // The logic becomes correct automatically.
            int tileHeight = tileState.FrameBuffer.GetLength(0);
            int tileWidth = tileState.FrameBuffer.GetLength(1);

            for (int y = 0; y < tileHeight; y++)
            {
                for (int x = 0; x < tileWidth; x++)
                {
                    int globalX = tileMinX + x;
                    int globalY = tileMinY + y;

                    // This boundary check is still good for tiles at the screen edges
                    if (globalX < this.width && globalY < this.height)
                    {
                        // Merge based on depth test, to prevent overwriting with background color
                        if (tileState.depthBuffer[y, x] < this.depthBuffer[globalY, globalX])
                        {
                            this.FrameBuffer[globalY, globalX] = tileState.FrameBuffer[y, x];
                            this.depthBuffer[globalY, globalX] = tileState.depthBuffer[y, x];
                        }
                    }
                }
            }
        }


        public int getHeight()
        {
            return height;
        }

        public int getWidth()
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

        public void Reset()
        {
            // This manual loop is needed for a 2D array of structs
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    FrameBuffer[i, j] = backgroundColor;
                    depthBuffer[i, j] = float.MaxValue;
                }
            }
        }
    }
}