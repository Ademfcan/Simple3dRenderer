using SDL;
using MathNet.Numerics.LinearAlgebra;
using Simple3dRenderer.Rendering;

namespace Simple3dRenderer.Lighting
{
    public interface ILight : IPerspective
    {
        public Matrix<float> getWToC();
        // public SDL_Color getLightColor();
    }
}