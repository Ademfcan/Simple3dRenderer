using System.Numerics;

namespace Simple3dRenderer.Rendering
{
    public interface IPerspective
    {
        Matrix4x4 getViewMatrix();
        Matrix4x4 getProjectionMatrix();

        int getWidth();
        int getHeight();
    }
}