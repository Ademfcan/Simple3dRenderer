using System.Numerics;
using MathNet.Numerics.LinearAlgebra;

namespace Simple3dRenderer.Rendering
{
    public interface IPerspective
    {
        Matrix<float> getWToC();
        int getWidth();
        int getHeight();
    }
}