using SDL;
using System;

public class WindowManager : IDisposable
{
    public unsafe SDL_Window* Window { get; private set; }
    public unsafe SDL_Renderer* Renderer { get; private set; }

    private readonly string _title;
    private readonly int _width;
    private readonly int _height;

    public WindowManager(string title, int width, int height)
    {
        _title = title;
        _width = width;
        _height = height;

        InitializeSdl();
        CreateWindow();
        CreateRenderer();
    }

    private void InitializeSdl()
    {
        if (!SDL3.SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
        {
            throw new Exception($"SDL_Init Error: {SDL3.SDL_GetError()}");
        }
    }

    private unsafe void CreateWindow()
    {
        Window = SDL3.SDL_CreateWindow(_title, _width, _height, SDL_WindowFlags.SDL_WINDOW_OPENGL | SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
        if (Window == null)
        {
            SDL3.SDL_Quit();
            throw new Exception($"SDL_CreateWindow Error: {SDL3.SDL_GetError()}");
        }
    }

    private unsafe void CreateRenderer()
    {
        Renderer = SDL3.SDL_CreateRenderer(Window, (byte*)null);
        if (Renderer == null)
        {
            SDL3.SDL_DestroyWindow(Window);
            SDL3.SDL_Quit();
            throw new Exception($"SDL_CreateRenderer Error: {SDL3.SDL_GetError()}");
        }
    }

    public unsafe void Dispose()
    {
        SDL3.SDL_DestroyRenderer(Renderer);
        SDL3.SDL_DestroyWindow(Window);
        SDL3.SDL_Quit();
    }
}