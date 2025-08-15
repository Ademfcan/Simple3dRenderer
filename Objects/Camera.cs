using System.Numerics;
using MathNet.Numerics.LinearAlgebra;
using Simple3dRenderer.Extensions;
using Simple3dRenderer.Rendering;

namespace Simple3dRenderer.Objects
{
    public class Camera : IPerspective
    {
        private readonly int HRes;
        private readonly int VRes;

        public Vector3 Position { get; private set; } = Vector3.Zero;
        public Quaternion Rotation { get; private set; } = Quaternion.Identity;
        public float FovDegrees { get; private set; }
        public float AspectRatio { get; private set; }
        public float NearClip { get; private set; }
        public float FarClip { get; private set; }

        private Matrix4x4? cachedViewMatrix;
        private Matrix4x4? cachedProjectionMatrix;
        private Matrix<float>? cachedWToC;

        public Camera(
            int hRes, int vRes,
            float fovDegrees,
            float nearClip = 0.1f,
            float farClip = 1e4f,
            Vector3? position = null,
            Quaternion? rotation = null)
        {
            HRes = hRes;
            VRes = vRes;
            AspectRatio = (float)HRes / VRes;

            FovDegrees = fovDegrees;
            NearClip = nearClip;
            FarClip = farClip;

            Position = position ?? Vector3.Zero;
            Rotation = rotation ?? Quaternion.Identity;

            InvalidateMatrices();
        }

        public void SetPosition(Vector3 pos)
        {
            Position = pos;
            cachedViewMatrix = null;
            cachedWToC = null;
        }

        public void SetRotation(Quaternion rot)
        {
            Rotation = rot;
            cachedViewMatrix = null;
            cachedWToC = null;
        }

        public void SetFieldOfView(float fovDegrees)
        {
            FovDegrees = fovDegrees;
            cachedProjectionMatrix = null;
            cachedWToC = null;
        }

        public void SetNearFarPlanes(float nearClip, float farClip)
        {
            NearClip = nearClip;
            FarClip = farClip;
            cachedProjectionMatrix = null;
            cachedWToC = null;
        }

        private void InvalidateMatrices()
        {
            cachedViewMatrix = null;
            cachedProjectionMatrix = null;
            cachedWToC = null;
        }

        private Matrix4x4 GetViewMatrix()
        {
            if (cachedViewMatrix == null)
            {
                // Define the "forward" direction based on the camera's rotation.
                Vector3 forward = Vector3.Transform(-Vector3.UnitZ, Rotation);
                Vector3 target = Position + forward;

                // Define the "up" direction based on the camera's rotation.
                // In a right-handed system, up is +Y.
                Vector3 up = Vector3.Transform(Vector3.UnitY, Rotation);
                
                // Create the view matrix using the built-in LookAt function.
                cachedViewMatrix = Matrix4x4.CreateLookAt(Position, target, up);
            }
            return cachedViewMatrix.Value;
        }

        private Matrix4x4 GetProjectionMatrix()
        {
            if (cachedProjectionMatrix == null)
            {
                cachedProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
                    MathF.PI * FovDegrees / 180f,
                    AspectRatio,
                    NearClip,
                    FarClip);
            }
            return cachedProjectionMatrix.Value;
        }

        public Matrix<float> getWToC()
        {
            if (cachedWToC == null)
            {
                cachedWToC = GetProjectionMatrix().ToMathNet() * GetViewMatrix().ToMathNet();
            }
            return cachedWToC;
        }

        public int getWidth() => HRes;
        public int getHeight() => VRes;
    }
}
