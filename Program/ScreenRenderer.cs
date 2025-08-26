using SDL;
using Simple3dRenderer.Objects;
using Simple3dRenderer.Rendering;
using System;
using System.Diagnostics;

public class Renderer
{
    private readonly int _renderWidth;
    private readonly int _renderHeight;
    private readonly Pipeline _pipeline;

    public Renderer(int renderWidth, int renderHeight, Pipeline pipeline)
    {
        _renderWidth = renderWidth;
        _renderHeight = renderHeight;
        _pipeline = pipeline;
    }

    public unsafe void Render(SDL_Renderer* renderer, Scene scene)
    {
        SDL3.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
        SDL3.SDL_RenderClear(renderer);

        var sw = Stopwatch.StartNew();
        SDL_Color[] frame = _pipeline.RenderScene(scene);
        sw.Stop();

        Console.WriteLine("Renderer time: " + sw.ElapsedMilliseconds);

        SDL_Texture* frameTexture = SDL3.SDL_CreateTexture(renderer,
            SDL3.SDL_PIXELFORMAT_RGBA32, SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
            _renderWidth, _renderHeight);

        nint pixels;
        int pitch;
        SDL3.SDL_LockTexture(frameTexture, null, &pixels, &pitch);

        UInt32* pixelBuffer = (UInt32*)pixels;
        int pixelsPerRow = pitch / sizeof(UInt32);

        for (int y = 0; y < _renderHeight; y++)
        {
            for (int x = 0; x < _renderWidth; x++)
            {
                SDL_Color c = frame[y * _renderWidth + x];
                pixelBuffer[y * pixelsPerRow + x] =
                    (UInt32)(c.r) | ((UInt32)(c.g) << 8) | ((UInt32)(c.b) << 16) | ((UInt32)(c.a) << 24);
            }
        }

        SDL3.SDL_UnlockTexture(frameTexture);
        SDL3.SDL_RenderTexture(renderer, frameTexture, null, null);
        SDL3.SDL_RenderPresent(renderer);
    }
}