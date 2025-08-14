using System.Numerics;
using MathNet.Numerics.LinearAlgebra;

namespace Simple3dRenderer.Lighting
{
    public static class LightSpaceHelper
    {
        /// <summary>
        /// Converts a world position to light screen space.
        /// Returns null if position is clipped (outside light frustum).
        /// </summary>
        public static Vector3? WorldToLightScreen(Vector4 worldPos, Matrix<float> wtoc, int screenWidth, int screenHeight, bool keepOutOfBounds = false)
        {
            // Transform to clip space
            var clipPos = TransformToLightClip(worldPos, wtoc);

            // W-clipping (homogeneous clip space bounds)
            if (!keepOutOfBounds && !IsInsideClipBounds(clipPos))
                return null;

            // Convert to NDC
            var ndc = ClipToNdc(clipPos);
            return NdcToScreen(ndc, screenWidth, screenHeight);
        }

        private static MathNet.Numerics.LinearAlgebra.Vector<float> TransformToLightClip(Vector4 worldPos, Matrix<float> wtoc)
        {
            var worldVec = MathNet.Numerics.LinearAlgebra.Vector<float>.Build.DenseOfArray(new float[] { worldPos.X, worldPos.Y, worldPos.Z, worldPos.W });
            return wtoc * worldVec;
        }

        private static bool IsInsideClipBounds(MathNet.Numerics.LinearAlgebra.Vector<float> clip)
        {
            float X = clip[0], Y = clip[1], Z = clip[2], W = clip[3];
            return !(X <= -W || X >= W || Y <= -W || Y >= W || Z <= -W || Z >= W);
        }

        private static Vector3 ClipToNdc(MathNet.Numerics.LinearAlgebra.Vector<float> clip)
        {
            float X = clip[0], Y = clip[1], Z = clip[2], W = clip[3];
            return new Vector3(X / W, Y / W, Z / W);
        }

        private static bool IsInsideNdcBounds(Vector3 ndc)
        {
            return ndc.X >= -1f && ndc.X <= 1f &&
                   ndc.Y >= -1f && ndc.Y <= 1f &&
                   ndc.Z >= -1f && ndc.Z <= 1f;
        }

        private static Vector3 NdcToScreen(Vector3 ndc, int width, int height)
        {
            float sx = (ndc.X + 1) * 0.5f * width;
            float sy = (1- ndc.Y) * 0.5f * height; // flip Y for screen space
            float sz = ndc.Z; // depth in [0,1]
            return new Vector3(sx, sy, sz);
        }
    }
}