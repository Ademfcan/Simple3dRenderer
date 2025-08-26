using SDL;
using Simple3dRenderer.Extensions;
using Simple3dRenderer.Objects;
using Simple3dRenderer.Shaders;
using Simple3dRenderer.Textures;

namespace Simple3dRenderer.Rendering
{
     public struct MaterialShader<TState> : IFragmentShader<MaterialShader<TState>, TState>
    where TState : IRasterizable, ITextured
    {
        public static SDL_Color getPixelColor(ref TState state, Vertex v0, Vertex v1, Vertex v2, float w0, float w1, float w2)
        {
            // Look up the texture from the state
            if (state.GetTexture() != null)
            {
                return TextureShader.getPixelColor(v0, v1, v2, w0, w1, w2, state.GetTexture());
            }
            else
            {
                return SDLColorExtensions.Interpolate(v0.Color, v1.Color, v2.Color, w0, w1, w2);
            }
        }

    }
}