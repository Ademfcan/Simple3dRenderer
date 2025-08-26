using SDL;
using Simple3dRenderer.Objects;

namespace Simple3dRenderer.Rendering
{
    public interface IFragmentShader<T, TState>
    where T : IFragmentShader<T, TState>
    where TState : IRasterizable
    {
        static abstract SDL_Color getPixelColor(ref TState state,
            Vertex v0, Vertex v1, Vertex v2,
            float w0, float w1, float w2);
    }
}