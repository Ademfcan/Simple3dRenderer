using MathNet.Numerics.LinearAlgebra;
using SDL;
using Simple3dRenderer.Lighting;
using Simple3dRenderer.Objects;
using Simple3dRenderer.Textures;

namespace Simple3dRenderer.Rendering
{
    public struct LightData : IRasterizable, ITextured
    {
        private int width;
        private int height;

        public DeepShadowMap shadowMap;

        public Matrix<float> wtoc;

        public Texture? texture;

        public static LightData create(IPerspective perspective)
        {
            return new LightData()
            {
                width = perspective.getWidth(),
                height = perspective.getHeight(),
                shadowMap = new DeepShadowMap(perspective.getWidth(), perspective.getHeight()),
                wtoc = perspective.getWToC()
            };
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
            return texture;
        }

        public void SetTexture(Texture? texture)
        {
            this.texture = texture;
        }
    }

    public struct LightFragmentProcessor<T> : IFragmentProcessor<LightFragmentProcessor<T>, LightData> where T : IFragmentShader<T, LightData>
    {
        public static void ProcessFragment(ref LightData state, int x, int y, float z, float fw0, float fw1, float fw2, Vertex v0, Vertex v1, Vertex v2, bool isMultithreaded)
        {

            // We only need to get the fragment's color to determine its alpha.
            SDL_Color fragColor = T.getPixelColor(ref state, v0, v1, v2, fw0, fw1, fw2);

            // Directly use the provided coordinates to add the visibility point.
            // The 'z' value is the perspective-correct depth in the [0, 1] range.
            state.shadowMap.AddVisibilityPoint(x, y, z, fragColor.a / 255f);
        }
    }
}