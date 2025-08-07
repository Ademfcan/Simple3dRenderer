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

            Vector3 half = size * 0.5f;

            // Define UV coordinates for consistent mapping
            Vector2 uv00 = new(0, 0); // Bottom-left
            Vector2 uv10 = new(1, 0); // Bottom-right
            Vector2 uv11 = new(1, 1); // Top-right
            Vector2 uv01 = new(0, 1); // Top-left

            // Front face (Z+)
            var frontNormal = new Vector3(0, 0, 1);
            var frontV0 = new Vertex(new Vector3(-half.X, -half.Y, half.Z), c, uv00, frontNormal); // Bottom-left
            var frontV1 = new Vertex(new Vector3(half.X, -half.Y, half.Z), c, uv10, frontNormal);  // Bottom-right
            var frontV2 = new Vertex(new Vector3(half.X, half.Y, half.Z), c, uv11, frontNormal);   // Top-right
            var frontV3 = new Vertex(new Vector3(-half.X, half.Y, half.Z), c, uv01, frontNormal);  // Top-left
            mesh.AddTriangle(frontV0, frontV1, frontV2);
            mesh.AddTriangle(frontV0, frontV2, frontV3);

            // Back face (Z-)
            var backNormal = new Vector3(0, 0, -1);
            var backV0 = new Vertex(new Vector3(half.X, -half.Y, -half.Z), c, uv00, backNormal);   // Bottom-left (flipped)
            var backV1 = new Vertex(new Vector3(-half.X, -half.Y, -half.Z), c, uv10, backNormal);  // Bottom-right (flipped)
            var backV2 = new Vertex(new Vector3(-half.X, half.Y, -half.Z), c, uv11, backNormal);   // Top-right (flipped)
            var backV3 = new Vertex(new Vector3(half.X, half.Y, -half.Z), c, uv01, backNormal);    // Top-left (flipped)
            mesh.AddTriangle(backV0, backV1, backV2);
            mesh.AddTriangle(backV0, backV2, backV3);

            // Right face (X+)
            var rightNormal = new Vector3(1, 0, 0);
            var rightV0 = new Vertex(new Vector3(half.X, -half.Y, half.Z), c, uv00, rightNormal);   // Bottom-left
            var rightV1 = new Vertex(new Vector3(half.X, -half.Y, -half.Z), c, uv10, rightNormal);  // Bottom-right
            var rightV2 = new Vertex(new Vector3(half.X, half.Y, -half.Z), c, uv11, rightNormal);   // Top-right
            var rightV3 = new Vertex(new Vector3(half.X, half.Y, half.Z), c, uv01, rightNormal);    // Top-left
            mesh.AddTriangle(rightV0, rightV1, rightV2);
            mesh.AddTriangle(rightV0, rightV2, rightV3);

            // Left face (X-)
            var leftNormal = new Vector3(-1, 0, 0);
            var leftV0 = new Vertex(new Vector3(-half.X, -half.Y, -half.Z), c, uv00, leftNormal);   // Bottom-left
            var leftV1 = new Vertex(new Vector3(-half.X, -half.Y, half.Z), c, uv10, leftNormal);    // Bottom-right
            var leftV2 = new Vertex(new Vector3(-half.X, half.Y, half.Z), c, uv11, leftNormal);     // Top-right
            var leftV3 = new Vertex(new Vector3(-half.X, half.Y, -half.Z), c, uv01, leftNormal);    // Top-left
            mesh.AddTriangle(leftV0, leftV1, leftV2);
            mesh.AddTriangle(leftV0, leftV2, leftV3);

            // Top face (Y+)
            var topNormal = new Vector3(0, 1, 0);
            var topV0 = new Vertex(new Vector3(-half.X, half.Y, half.Z), c, uv00, topNormal);    // Bottom-left
            var topV1 = new Vertex(new Vector3(half.X, half.Y, half.Z), c, uv10, topNormal);     // Bottom-right
            var topV2 = new Vertex(new Vector3(half.X, half.Y, -half.Z), c, uv11, topNormal);    // Top-right
            var topV3 = new Vertex(new Vector3(-half.X, half.Y, -half.Z), c, uv01, topNormal);   // Top-left
            mesh.AddTriangle(topV0, topV1, topV2);
            mesh.AddTriangle(topV0, topV2, topV3);

            // Bottom face (Y-)
            var bottomNormal = new Vector3(0, -1, 0);
            var bottomV0 = new Vertex(new Vector3(-half.X, -half.Y, -half.Z), c, uv00, bottomNormal); // Bottom-left
            var bottomV1 = new Vertex(new Vector3(half.X, -half.Y, -half.Z), c, uv10, bottomNormal);  // Bottom-right
            var bottomV2 = new Vertex(new Vector3(half.X, -half.Y, half.Z), c, uv11, bottomNormal);   // Top-right
            var bottomV3 = new Vertex(new Vector3(-half.X, -half.Y, half.Z), c, uv01, bottomNormal);  // Top-left
            mesh.AddTriangle(bottomV0, bottomV1, bottomV2);
            mesh.AddTriangle(bottomV0, bottomV2, bottomV3);

            return mesh;
        }

        // Alternative simpler cube generation for testing
        public static Mesh CreateSimpleCube(float size = 1.0f, SDL_Color? color = null)
        {
            var mesh = new Mesh();
            var c = color ?? new SDL_Color { r = 255, g = 255, b = 255, a = 255 };
            float s = size * 0.5f;

            // Vertices (8 corners of a cube)
            Vector3[] positions = {
                new Vector3(-s, -s, -s), // 0
                new Vector3( s, -s, -s), // 1
                new Vector3( s,  s, -s), // 2
                new Vector3(-s,  s, -s), // 3
                new Vector3(-s, -s,  s), // 4
                new Vector3( s, -s,  s), // 5
                new Vector3( s,  s,  s), // 6
                new Vector3(-s,  s,  s), // 7
            };

            // Face definitions: vertex indices and normals
            var faces = new[]
            {
                // Front face
                new { indices = new[] { 4, 5, 6, 7 }, normal = new Vector3(0, 0, 1) },
                // Back face  
                new { indices = new[] { 1, 0, 3, 2 }, normal = new Vector3(0, 0, -1) },
                // Right face
                new { indices = new[] { 5, 1, 2, 6 }, normal = new Vector3(1, 0, 0) },
                // Left face
                new { indices = new[] { 0, 4, 7, 3 }, normal = new Vector3(-1, 0, 0) },
                // Top face
                new { indices = new[] { 7, 6, 2, 3 }, normal = new Vector3(0, 1, 0) },
                // Bottom face
                new { indices = new[] { 0, 1, 5, 4 }, normal = new Vector3(0, -1, 0) },
            };

            Vector2[] uvs = { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };

            foreach (var face in faces)
            {
                var v0 = new Vertex(positions[face.indices[0]], new SDL_Color{r = 10, g = 150, b = 160, a = 255}, uvs[0], face.normal);
                var v1 = new Vertex(positions[face.indices[1]], new SDL_Color{r = 100, g = 150, b = 160, a = 255}, uvs[1], face.normal);
                var v2 = new Vertex(positions[face.indices[2]], new SDL_Color{r = 10, g = 2, b = 60, a = 255}, uvs[2], face.normal);
                var v3 = new Vertex(positions[face.indices[3]], new SDL_Color{r = 150, g = 0, b = 160, a = 255}, uvs[3], face.normal);

                // Two triangles per face
                mesh.AddTriangle(v0, v1, v2);
                mesh.AddTriangle(v0, v2, v3);
            }

            return mesh;
        }
    }
}