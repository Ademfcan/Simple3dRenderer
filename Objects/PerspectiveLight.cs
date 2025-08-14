using System.Numerics;
using MathNet.Numerics.LinearAlgebra;
using Simple3dRenderer.Extensions;
using Simple3dRenderer.Lighting;
using Simple3dRenderer.Rendering;

namespace Simple3dRenderer.Objects
{
    public class PerspectiveLight : ILight
    {
        public Vector3 Color;        // linear 0..1
        public float Intensity;      // scalar multiplier (e.g. 1.0 = default)
        // Simple attenuation: 1 / (1 + k * d^2)
        public float Quadratic;      // e.g. 0.02f..0.2f depending on scene scale

        // Spotlight cone (cosines stored to save per-fragment trig)
        public float InnerCutoffCos { get; private set; } = MathF.Cos(MathF.PI / 6f); // default 30° inner
        public float OuterCutoffCos { get; private set; } = MathF.Cos(MathF.PI / 4f); // default 45° outer

        private readonly int HRes;
        private readonly int VRes;

        public Vector3 Position { get; private set; } = Vector3.Zero;
        public Quaternion Rotation { get; private set; } = Quaternion.Identity;
        public Vector3 Direction { get; private set; } = Vector3.Transform(-Vector3.UnitZ, Quaternion.Identity);
        public float FieldOfViewDegrees { get; private set; } = 60f;  // vertical FOV
        public float AspectRatio { get; private set; } = 1f;
        public float NearPlane { get; private set; } = 0.1f;
        public float FarPlane { get; private set; } = 100f;

        private Matrix4x4? cachedViewMatrix;
        private Matrix4x4? cachedProjectionMatrix;

        private Matrix<float>? cachedWToC;

        public PerspectiveLight(
            int HRes, int VRes, 
            float fovDegrees, 
            float nearPlane = 0.01f, 
            float farPlane = 1e4f, 
            Vector3? color = null, 
            float intensity = 1f, 
            float quadratic = 0.02f,
            float innerCutoffDegrees = 30f,
            float outerCutoffDegrees = 45f)
        {
            this.HRes = HRes;
            this.VRes = VRes;
            FieldOfViewDegrees = fovDegrees;
            AspectRatio = (float)HRes / VRes;
            NearPlane = nearPlane;
            FarPlane = farPlane;

            this.Color = color ?? new Vector3(1f, 1f, 1f);
            this.Intensity = intensity;
            this.Quadratic = quadratic;

            SetCutoffAngles(innerCutoffDegrees, outerCutoffDegrees);
            UpdateMatrices();
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
            Direction = Vector3.Transform(-Vector3.UnitZ, rot);
            cachedViewMatrix = null;
            cachedWToC = null;
        }

        public void SetFieldOfView(float fovDegrees)
        {
            FieldOfViewDegrees = fovDegrees;
            cachedProjectionMatrix = null;
            cachedWToC = null;
        }

        public void SetNearFarPlanes(float nearPlane, float farPlane)
        {
            NearPlane = nearPlane;
            FarPlane = farPlane;
            cachedProjectionMatrix = null;
            cachedWToC = null;
        }

        public void SetCutoffAngles(float innerDegrees, float outerDegrees)
        {
            InnerCutoffCos = MathF.Cos(MathF.PI * innerDegrees / 180f);
            OuterCutoffCos = MathF.Cos(MathF.PI * outerDegrees / 180f);
        }

        public Matrix4x4 getViewMatrix()
        {
            if (cachedViewMatrix == null)
            {
                Matrix4x4 rotationMatrix = Matrix4x4.CreateFromQuaternion(Quaternion.Conjugate(Rotation));
                Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(-Position);

                // row-major multiplication order
                cachedViewMatrix = translationMatrix * rotationMatrix;
            }
            return cachedViewMatrix.Value;
        }

        public Matrix4x4 getProjectionMatrix()
        {
            if (cachedProjectionMatrix == null)
            {
                cachedProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
                    MathF.PI * FieldOfViewDegrees / 180f,
                    AspectRatio,
                    NearPlane,
                    FarPlane);
            }
            return cachedProjectionMatrix.Value;
        }

        private void UpdateMatrices()
        {
            cachedViewMatrix = null;
            cachedProjectionMatrix = null;
        }

        public int getWidth() => HRes;
        public int getHeight() => VRes;

        public Matrix<float> getWToC()
        {
            if (cachedWToC == null)
            {
                cachedWToC = getProjectionMatrix().ToMathNet() * getViewMatrix().ToMathNet();
            }
            return cachedWToC;
        }
    }
}
