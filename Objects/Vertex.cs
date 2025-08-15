using System.Numerics;
using SDL;
using Simple3dRenderer.Extensions;

namespace Simple3dRenderer.Objects
{
    public struct Vertex
    {
        public Vector4 clipPosition;
        public SDL_Color Color;       // e.g., RGB or any gradient
        public Vector2 UV;            // Texture coordinate
        public Vector3 Normal;        // For lighting

        // below only valid after pre clip init!
        public float invW;
        public Vector4 worldPosition;
        public Vector4 worldPositionOverW;

        public Vector2 uvOverW;       // UV multiplied by invW (for perspective correction)
        public Vector3 normalOverW;   // Normal multiplied by invW

        // below only valid after add clip light spaces

        public Vector4[] lightClipSpaces;
        public Vector4[] lightClipSpacesOverW;

        public Vertex(Vector3 position, SDL_Color color = default, Vector2 uv = default, Vector3 normal = default)
        {
            clipPosition = new Vector4(position.X, position.Y, position.Z, 1f);
            Color = color;
            UV = uv;
            Normal = normal;
        }

        private Vertex(Vector4 position, SDL_Color color, Vector2 uv, Vector3 normal, float invW, Vector2 uvOverW, Vector3 normalOverW,
                       Vector4 worldPosition, Vector4 worldPositionOverW)
        {
            clipPosition = position;
            Color = color;
            UV = uv;
            Normal = normal;

            this.invW = invW;
            this.uvOverW = uvOverW;
            this.normalOverW = normalOverW;

            this.worldPosition = worldPosition;
            this.worldPositionOverW = worldPositionOverW;
        }

        // called after pre clip init (onlt in clipping)
        public static Vertex Lerp(Vertex a, Vertex b, float t)
        {
            Vector4 newPosition = Vector4.Lerp(a.clipPosition, b.clipPosition, t);
            Vector4 newWorldPosition = Vector4.Lerp(a.worldPosition, b.worldPosition, t);
            SDL_Color newColor = SDLColorExtensions.Lerp(a.Color, b.Color, t);
            Vector2 newUV = Vector2.Lerp(a.UV, b.UV, t);
            Vector3 newNormal = Vector3.Lerp(a.Normal, b.Normal, t);

            float invW = 1.0f / newPosition.W;

            Vector2 newUVOverW = newUV * invW;
            Vector3 newNormalOverW = newNormal * invW;
            Vector4 newWorldPositionOverW = newWorldPosition * invW;

            return new Vertex(
                newPosition,
                newColor,
                newUV,
                newNormal,
                invW,
                newUVOverW,
                newNormalOverW,
                newWorldPosition,
                newWorldPositionOverW
            );
        }

        public void PreClipInit(Vector4 clipPosition, Vector4 worldPosition)
        {
            this.worldPosition = worldPosition;
            this.clipPosition = clipPosition;

            invW = 1 / clipPosition.W;

            uvOverW = UV * invW;
            normalOverW = Normal * invW;
            worldPositionOverW = worldPosition * invW;


        }


        // called AFTER lerp and pre clip init
        public void setLightClipSpaces(Vector4[] clipSpaces)
        {
            this.lightClipSpaces = clipSpaces;
            lightClipSpacesOverW = new Vector4[lightClipSpaces.Length];
            for (int i = 0; i < lightClipSpaces.Length; i++)
            {
                lightClipSpacesOverW[i] = lightClipSpaces[i] * invW;
            }

        }
    }
}
