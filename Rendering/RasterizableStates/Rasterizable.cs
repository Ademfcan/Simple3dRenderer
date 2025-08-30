using Simple3dRenderer.Lighting;
using Simple3dRenderer.Objects;

namespace Simple3dRenderer.Rendering
{
    public interface IRasterizable
    {
        int GetWidth();
        int GetHeight();

        /// <summary>
        /// Resets the rasterizer to its initial state.
        /// </summary>
        void Reset();

        /// <summary>
        /// Initializes the frame with the given scene, shadow maps, and lights.
        /// </summary>
        void InitFrame(Scene scene, List<DeepShadowMap> shadowMaps, List<PerspectiveLight> lights);
    }

    public interface ITiledRasterizable<T> : IRasterizable where T : ITiledRasterizable<T>
    {
        /// <summary>
        /// Provides direct access to the depth buffer for rasterizer optimizations, if one exists.
        /// Returns null if the state does not use a traditional depth buffer.
        /// </summary>
        float[]? depthBuffer { get; }

        /// <summary>
        /// Creates a "deep enough" copy of the state for a single thread to use.
        /// Called once per thread; result is reused across frames.
        /// </summary>
        T CreateThreadLocalState(int tileWidth, int tileHeight);

        /// <summary>
        /// Merges the results from a completed tile's local state back into the main state.
        /// Also clears the local tile's buffers by copying the initial state from the main buffers,
        /// preparing it for the next frame.
        /// </summary>
        void MergeTile(T tileState, int tileMinX, int tileMinY);

        /// <summary>
        /// Synchronizes per-frame state with another instance.
        /// </summary>
        void SyncPerFrameState(T otherState);
    }
}
