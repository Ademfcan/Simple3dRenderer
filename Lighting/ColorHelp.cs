using SDL;
using System.Numerics;

namespace Simple3dRenderer.Lighting
{

    static class ColorLin
    {
        public static Vector3 FromSDL(SDL_Color c)
            => new Vector3(c.r / 255f, c.g / 255f, c.b / 255f);

        public static SDL_Color ToSDL(Vector3 v, byte a = 255)
        {
            v = Vector3.Clamp(v, Vector3.Zero, Vector3.One);
            return new SDL_Color
            {
                r = (byte)(v.X * 255f),
                g = (byte)(v.Y * 255f),
                b = (byte)(v.Z * 255f),
                a = a
            };
        }
    }
}