using Simple3dRenderer.Objects;

namespace Simple3dRenderer.Rendering
{
    public struct DepthPrePassProcessor : IFragmentProcessor<DepthPrePassProcessor, FrameData>
    {
        public static void ProcessFragment(ref FrameData state, int x, int y, float z, float w0, float w1, float w2, Vertex v0, Vertex v1, Vertex v2, bool isMultithreaded)
        {
            if (isMultithreaded)
            {

                float currentDepth;
                // Atomically check and set the depth value. Loop to handle race conditions.
                do
                {
                    currentDepth = Volatile.Read(ref state.depthBuffer[y, x]);
                    if (z >= currentDepth) return; // Failed depth test
                } while (Interlocked.CompareExchange(ref state.depthBuffer[y, x], z, currentDepth) != currentDepth);

            }
            // single threaded with condition
            else if (z < state.depthBuffer[y, x])
            {
                
                state.depthBuffer[y, x] = z; // Update depth buffer
                
            }
        }

    }

}