using MathNet.Numerics.LinearAlgebra;
using SDL;
using Simple3dRenderer.Lighting;
using Simple3dRenderer.Objects;
using Simple3dRenderer.Textures;

namespace Simple3dRenderer.Rendering
{
    public struct LightFragmentProcessor<T> : IFragmentProcessor<LightFragmentProcessor<T>, LightData> where T : IFragmentShader<T, LightData>
    {
        public static void ProcessFragment(ref LightData state, int x, int y, float z, float fw0, float fw1, float fw2, Vertex v0, Vertex v1, Vertex v2, bool isMultithreaded)
        {
            SDL_Color fragColor = T.getPixelColor(ref state, v0, v1, v2, fw0, fw1, fw2);
            state.shadowMap.AddVisibilityPoint(x, y, z, fragColor.a / 255f);
        }
    }
}