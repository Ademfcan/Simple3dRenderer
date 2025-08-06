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
                SDL_Surface* surface = SDL3.SDL_LoadBMP(path);
                if (surface == null)
                    throw new Exception("Failed to load texture");

                int width = surface->w;
                int height = surface->h;

                SDL_Color[,] pixels = new SDL_Color[height, width];


                byte* pixelPtr = (byte*)surface->pixels;
                int pitch = surface->pitch;

                bool isOpaque = true;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = y * pitch + x * 3;
                        byte b = pixelPtr[index];
                        byte g = pixelPtr[index + 1];
                        byte r = pixelPtr[index + 2];
                        byte a = 255;

                        pixels[y, x] = new SDL_Color { r = r, g = g, b = b, a = a };

                        isOpaque &= a == 255;
                    }
                }

                SDL3.SDL_DestroySurface(surface);
                return new Texture(pixels, isOpaque);
            }

        }
    }

}