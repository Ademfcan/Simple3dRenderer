using Simple3dRenderer.Objects;

namespace Simple3dRenderer.Rendering
{
    public struct DepthPrePassProcessor : IFragmentProcessor<DepthPrePassProcessor, FrameData>
    {
        public static void ProcessFragment(ref FrameData state, int x, int y, float z, float w0, float w1, float w2, Vertex v0, Vertex v1, Vertex v2, bool isMultithreaded)
        {
            state.depthBuffer[y * state.GetWidth() + x] = z; // Update depth buffer
        }

    }

}