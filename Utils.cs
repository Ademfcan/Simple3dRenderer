using SDL;

namespace Simple3dRenderer
{
    public static class Utils
    {
        public static SDL_Color[,] RescaleFrameBuffer(SDL_Color[,] src, int destWidth, int destHeight)
        {
            if (src.GetLength(0) < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(src), "Src array must not be empty!");
            }

            int srcHeight = src.GetLength(0);
            int srcWidth = src.GetLength(1);

            float desttosrcH = srcHeight / destHeight;
            float desttosrcW = srcWidth / destWidth;


            SDL_Color[,] dest = new SDL_Color[srcHeight, srcWidth];



            for (int i = 0; i < dest.GetLength(0); i++)
            {
                for (int j = 0; j < dest.GetLength(1); j++)
                {
                    int destX = j, destY = i;
                    float srcX = desttosrcW * destX, srcY = desttosrcH * destY;

                    SDL_Color[] samples = new SDL_Color[4];

                    int floorX = (int)Math.Floor(srcX);
                    int floorY = (int)Math.Floor(srcY);

                    int cielX = Math.Min((int)Math.Ceiling(srcX), srcWidth - 1);
                    int cielY = Math.Min((int)Math.Ceiling(srcY), srcHeight - 1);

                    samples[0] = src[floorY, floorX];
                    samples[1] = src[floorY, cielX];
                    samples[2] = src[cielY, floorX];
                    samples[3] = src[cielY, cielX];

                    SDL_Color sampleAvg = Average(samples);

                    dest[destY, destX] = sampleAvg;

                }
            }

            return dest;

        }

        private static SDL_Color Average(SDL_Color[] colors)
        {
            int r = 0, g = 0, b = 0, a = 0;

            foreach (SDL_Color color in colors)
            {
                r += color.r;
                g += color.g;
                b += color.b;
                a += color.a;
            }

            int n = colors.Length;

            r /= n;
            g /= n;
            b /= n;
            a /= n;

            return new SDL_Color { r = (byte)r, g = (byte)g, b = (byte)b, a = (byte)a };
        }
    }
}