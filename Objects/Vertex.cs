using System.Numerics;
using SDL;
using Simple3dRenderer.Extensions;


namespace Simple3dRenderer.Objects
{
    public struct Vertex
    {
        public Vector4 Position;
        public SDL_Color Color;       // e.g., RGB or any gradient
        public Vector2 UV;          // Texture coordinate (if needed)
        public Vector3 Normal;      // For lighting
                                    // Add more attributes as needed

        public Vertex(Vector3 position, SDL_Color color = default, Vector2 uv = default, Vector3 normal = default)
        {
            Position = new Vector4(position.X, position.Y, position.Z, 1); // homogenous coords
            Color = color;
            UV = uv;
            Normal = normal;
        }

        private Vertex(Vector4 position, SDL_Color color, Vector2 uv, Vector3 normal)
        {
            Position = position;
            Color = color;
            UV = uv;
            Normal = normal;
        }

        public static Vertex Lerp(Vertex a, Vertex b, float t)
        {
            return new Vertex(
                Vector4.Lerp(a.Position, b.Position, t),
                SDLColorExtensions.Lerp(a.Color, b.Color, t),
                Vector2.Lerp(a.UV, b.UV, t),
                Vector3.Lerp(a.Normal, b.Normal, t)
            );
        }

        public static Vertex Interpolate(Vertex v0, Vertex v1, Vertex v2, float w0, float w1, float w2)
        {
            return new Vertex(
                v0.Position * w0 + v1.Position * w1 + v2.Position * w2,
                SDLColorExtensions.Interpolate(v0.Color,v1.Color,v2.Color, w0, w1, w2),
                v0.UV * w0 + v1.UV * w1 + v2.UV * w2,
                Vector3.Normalize(v0.Normal * w0 + v1.Normal * w1 + v2.Normal * w2)
            );
        }
        
        public static Vector2 InterpolateUV(Vertex v0, Vertex v1, Vertex v2, float w0, float w1, float w2)
        {
            return v0.UV * w0 + v1.UV * w1 + v2.UV * w2;
        }

}

}
