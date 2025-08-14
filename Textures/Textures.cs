using System.Runtime.InteropServices;
using SDL;

namespace Simple3dRenderer.Textures
{
    public class Texture(SDL_Color[,] pixels, bool isOpaque)
    {
        public int width = pixels.GetLength(1);
        public int height = pixels.GetLength(0);
        public bool isOpaque = isOpaque;

        public SDL_Color[,] pixels = pixels;
    }

    public static class TextureLoader
    {
        public static Texture LoadBMP(string path)
        {
            unsafe
            {
                SDL_Surface* rawSurface = SDL3.SDL_LoadBMP(path);
                if (rawSurface == null)
                    throw new Exception($"Failed to load texture: {SDL3.SDL_GetError()}");

                // Convert to a known consistent format
                SDL_Surface* surface = SDL3.SDL_ConvertSurface(rawSurface, SDL3.SDL_PIXELFORMAT_RGBA32);
                SDL3.SDL_DestroySurface(rawSurface); // Free original

                if (surface == null)
                    throw new Exception($"Failed to convert texture format: {SDL3.SDL_GetError()}");

                int width = surface->w;
                int height = surface->h;
                int pitch = surface->pitch;

                bool isOpaque = true;

                SDL_Color[,] pixels = new SDL_Color[height, width];

                byte* pixelPtr = (byte*)surface->pixels;


                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int offset = y * pitch + x * 4;
                        byte r = pixelPtr[offset + 0];
                        byte g = pixelPtr[offset + 1];
                        byte b = pixelPtr[offset + 2];
                        byte a = pixelPtr[offset + 3];

                        isOpaque &= (a == 255);

                        pixels[y, x] = new SDL_Color
                        {
                            r = r,
                            g = g,
                            b = b,
                            a = a
                        };
                    }
                }

                SDL3.SDL_DestroySurface(surface);

                return new Texture(pixels, isOpaque);
            }
        }



        public static void SaveColorArrayAsBmp(SDL_Color[,] pixels, int width, int height, string outputPath)
        {
            if (pixels.Length != width * height)
                throw new ArgumentException("Pixel array size doesn't match width * height");

            int pitch = width * 4; // 4 bytes per pixel
            int bufferSize = height * pitch;

            IntPtr pixelBuffer = Marshal.AllocHGlobal(bufferSize);

            unsafe
            {
                byte* ptr = (byte*)pixelBuffer.ToPointer();
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * width + x;
                        int offset = y * pitch + x * 4;

                        SDL_Color color = pixels[y, x];
                        ptr[offset + 0] = color.r; // R
                        ptr[offset + 1] = color.g; // G
                        ptr[offset + 2] = color.b; // B
                        ptr[offset + 3] = color.a; // A
                    }
                }

                SDL_Surface* surface = SDL3.SDL_CreateSurfaceFrom(
                    width,
                    height,
                    SDL3.SDL_PIXELFORMAT_RGBA32,
                    pixelBuffer,
                    pitch
                );

                if (surface == null)
                {
                    Marshal.FreeHGlobal(pixelBuffer);
                    throw new Exception("Failed to create surface: " + SDL3.SDL_GetError());
                }

                if (!SDL3.SDL_SaveBMP(surface, outputPath))
                {
                    SDL3.SDL_DestroySurface(surface);
                    Marshal.FreeHGlobal(pixelBuffer);
                    throw new Exception("Failed to save BMP: " + SDL3.SDL_GetError());
                }

                SDL3.SDL_DestroySurface(surface);
                Marshal.FreeHGlobal(pixelBuffer);
            }

        }
    }

}