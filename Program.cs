using SDL;
using MathNet.Numerics.LinearAlgebra;
using Simple3dRenderer;
using System.Numerics;
using System.Diagnostics;
using System.Reflection;

class Program
{

    
    const int WINDOW_WIDTH = 2560;
    const int WINDOW_HEIGHT = 1440;

    const int RENDER_WIDTH = WINDOW_WIDTH/3;
    const int RENDER_HEIGHT = WINDOW_HEIGHT/3;

    const int RENDER_FOV = 60;

    const int FPS = 60;

    const float MoveAmt = 0.3f;

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

            Camera camera = new(RENDER_WIDTH, RENDER_HEIGHT, RENDER_FOV);
            Mesh mesh = new();

            // Define pyramid vertices
            Vertex top = new Vertex(new Vector3(0, 1, 0), new Vector3(1, 255, 255));
            Vertex frontLeft = new Vertex(new Vector3(-1, -1, -1), new Vector3(255, 1, 255));
            Vertex frontRight = new Vertex(new Vector3(1, -1, -1), new Vector3(255, 255, 20));
            Vertex backRight = new Vertex(new Vector3(1, -1, 1), new Vector3(30, 255, 255));
            Vertex backLeft = new Vertex(new Vector3(-1, -1, 1), new Vector3(255, 16, 255));

            // Side triangles
            mesh.AddTriangle(top, frontRight, frontLeft);   // Front face
            mesh.AddTriangle(top, backRight, frontRight);   // Right face
            mesh.AddTriangle(top, backLeft, backRight);     // Back face
            mesh.AddTriangle(top, frontLeft, backLeft);     // Left face

            // Base (split into 2 triangles)
            mesh.AddTriangle(frontLeft, frontRight, backRight); // Base triangle 1
            mesh.AddTriangle(frontLeft, backRight, backLeft);

            float x_off = 0;

            float objrotY = 0;

            float camX = 0;
            float camZ = 0;

            float camrotY = 0;

            while (running)
            {
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
                            case SDL_Keycode.SDLK_LEFT:
                                Console.WriteLine("Left arrow pressed");
                                camX -= MoveAmt;
                                break;
                            case SDL_Keycode.SDLK_RIGHT:
                                Console.WriteLine("Right arrow pressed");
                                camX += MoveAmt;
                                break;
                            case SDL_Keycode.SDLK_UP:
                                camZ -= MoveAmt;
                                Console.WriteLine("Up arrow pressed");
                                break;
                            case SDL_Keycode.SDLK_DOWN:
                                camZ += MoveAmt;
                                Console.WriteLine("Down arrow pressed");
                                break;
                            case SDL_Keycode.SDLK_A:
                                camrotY += 10;
                                break;
                            case SDL_Keycode.SDLK_D:
                                camrotY -= 10;
                                break;

                        }
                    }
                }

                var sw = Stopwatch.StartNew();
                // Clear screen (black)
                SDL3.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
                SDL3.SDL_RenderClear(renderer);

                camera.Position = new Vector3(camX, 0, camZ);
                camera.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)(camrotY / 180 * Math.PI));

                mesh.Position = new Vector3(x_off, 0, -5);
                mesh.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, objrotY);
                objrotY += 0.1f;
                // x_off += 0.01f;
                
                

                SDL_Color[,] frame = Pipeline.RenderScene(camera, [mesh]);

                sw.Stop();
                var timeElapsed = sw.ElapsedMilliseconds;
                
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
                for (int y = 0; y < RENDER_HEIGHT; y++) {
                    for (int x = 0; x < RENDER_WIDTH; x++) {
                        SDL_Color c = frame[y, x];  // Assuming this is your 2D array access
                        pixelBuffer[y * pixelsPerRow + x] = 
                            (UInt32)(c.r) | ((UInt32)(c.g) << 8) | ((UInt32)(c.b) << 16) | ((UInt32)(c.a) << 24);
                    }
                }

                SDL3.SDL_UnlockTexture(frameTexture);
                SDL3.SDL_RenderTexture(renderer, frameTexture, null, null);
                SDL3.SDL_RenderPresent(renderer);

                

                Console.WriteLine("Renderer time: " + timeElapsed);

                SDL3.SDL_Delay((uint) Math.Max(0, (1000 / FPS) - timeElapsed));
            }

            // Cleanup
            SDL3.SDL_DestroyRenderer(renderer);
            SDL3.SDL_DestroyWindow(window);
            SDL3.SDL_Quit();
        }

    }

    
}
