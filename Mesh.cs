using System.Numerics;
using MathNet.Numerics.LinearAlgebra;

namespace Simple3dRenderer
{
    public class Mesh
    {
        public Vector3 Position = Vector3.Zero;
        public Quaternion Rotation = Quaternion.Identity;
        public Vector3 Scale = new(1, 1, 1);

        public readonly List<Vertex> originalVertexes = [];
        public readonly List<(int, int, int)> indices = [];

        // Adds a vertex and returns its index
        private int AddVertex(Vertex v)
        {
            originalVertexes.Add(v);
            return originalVertexes.Count - 1;
        }

        // Adds a triangle by indices of vertices
        private void AddTriangle(int i0, int i1, int i2)
        {
            indices.Add((i0, i1, i2));
        }

        public void AddTriangle(Vertex v0, Vertex v1, Vertex v2)
        {
            int i0 = AddVertex(v0);
            int i1 = AddVertex(v1);
            int i2 = AddVertex(v2);
            AddTriangle(i0, i1, i2);
        }

        // public void AddTriangle(Vertex v0, Vertex v1, Vertex v2, Vector3 referenceNormal)
        // {
        //     EnsureCounterClockwise(ref v0, ref v1, ref v2, referenceNormal);
        //     AddTriangle(v0, v1, v2);
        // }


        public Matrix4x4 GetModelMatrix()
        {

            // Console.WriteLine(Scale);
            // Console.WriteLine(Rotation);
            // Console.WriteLine(Position);

            // Create scale matrix
            Matrix4x4 scaleMatrix = Matrix4x4.CreateScale(Scale);

            // Create rotation matrix from quaternion
            Matrix4x4 rotationMatrix = Matrix4x4.CreateFromQuaternion(Rotation);

            // Create translation matrix
            Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(Position);

            // Combine: Scale → Rotate → Translate
            // NOTE: row major order so this is a correct SRT move
            Matrix4x4 modelMatrix = scaleMatrix * rotationMatrix * translationMatrix;

            return modelMatrix;
        }

        // Returns a 4xN matrix where each column is a Vector4 (x, y, z, w)
        public Matrix<float> GetVertexMatrix()
        {
            int n = originalVertexes.Count;
            var m = Matrix<float>.Build.Dense(4, n);

            for (int i = 0; i < n; i++)
            {
                m[0, i] = originalVertexes[i].Position.X;
                m[1, i] = originalVertexes[i].Position.Y;
                m[2, i] = originalVertexes[i].Position.Z;
                m[3, i] = originalVertexes[i].Position.W;
            }

            return m;
        }

        private static void EnsureCounterClockwise(ref Vector3 v1, ref Vector3 v2, ref Vector3 v3, Vector3 referenceNormal)
        {
            var edge1 = v2 - v1;
            var edge2 = v3 - v1;

            // Cross product of two 3D vectors
            var n1 = edge1[1] * edge2[2] - edge1[2] * edge2[1];
            var n2 = edge1[2] * edge2[0] - edge1[0] * edge2[2];
            var n3 = edge1[0] * edge2[1] - edge1[1] * edge2[0];
            var normal = new Vector3(n1, n2, n3);


            if (Vector3.Dot(normal, referenceNormal) < 0)
            {
                // Swap v2 and v3
                var temp = v2;
                v2 = v3;
                v3 = temp;
            }
        }
    }

}

