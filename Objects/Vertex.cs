using System.Numerics;
using SDL;
using Simple3dRenderer.Extensions;

namespace Simple3dRenderer.Objects
{
    public struct Vertex
    {
        public Vector4 Position;
        public SDL_Color Color;       // e.g., RGB or any gradient
        public Vector2 UV;            // Texture coordinate - now public for proper interpolation
        public Vector3 Normal;        // For lighting

        // below only valid after pre clip init!

        public float invW;            // 1 / clip-space w
        public Vector2 uvOverW;       // UV multiplied by invW (for perspective correction)

        public Vector4 worldPosition;

        public Vector4[] lightClipSpaces;

        public Vertex(Vector3 position, SDL_Color color = default, Vector2 uv = default, Vector3 normal = default)
        {
            Position = new Vector4(position.X, position.Y, position.Z, 1f); // homogeneous coords
            Color = color;
            UV = uv;
            Normal = normal;

            invW = 1f;                   // default invW (before projection)
            uvOverW = uv * invW;
        }

        private Vertex(Vector4 position, SDL_Color color, Vector2 uv, Vector3 normal, float invW, Vector2 uvOverW, Vector4 worldPosition)
        {
            Position = position;
            Color = color;
            UV = uv;
            Normal = normal;
            this.invW = invW;
            this.uvOverW = uvOverW;
            this.worldPosition = worldPosition;
        }

        // Linear interpolation for clipping intersection: Properly handles UV interpolation
        public static Vertex Lerp(Vertex a, Vertex b, float t)
        {
            // Interpolate position in homogeneous coordinates
            Vector4 newPosition = Vector4.Lerp(a.Position, b.Position, t);
            Vector4 newWorldPosition = Vector4.Lerp(a.worldPosition, b.worldPosition, t);

            // Interpolate other attributes linearly
            SDL_Color newColor = SDLColorExtensions.Lerp(a.Color, b.Color, t);
            Vector2 newUV = Vector2.Lerp(a.UV, b.UV, t);
            Vector3 newNormal = Vector3.Lerp(a.Normal, b.Normal, t);

            // Create the interpolated vertex
            var result = new Vertex(
                newPosition,
                newColor,
                newUV,
                newNormal,
                0f, // temporary invW
                Vector2.Zero,
                newWorldPosition
            );

            // Now compute the correct perspective correction terms
            result.invW = 1.0f / newPosition.W;
            result.uvOverW = newUV * result.invW;

            return result;
        }

        public void PreClipInit(Vector4 clipPosition, Vector4 worldPosition)
        {
            Position = clipPosition;
            invW = 1 / clipPosition.W;
            uvOverW = UV * invW;
            this.worldPosition = worldPosition;
        }
    }
}