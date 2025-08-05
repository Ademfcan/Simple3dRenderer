using SDL;
    using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Simple3dRenderer;
using System.Numerics;

class Program
{
    
    const int WINDOW_WIDTH = 640;
    const int WINDOW_HEIGHT = 480;

    const int RENDER_WIDTH = 640;
    const int RENDER_HEIGHT = 480;
    const int RENDER_FOV = 60;

    static void Main(string[] args)
    {
        


        // Initialize SDL
        if (!SDL3.SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
        {
            Console.WriteLine($"SDL_Init Error: {SDL3.SDL_GetError()}");
            return;

        }

        unsafe
        {
            // Create a window
            SDL_Window* window = SDL3.SDL_CreateWindow("Hello SDL3-CS",
                WINDOW_WIDTH, WINDOW_HEIGHT, SDL_WindowFlags.SDL_WINDOW_OPENGL);

            if (window == null)
            {
                Console.WriteLine($"SDL_CreateWindow Error: {SDL3.SDL_GetError()}");
                SDL3.SDL_Quit();
                return;
            }

            SDL_Renderer* renderer = SDL3.SDL_CreateRenderer(window, (byte*)null);

            if (renderer == null)
            {
                Console.WriteLine($"SDL_CreateRenderer Error: {SDL3.SDL_GetError()}");
                SDL3.SDL_DestroyWindow(window);
                SDL3.SDL_Quit();
                return;
            }

            // Main event loop
            bool running = true;
            SDL_Event e;

            Camera camera = new(RENDER_WIDTH, RENDER_HEIGHT, RENDER_FOV);
            Mesh mesh = new();

            mesh.AddTriangle(new Vector3(0, 0, -1),
                        new Vector3(1, 0, 0),
                        new Vector3(0.5f, 1, 0));

            mesh.Position = new Vector3(0, 0, -5);

            SDL_Color[,] frame = Pipeline.Run(camera, mesh);

            while (running)
            {
                while (SDL3.SDL_PollEvent(&e) != false)
                {
                    if ((SDL_EventType)e.type == SDL_EventType.SDL_EVENT_QUIT)
                    {
                        running = false;
                    }
                }

                // Clear screen (black)
                SDL3.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
                SDL3.SDL_RenderClear(renderer);


                for (int y = 0; y < RENDER_HEIGHT; y++)
                {
                    for (int x = 0; x < RENDER_WIDTH; x++)
                    {
                        SDL_Color frameColor = frame[y, x];
                        SDL3.SDL_SetRenderDrawColor(renderer, frameColor.r, frameColor.g, frameColor.b, frameColor.a);
                        SDL3.SDL_RenderPoint(renderer, x, y);
                    }
                }

                SDL3.SDL_RenderPresent(renderer);  // <== This flushes the backbuffer to the screen
                
                SDL3.SDL_Delay(50);
            }

            // Cleanup
            SDL3.SDL_DestroyRenderer(renderer);
            SDL3.SDL_DestroyWindow(window);
            SDL3.SDL_Quit();
        }

    }
}
