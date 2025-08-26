using Simple3dRenderer.Rendering;

namespace Simple3dRenderer.Objects
{
    public class Camera(int hRes, int vRes, float fovDegrees = 30, float nearPlane = 1e-2f, float farPlane = 1e4f) : Viewport(hRes, vRes, fovDegrees, nearPlane, farPlane)
    {
        // not much here at the moment...
    }
}
