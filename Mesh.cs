using System.Numerics;
using MathNet.Numerics.LinearAlgebra;

namespace Simple3dRenderer
{
    public class Mesh
    {
        public Vector3 Position = Vector3.Zero;
        public Quaternion Rotation = Quaternion.Identity;
        public Vector3 Scale = new(1, 1, 1);

        public readonly List<Vector3> vertices = [];
        public readonly List<(int, int, int)> indices = [];

        // Adds a vertex and returns its index
        private int AddVertex(Vector3 v)
        {
            // todo: handle duplicates
            vertices.Add(v);
            return vertices.Count - 1;
        }

        // Adds a triangle by indices of vertices
        private void AddTriangle(int i0, int i1, int i2)
        {
            indices.Add((i0, i1, i2));
        }

        public void AddTriangle(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            int i0 = AddVertex(v0);
            int i1 = AddVertex(v1);
            int i2 = AddVertex(v2);
            AddTriangle(i0, i1, i2);
        }


        public Matrix4x4 GetModelMatrix()
        {

            Console.WriteLine(Scale);
            Console.WriteLine(Rotation);
            Console.WriteLine(Position);

            // Create scale matrix
            Matrix4x4 scaleMatrix = Matrix4x4.CreateScale(Scale);

            // Create rotation matrix from quaternion
            Matrix4x4 rotationMatrix = Matrix4x4.CreateFromQuaternion(Rotation);

            // Create translation matrix
            Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(Position);

            // Combine: Scale → Rotate → Translate
            Matrix4x4 modelMatrix = translationMatrix * rotationMatrix * scaleMatrix;

            return modelMatrix;
        }

        // Returns a 4xN matrix where each column is a Vector4 (x, y, z, w)
        public Matrix<float> GetVertexMatrix(float w = 1.0f)
        {
            int n = vertices.Count;
            var m = Matrix<float>.Build.Dense(4, n);

            for (int i = 0; i < n; i++)
            {
                m[0, i] = vertices[i].X;
                m[1, i] = vertices[i].Y;
                m[2, i] = vertices[i].Z;
                m[3, i] = w;
            }

            return m;
        }
    }

}

