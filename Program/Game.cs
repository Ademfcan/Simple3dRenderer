using System.Diagnostics;
using System.Numerics;
using SDL;
using Simple3dRenderer.Input;
using Simple3dRenderer.Objects;
using Simple3dRenderer.Rendering;

public class Game
{
    
    private const float MoveSpeed = 2f;

    private readonly WindowManager _windowManager;
    private readonly InputManager _inputManager;
    private readonly Renderer _renderer;
    private readonly Scene _scene;
    private readonly PerspectiveLight _flashlight;

    // default mouse mode is relative
    private bool isRelative = true;

    private readonly int targetFps;

    public Game(int windowWidth, int windowHeight, int downScaleRes, int targetFps)
    {
        this.targetFps = targetFps;
        int renderWidth = windowWidth / downScaleRes;
        int renderHeight = windowHeight / downScaleRes;



        _windowManager = new WindowManager("Demo", windowWidth, windowHeight);
        _inputManager = new InputManager();

        _scene = SceneFactory.CreateScene(renderWidth, renderHeight);
        _flashlight = new PerspectiveLight(300, 300, 30, color: new(0.5f, 0.4f, 0.2f), farPlane: 20, innerCutoffDegrees: 5, outerCutoffDegrees: 15);

        _scene.camera.Link(_flashlight);

        // give camera a little height boost
        _scene.camera.SetPosition(new Vector3(0, 2.5f, 0));

        var pipeline = new Pipeline(renderWidth, renderHeight, [_flashlight]);
        _renderer = new Renderer(renderWidth, renderHeight, pipeline);

        // Set up all input bindings here
        SetupInputBindings();

        unsafe
        {
            // set default mouse mode
            MouseLookController.SetRelativeMouseMode(_windowManager.Window, isRelative);
        }
    }

    private void SetupInputBindings()
    {
        _inputManager.BindQuit(SDL_Keycode.SDLK_Q);

        // Yaw-oriented movement bindings
        _inputManager.BindAction(SDL_Keycode.SDLK_W, (deltatime) =>
        {
            float yaw = MouseLookController.GetTotalYaw();
            var forward = new Vector3(MathF.Sin(yaw), 0, MathF.Cos(yaw));
            _scene.camera.SetPosition(_scene.camera.Position - forward * MoveSpeed * deltatime);
        });

        _inputManager.BindAction(SDL_Keycode.SDLK_S, (deltatime) =>
        {
            float yaw = MouseLookController.GetTotalYaw();
            var forward = new Vector3(MathF.Sin(yaw), 0, MathF.Cos(yaw));
            _scene.camera.SetPosition(_scene.camera.Position + forward * MoveSpeed * deltatime);
        });

        _inputManager.BindAction(SDL_Keycode.SDLK_A, (deltatime) =>
        {
            float yaw = MouseLookController.GetTotalYaw();
            var right = new Vector3(MathF.Cos(yaw), 0, -MathF.Sin(yaw));
            _scene.camera.SetPosition(_scene.camera.Position - right * MoveSpeed * deltatime);
        });

        _inputManager.BindAction(SDL_Keycode.SDLK_D, (deltatime) =>
        {
            float yaw = MouseLookController.GetTotalYaw();
            var right = new Vector3(MathF.Cos(yaw), 0, -MathF.Sin(yaw));
            _scene.camera.SetPosition(_scene.camera.Position + right * MoveSpeed * deltatime);
        });

        unsafe
        {
            _inputManager.BindToggle(SDL_Keycode.SDLK_M, () => isRelative, (isRelativeSetter) =>
            {
                isRelative = isRelativeSetter;
                MouseLookController.SetRelativeMouseMode(_windowManager.Window, isRelativeSetter);
            });
        }
    }

    public void Run()
    {
        Stopwatch swTotal = Stopwatch.StartNew();
        long lastTime = swTotal.ElapsedMilliseconds;

        while (_inputManager.IsRunning)
        {
            long currentTime = swTotal.ElapsedMilliseconds;
            float deltaTime = (currentTime - lastTime) / 1000f;
            lastTime = currentTime;

            // 1. Handle input, which updates the CameraController's properties
            _inputManager.HandleInput(deltaTime);

            // 3. Render the scene
            unsafe
            {
                if (isRelative)
                {
                    _scene.camera.SetRotation(MouseLookController.UpdateAndGetRotation());
                }
                else
                {
                    _scene.camera.SetRotation(Quaternion.Identity);
                }

                _renderer.Render(_windowManager.Renderer, _scene);
            }

            // wait for target fps or in a loop overrun dont wait at all
            SDL3.SDL_Delay((uint)Math.Max(0, (1000 / targetFps) - (swTotal.ElapsedMilliseconds - currentTime)));
        }
    }
    public void Shutdown()
    {
        _windowManager.Dispose();
    }
}