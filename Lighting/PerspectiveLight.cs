using System.Numerics;
using Simple3dRenderer.Rendering;

namespace Simple3dRenderer.Objects
{
    public class PerspectiveLight : Viewport
    {
        public Vector3 Color { get; set; }
        public float Intensity { get; set; }
        public float Quadratic { get; set; }

        // Spotlight cone
        public float InnerCutoffCos { get; set; }
        public float OuterCutoffCos { get; set; }

        public PerspectiveLight(
            int hRes,
            int vRes,
            float fovDegrees,
            float nearPlane = 0.01f,
            float farPlane = 1e4f,
            Vector3? color = null,
            float intensity = 1f,
            float quadratic = 0f,
            float innerCutoffDegrees = 10f,
            float? outerCutoffDegrees = null
        ) : base(hRes, vRes, fovDegrees, nearPlane, farPlane)
        {
            Color = color ?? Vector3.One;
            Intensity = intensity;
            Quadratic = quadratic;

            InnerCutoffCos = MathF.Cos(MathF.PI * innerCutoffDegrees / 180f);
            OuterCutoffCos = MathF.Cos(MathF.PI * (outerCutoffDegrees ?? fovDegrees) / 180f);
        }
    }
}
