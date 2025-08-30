using SDL;

namespace Simple3dRenderer.Rendering
{
    public static class PostProcessing
    {
        // Helper to calculate the perceived brightness of a color.
        private static float Luminance(SDL_Color color)
        {
            // Standard formula for converting sRGB to Luminance
            return (color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f) / 255.0f;
        }

        public static SDL_Color[] ApplyFXAA(SDL_Color[] sourceBuffer, int width, int height, float edgeThreshold = 0.075f)
        {
            var resultBuffer = new SDL_Color[sourceBuffer.Length];

            // This is a user-tunable value. Higher means less blurring, lower means more.
            // A value between 0.1 and 0.2 is usually a good starting point.

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int index = y * width + x;

                    // 1. Sample the center pixel and its neighbors
                    SDL_Color colorCenter = sourceBuffer[index];
                    float lumaCenter = Luminance(colorCenter);
                    float lumaNorth = Luminance(sourceBuffer[index - width]);
                    float lumaSouth = Luminance(sourceBuffer[index + width]);
                    float lumaWest = Luminance(sourceBuffer[index - 1]);
                    float lumaEast = Luminance(sourceBuffer[index + 1]);

                    // 2. Detect the contrast range
                    float lumaMin = Math.Min(lumaCenter, Math.Min(lumaNorth, Math.Min(lumaSouth, Math.Min(lumaWest, lumaEast))));
                    float lumaMax = Math.Max(lumaCenter, Math.Max(lumaNorth, Math.Max(lumaSouth, Math.Max(lumaWest, lumaEast))));
                    float lumaRange = lumaMax - lumaMin;

                    // 3. Check if this pixel is on an edge
                    if (lumaRange > edgeThreshold)
                    {
                        // This is a simplified blend. A full FXAA implementation has more
                        // complex logic to determine the edge direction for a better blend.
                        // But for a simple start, averaging with neighbors works well.
                        int r = (colorCenter.r + sourceBuffer[index - 1].r + sourceBuffer[index + 1].r + sourceBuffer[index - width].r + sourceBuffer[index + width].r) / 5;
                        int g = (colorCenter.g + sourceBuffer[index - 1].g + sourceBuffer[index + 1].g + sourceBuffer[index - width].g + sourceBuffer[index + width].g) / 5;
                        int b = (colorCenter.b + sourceBuffer[index - 1].b + sourceBuffer[index + 1].b + sourceBuffer[index - width].b + sourceBuffer[index + width].b) / 5;

                        resultBuffer[index] = new SDL_Color { r = (byte)r, g = (byte)g, b = (byte)b, a = colorCenter.a };
                    }
                    else
                    {
                        // 4. If not on an edge, just copy the original pixel
                        resultBuffer[index] = colorCenter;
                    }
                }
            }

            return resultBuffer;
        }
    }
}
