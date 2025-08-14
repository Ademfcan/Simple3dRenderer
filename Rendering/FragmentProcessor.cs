using Simple3dRenderer.Objects;

namespace Simple3dRenderer.Rendering
{
    public interface IFragmentProcessor<TProcessor, TState>
    where TProcessor : IFragmentProcessor<TProcessor, TState>
    where TState : IRasterizable
    {
        static abstract void ProcessFragment(
            ref TState state, int x, int y, float z, float fw0, float fw1, float fw2,
            Vertex v0, Vertex v1, Vertex v2, bool isMultithreaded);

    }

}