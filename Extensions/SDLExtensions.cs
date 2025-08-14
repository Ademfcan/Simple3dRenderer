using SDL;

namespace Simple3dRenderer.Extensions
{
    public static class SDLColorExtensions
    {
        public static SDL_Color Lerp(SDL_Color a, SDL_Color b, float t)
        {
            return new SDL_Color
            {
                r = (byte)(a.r + (b.r - a.r) * t),
                g = (byte)(a.g + (b.g - a.g) * t),
                b = (byte)(a.b + (b.b - a.b) * t),
                a = (byte)(a.a + (b.a - a.a) * t)
            };
        }

        public static SDL_Color Interpolate(SDL_Color c1, SDL_Color c2, SDL_Color c3, float w1, float w2, float w3)
        {
            return new SDL_Color
            {
                r = (byte)MathF.Round(c1.r * w1 + c2.r * w2 + c3.r * w3),
                g = (byte)MathF.Round(c1.g * w1 + c2.g * w2 + c3.g * w3),
                b = (byte)MathF.Round(c1.b * w1 + c2.b * w2 + c3.b * w3),
                a = (byte)MathF.Round(c1.a * w1 + c2.a * w2 + c3.a * w3)
            };
        }

        public static SDL_Color ScaleWNoA(SDL_Color color, float scale)
        {
            return new SDL_Color
            {
                r = (byte)(color.r * scale),
                g = (byte)(color.g * scale),
                b = (byte)(color.b * scale),
                a = color.a
            };
        }

    }
}
