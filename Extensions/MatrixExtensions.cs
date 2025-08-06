using System.Numerics;
using MathNet.Numerics.LinearAlgebra;

namespace Simple3dRenderer.Extensions
{
    public static class MatrixExtensions
    {
        // NOTE: this flips the matrix from row major to column major layout. this means A*B*C (which in row major is A then B then C) will now be the more normal (C then B then A)
        public static Matrix<float> ToMathNet(this Matrix4x4 m)
        {
            return Matrix<float>.Build.DenseOfArray(new float[,]
            {
                // Column 0
                { m.M11, m.M21, m.M31, m.M41 },
                // Column 1
                { m.M12, m.M22, m.M32, m.M42 },
                // Column 2
                { m.M13, m.M23, m.M33, m.M43 },
                // Column 3
                { m.M14, m.M24, m.M34, m.M44 }
            });
        }

    }
}