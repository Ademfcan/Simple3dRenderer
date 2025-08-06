using System.Numerics;
using SDL;

namespace Simple3dRenderer.Objects
{
    public static class MeshFactory
    {
        public static Mesh CreateCube(Vector3 size, SDL_Color? color = null)
        {
            var mesh = new Mesh();
            var c = color ?? new SDL_Color { r = 255, g = 255, b = 255, a = 255 };

            Vector3[] faceNormals = new Vector3[]
            {
                new( 0,  0,  -1), // Front
                new( 0,  0, 1), // Back
                new( 0,  1,  0), // Top
                new( 0, -1,  0), // Bottom
                new( 1,  0,  0), // Right
                new(-1,  0,  0), // Left
            };

            Vector3 half = size * 0.5f;

            void AddFace(Vector3 normal)
            {
                // Pick a world-space "up" vector that's not parallel to the normal
                Vector3 arbitrary = MathF.Abs(Vector3.Dot(normal, Vector3.UnitY)) < 0.99f
                    ? Vector3.UnitY
                    : Vector3.UnitZ;

                Vector3 tangent = Vector3.Normalize(Vector3.Cross(arbitrary, normal));
                Vector3 bitangent = Vector3.Normalize(Vector3.Cross(normal, tangent));

                Vector3 center = -Vector3.Multiply(normal, half);

                Vector3 topLeft     = center - tangent * half + bitangent * half;
                Vector3 topRight    = center + tangent * half + bitangent * half;
                Vector3 bottomRight = center + tangent * half - bitangent * half;
                Vector3 bottomLeft  = center - tangent * half - bitangent * half;

                Vector2 uv00 = new(0, 0);
                Vector2 uv10 = new(1, 0);
                Vector2 uv11 = new(1, 1);
                Vector2 uv01 = new(0, 1);

                Vertex v0 = new(topLeft, c, uv00, normal);
                Vertex v1 = new(topRight, c, uv10, normal);
                Vertex v2 = new(bottomRight, c, uv11, normal);
                Vertex v3 = new(bottomLeft, c, uv01, normal);

                mesh.AddTriangle(v0, v1, v2); // Triangle 1
                mesh.AddTriangle(v0, v2, v3); // Triangle 2
            }

            foreach (var normal in faceNormals)
                AddFace(normal);

            return mesh;
        }

    }
}