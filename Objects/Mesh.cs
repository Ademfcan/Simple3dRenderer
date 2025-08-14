using System.Numerics;
using MathNet.Numerics.LinearAlgebra;
using Simple3dRenderer.Textures;

namespace Simple3dRenderer.Objects
{
    public class Mesh
    {
        public Vector3 Position { get; private set; } = Vector3.Zero;
        public Quaternion Rotation { get; private set; } = Quaternion.Identity;
        public Vector3 Scale { get; private set; } = new(1, 1, 1);

        bool isOpaque = true;
        bool isLocked = false;

        public void Lock() => isLocked = true;
        public void Unlock() => isLocked = false;


        public Texture? texture = null;

        public readonly List<Vertex> Vertexes = new();
        public readonly List<(int, int, int)> indices = new();

        // Local-space bounds (before transform)
        public Vector3 LocalBoundsMin { get; private set; } = new(float.MaxValue);
        public Vector3 LocalBoundsMax { get; private set; } = new(float.MinValue);

        // World-space bounds (updated when triangles are added)
        public Vector3 WorldBoundsMin { get; private set; } = new(float.MaxValue);
        public Vector3 WorldBoundsMax { get; private set; } = new(float.MinValue);

        public int VertexCount { get; private set; } = 0;
        public int TriangleCount { get; private set; } = 0;

        // Adds a vertex and returns its index
        private int AddVertex(Vertex v)
        {
            Vertexes.Add(v);
            VertexCount++;

            // Update local bounds
            var p = new Vector3(v.Position.X, v.Position.Y, v.Position.Z);
            LocalBoundsMin = Vector3.Min(LocalBoundsMin, p);
            LocalBoundsMax = Vector3.Max(LocalBoundsMax, p);

            // Update world bounds incrementally
            UpdateWorldBoundsForVertex(p);

            return Vertexes.Count - 1;
        }

        // Adds a triangle by indices of vertices
        private void AddTriangle(int i0, int i1, int i2)
        {
            indices.Add((i0, i1, i2));
            TriangleCount++;
        }

        public void AddTriangle(Vertex v0, Vertex v1, Vertex v2)
        {
            if (isLocked)
                throw new InvalidOperationException("Mesh is locked and cannot be modified.");

            int i0 = AddVertex(v0);
            int i1 = AddVertex(v1);
            int i2 = AddVertex(v2);
            AddTriangle(i0, i1, i2);

            isOpaque &= (v0.Color.a == 255 && v1.Color.a == 255 && v2.Color.a == 255);
        }


        private void UpdateWorldBoundsForVertex(Vector3 localPos)
        {
            // Apply model transform to get world-space vertex position
            Vector3 worldPos = Vector3.Transform(localPos, GetModelMatrix());
            WorldBoundsMin = Vector3.Min(WorldBoundsMin, worldPos);
            WorldBoundsMax = Vector3.Max(WorldBoundsMax, worldPos);
        }

        private void RecalculateWorldBounds()
        {
            WorldBoundsMin = new(float.MaxValue);
            WorldBoundsMax = new(float.MinValue);

            foreach (var v in Vertexes)
                UpdateWorldBoundsForVertex(new Vector3(v.Position.X, v.Position.Y, v.Position.Z));
        }

        public void SetPosition(Vector3 Position)
        {
            if (isLocked)
                throw new InvalidOperationException("Mesh is locked and cannot be modified.");
            this.Position = Position;
            RecalculateWorldBounds();
        }

        public void SetRotation(Quaternion Rotation)
        {
            if (isLocked)
                throw new InvalidOperationException("Mesh is locked and cannot be modified.");

            this.Rotation = Rotation;
            RecalculateWorldBounds();
        }

        public void SetScale(Vector3 Scale)
        {
            if (isLocked)
                throw new InvalidOperationException("Mesh is locked and cannot be modified.");
            this.Scale = Scale;
            RecalculateWorldBounds();
        }



        public Matrix4x4 GetModelMatrix()
        {
            Matrix4x4 scaleMatrix = Matrix4x4.CreateScale(Scale);
            Matrix4x4 rotationMatrix = Matrix4x4.CreateFromQuaternion(Rotation);
            Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(Position);

            return scaleMatrix * rotationMatrix * translationMatrix;
        }

        // Returns a 4xN matrix where each column is a Vector4 (x, y, z, w)
        public Matrix<float> GetVertexMatrix()
        {
            int n = Vertexes.Count;
            var m = Matrix<float>.Build.Dense(4, n);

            for (int i = 0; i < n; i++)
            {
                m[0, i] = Vertexes[i].Position.X;
                m[1, i] = Vertexes[i].Position.Y;
                m[2, i] = Vertexes[i].Position.Z;
                m[3, i] = Vertexes[i].Position.W;
            }

            return m;
        }



        public bool IsOpaque()
        {
            if (texture != null)
                return texture.isOpaque;
            return isOpaque;
        }
    }
}
