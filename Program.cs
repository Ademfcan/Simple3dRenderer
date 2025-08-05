using SDL;
using MathNet.Numerics.LinearAlgebra;
using Simple3dRenderer;
using System.Numerics;

class Program
{

    const int WINDOW_WIDTH = 640;
    const int WINDOW_HEIGHT = 480;

    const int RENDER_WIDTH = 640;
    const int RENDER_HEIGHT = 480;
    const int RENDER_FOV = 60;

    const int FPS = 60;

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
                                camX--;
                                break;
                            case SDL_Keycode.SDLK_RIGHT:
                                Console.WriteLine("Right arrow pressed");
                                camX++;
                                break;
                            case SDL_Keycode.SDLK_UP:
                                camZ--;
                                Console.WriteLine("Up arrow pressed");
                                break;
                            case SDL_Keycode.SDLK_DOWN:
                                camZ++;
                                Console.WriteLine("Down arrow pressed");
                                break;
                            case SDL_Keycode.SDLK_A:
                                camrotY+=10;
                                break;
                            case SDL_Keycode.SDLK_D:
                                camrotY-=10;
                                break;

                        }
                    }
                }

                // Clear screen (black)
                SDL3.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
                SDL3.SDL_RenderClear(renderer);

                camera.Position = new Vector3(camX, 0, camZ);
                camera.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float) (camrotY / 180 * Math.PI));

                mesh.Position = new Vector3(x_off, 0, -5);
                mesh.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, objrotY);
                objrotY += 0.1f;
                // x_off += 0.01f;
                SDL_Color[,] frame = Pipeline.Run(camera, mesh);


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

                SDL3.SDL_Delay(1000 / FPS);
            }

            // Cleanup
            SDL3.SDL_DestroyRenderer(renderer);
            SDL3.SDL_DestroyWindow(window);
            SDL3.SDL_Quit();
        }

    }

    
}
