using SDL;
using System.Numerics;
using System.Diagnostics;
using Simple3dRenderer.Rendering;
using Simple3dRenderer.Objects;
using System.Runtime.Intrinsics.Arm;
using Simple3dRenderer.Lighting;

class Program
{


    const int WINDOW_WIDTH = 1920;
    const int WINDOW_HEIGHT = 1080;

    const int downScale = 1;

    const int RENDER_WIDTH = WINDOW_WIDTH / downScale;
    const int RENDER_HEIGHT = WINDOW_HEIGHT / downScale;

    const int RENDER_FOV = 60;

    const int FPS = 60;

    const float MoveAmt = 2f;

    private static Scene createScene()
    {
        Camera camera = new(RENDER_WIDTH, RENDER_HEIGHT, RENDER_FOV);

        Mesh[] cubeWall = CubeWall.CreateCubeWall(10, 5, 1, z_init: -5);
        // Mesh cubeWall = MeshFactory.CreateCube(new Vector3(10, 5, 1), new SDL_Color{ r = 124, g = 250, b = 189, a = 255});
        // Mesh cube = MeshFactory.CreateCube(new Vector3(1, 1, 1), new SDL_Color{ r = 124, g = 250, b = 189, a = 255});
        // cube.SetPosition(new Vector3(0, 0, -5));

        // mesh.texture = TextureLoader.LoadBMP("Textures/dragon_head_symbol.bmp");

        SDL_Color bg = new SDL_Color { r = 124, g = 240, b = 189, a = 255 };

        return new Scene(camera, cubeWall.ToList(), bg,  ambientLight: new Vector3(0.3f, 0.3f, 0.3f));
    }



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
                WINDOW_WIDTH, WINDOW_HEIGHT, SDL_WindowFlags.SDL_WINDOW_OPENGL | SDL_WindowFlags.SDL_WINDOW_RESIZABLE);

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

            Scene scene = createScene();
            PerspectiveLight light = new PerspectiveLight(300, 300, 100, color: new(0.5f, 0.4f, 0.2f), farPlane: 20);
            List<PerspectiveLight> lights = [light];
            var pipeline = new Pipeline(RENDER_WIDTH, RENDER_HEIGHT, lights);

            float camX = 0;
            float camZ = 0;

            float camrotY = 0;

            float a = 0;

            Stopwatch swTotal = Stopwatch.StartNew();
            long lastTime = swTotal.ElapsedMilliseconds;


            while (running)
            {
                // Calculate deltaTime in seconds
                long currentTime = swTotal.ElapsedMilliseconds;
                float deltaTime = (currentTime - lastTime) / 1000f;
                lastTime = currentTime;

                // --- Event handling ---
                while (SDL3.SDL_PollEvent(&e) != false)
                {
                    if ((SDL_EventType)e.type == SDL_EventType.SDL_EVENT_QUIT)
                    {
                        running = false;
                    }
                    else if ((SDL_EventType)e.type == SDL_EventType.SDL_EVENT_KEY_DOWN)
                    {
                        switch (e.key.key)
                        {
                            case SDL_Keycode.SDLK_Q: running = false; break;
                            case SDL_Keycode.SDLK_LEFT: camX -= MoveAmt * deltaTime; break;
                            case SDL_Keycode.SDLK_RIGHT: camX += MoveAmt * deltaTime; break;
                            case SDL_Keycode.SDLK_UP: camZ -= MoveAmt * deltaTime; break;
                            case SDL_Keycode.SDLK_DOWN: camZ += MoveAmt * deltaTime; break;
                            case SDL_Keycode.SDLK_A: camrotY += 90f * deltaTime; break; // degrees/sec
                            case SDL_Keycode.SDLK_D: camrotY -= 90f * deltaTime; break;
                            case SDL_Keycode.SDLK_2: a += 0.5f * deltaTime; break;
                            case SDL_Keycode.SDLK_1: a -= 0.5f * deltaTime; break;
                        }
                    }
                }


                // Clear screen (black)
                SDL3.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
                SDL3.SDL_RenderClear(renderer);

                scene.camera.SetPosition(new Vector3(camX, 0, camZ));
                scene.camera.SetRotation(Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)(camrotY / 180 * Math.PI)));

                light.SetPosition(new Vector3(camX, 0, camZ));
                light.SetRotation(Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)(camrotY / 180 * Math.PI)));

                light.setQuadratic(a);


                var sw = Stopwatch.StartNew();

                SDL_Color[,] frame = pipeline.RenderScene(scene);

                sw.Stop();
                Console.WriteLine("Renderer time: " + sw.ElapsedMilliseconds);


                // Create texture once (during initialization)
                SDL_Texture* frameTexture = SDL3.SDL_CreateTexture(renderer,
                    SDL3.SDL_PIXELFORMAT_RGBA32, SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                    RENDER_WIDTH, RENDER_HEIGHT);

                // Each frame:
                nint pixels;
                int pitch;
                SDL3.SDL_LockTexture(frameTexture, null, &pixels, &pitch);

                UInt32* pixelBuffer = (UInt32*)pixels;
                int pixelsPerRow = pitch / sizeof(UInt32);

                // Fast conversion from SDL_Color to Uint32
                for (int y = 0; y < RENDER_HEIGHT; y++)
                {
                    for (int x = 0; x < RENDER_WIDTH; x++)
                    {
                        SDL_Color c = frame[y, x];  // Assuming this is your 2D array access
                        pixelBuffer[y * pixelsPerRow + x] =
                            (UInt32)(c.r) | ((UInt32)(c.g) << 8) | ((UInt32)(c.b) << 16) | ((UInt32)(c.a) << 24);
                    }
                }

                SDL3.SDL_UnlockTexture(frameTexture);
                SDL3.SDL_RenderTexture(renderer, frameTexture, null, null);
                SDL3.SDL_RenderPresent(renderer);






                SDL3.SDL_Delay((uint)Math.Max(0, (1000 / FPS) - sw.ElapsedMilliseconds));
            }

            // Cleanup
            SDL3.SDL_DestroyRenderer(renderer);
            SDL3.SDL_DestroyWindow(window);
            SDL3.SDL_Quit();
        }

    }


}
