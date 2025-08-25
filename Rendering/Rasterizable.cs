using Simple3dRenderer.Lighting;
using Simple3dRenderer.Objects;

namespace Simple3dRenderer.Rendering
{
    public interface IRasterizable
    {
        public int getWidth();
        public int getHeight();

        void Reset();

        void InitFrame(Scene scene, List<DeepShadowMap> shadowMaps, List<PerspectiveLight> lights);
    }

    public interface ITiledRasterizable<T> : IRasterizable where T : ITiledRasterizable<T>
    {
        /// <summary>
        /// Method 1: Creates a "deep enough" copy of the state for a single thread to use.
        /// This is called once per thread and the result is reused across frames.
        /// </summary>
        T CreateThreadLocalState(int tileWidth, int tileHeight);

        /// <summary>
        /// Method 2: Merges the results from a completed tile's local state back into the main state.
        /// This method is now also responsible for "clearing" the local tile's buffers by copying
        /// the initial state from the main buffers, effectively preparing it for the next frame's use.
        /// </summary>
        void MergeTile(T tileState, int tileMinX, int tileMinY);

        void SyncPerFrameState(T otherState);
    }
}