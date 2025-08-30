using SDL;
using Simple3dRenderer.Objects;
using Simple3dRenderer.Rendering;
using System.Numerics;

public static class SceneFactory
{
    public static Scene CreateScene(int renderWidth, int renderHeight)
    {
        const int cameraFov = 60; // degrees

        Camera camera = new(renderWidth, renderHeight, cameraFov);
        Mesh[] cubeWall = CubeWall.CreateCubeWall(5, 5, 1, z_init: -5);

        const float lightLvl = 0.1f;
        return new Scene(camera, cubeWall.ToList(), new SDL_Color {r = 40, b = 30, g = 20, a = 255 }, ambientLight: new Vector3(lightLvl, lightLvl, lightLvl));
    }
}