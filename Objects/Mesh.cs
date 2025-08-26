using System;
using System.Numerics;
using System.Collections.Generic;
using MathNet.Numerics.LinearAlgebra;
using Simple3dRenderer.Rendering;
using Simple3dRenderer.Textures;

namespace Simple3dRenderer.Objects
{
    public class Mesh : WorldObject
    {
        private bool isOpaque = true;
        public Texture? texture = null;

        public readonly List<Vertex> Vertexes = new();
        public readonly List<(int, int, int)> indices = new();

        public Vector3 LocalBoundsMin { get; private set; } = new(float.MaxValue);
        public Vector3 LocalBoundsMax { get; private set; } = new(float.MinValue);

        public Vector3 WorldBoundsMin { get; private set; } = new(float.MaxValue);
        public Vector3 WorldBoundsMax { get; private set; } = new(float.MinValue);

        public int VertexCount { get; private set; } = 0;
        public int TriangleCount { get; private set; } = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="Mesh"/> class
        /// and subscribes to transform update events to recalculate world bounds.
        /// </summary>
        public Mesh()
        {
            OnPositionUpdate += _ => RecalculateWorldBounds();
            OnRotationUpdate += _ => RecalculateWorldBounds();
            OnScaleUpdate += _ => RecalculateWorldBounds();
        }

        private int AddVertex(Vertex v)
        {
            Vertexes.Add(v);
            VertexCount++;

            var p = new Vector3(v.clipPosition.X, v.clipPosition.Y, v.clipPosition.Z);
            LocalBoundsMin = Vector3.Min(LocalBoundsMin, p);
            LocalBoundsMax = Vector3.Max(LocalBoundsMax, p);

            UpdateWorldBoundsForVertex(p);
            return Vertexes.Count - 1;
        }

        private void AddTriangle(int i0, int i1, int i2)
        {
            indices.Add((i0, i1, i2));
            TriangleCount++;
        }

        public void AddTriangle(Vertex v0, Vertex v1, Vertex v2)
        {
            int i0 = AddVertex(v0);
            int i1 = AddVertex(v1);
            int i2 = AddVertex(v2);
            AddTriangle(i0, i1, i2);

            isOpaque &= (v0.Color.a == 255 && v1.Color.a == 255 && v2.Color.a == 255);
        }

        private void UpdateWorldBoundsForVertex(Vector3 localPos)
        {
            Vector3 worldPos = Vector3.Transform(localPos, GetModelMatrix());
            WorldBoundsMin = Vector3.Min(WorldBoundsMin, worldPos);
            WorldBoundsMax = Vector3.Max(WorldBoundsMax, worldPos);
        }

        private void RecalculateWorldBounds()
        {
            WorldBoundsMin = new(float.MaxValue);
            WorldBoundsMax = new(float.MinValue);

            foreach (var v in Vertexes)
                UpdateWorldBoundsForVertex(new Vector3(v.clipPosition.X, v.clipPosition.Y, v.clipPosition.Z));
        }

        public Matrix4x4 GetModelMatrix()
        {
            Matrix4x4 scaleMatrix = Matrix4x4.CreateScale(Scale);
            Matrix4x4 rotationMatrix = Matrix4x4.CreateFromQuaternion(Rotation);
            Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(Position);
            // note row major layout, so this is doing scale then rotate then translate
            return scaleMatrix * rotationMatrix * translationMatrix;
        }

        public Matrix<float> GetVertexMatrix()
        {
            int n = Vertexes.Count;
            var m = Matrix<float>.Build.Dense(4, n);

            for (int i = 0; i < n; i++)
            {
                m[0, i] = Vertexes[i].clipPosition.X;
                m[1, i] = Vertexes[i].clipPosition.Y;
                m[2, i] = Vertexes[i].clipPosition.Z;
                m[3, i] = Vertexes[i].clipPosition.W;
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