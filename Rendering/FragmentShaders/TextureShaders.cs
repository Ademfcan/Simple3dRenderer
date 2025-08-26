using System.Numerics;
using SDL;
using Simple3dRenderer.Textures;
using Simple3dRenderer.Objects;

namespace Simple3dRenderer.Shaders
{
    public struct TextureShader
    {
        public static SDL_Color getPixelColor(
            Vertex v0, Vertex v1, Vertex v2,
            float w0, float w1, float w2, Texture texture)
        {
            // interpolate uOverW and vOverW
            Vector2 uvOverW = w0 * v0.uvOverW + w1 * v1.uvOverW + w2 * v2.uvOverW;

            // interpolate invW
            float interpolatedInvW = w0 * v0.invW + w1 * v1.invW + w2 * v2.invW;

            // recover perspective-correct UV
            Vector2 uv = uvOverW / interpolatedInvW;

            // Clamp UV coordinates to [0, 1] range
            uv.X = Math.Clamp(uv.X, 0.0f, 1.0f);
            uv.Y = Math.Clamp(uv.Y, 0.0f, 1.0f);

            return SampleTextureFiltered(uv, texture);
        }

        private static SDL_Color SampleTextureFiltered(Vector2 uv, Texture texture)
        {
            // Convert UV to texture space
            float texX = uv.X * (texture.width - 1);
            float texY = (1.0f - uv.Y) * (texture.height - 1);

            // Get integer coordinates
            int x0 = (int)Math.Floor(texX);
            int y0 = (int)Math.Floor(texY);
            int x1 = Math.Min(x0 + 1, texture.width - 1);
            int y1 = Math.Min(y0 + 1, texture.height - 1);

            // Get fractional parts
            float fx = texX - x0;
            float fy = texY - y0;

            // Sample four neighboring pixels
            SDL_Color c00 = texture.pixels[y0, x0];
            SDL_Color c10 = texture.pixels[y0, x1];
            SDL_Color c01 = texture.pixels[y1, x0];
            SDL_Color c11 = texture.pixels[y1, x1];

            // Bilinear interpolation
            SDL_Color top = LerpColor(c00, c10, fx);
            SDL_Color bottom = LerpColor(c01, c11, fx);
            return LerpColor(top, bottom, fy);
        }

        private static SDL_Color LerpColor(SDL_Color a, SDL_Color b, float t)
        {
            return new SDL_Color
            {
                r = (byte)(a.r + (b.r - a.r) * t),
                g = (byte)(a.g + (b.g - a.g) * t),
                b = (byte)(a.b + (b.b - a.b) * t),
                a = (byte)(a.a + (b.a - a.a) * t)
            };
        }

    }
}