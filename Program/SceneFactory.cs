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

        List<Mesh> objects = [];

        Mesh[] cubeWall = CubeWall.CreateCubeWall(5, 5, 1, z_init: -5);
        objects.AddRange(cubeWall);

        Mesh floor = MeshFactory.CreateCube(new Vector3(100, 1f, 100), color: new SDL_Color { r = 255, b = 255, g = 255, a = 255 });
        floor.SetPosition(new Vector3(0, -4, 0));
        objects.Add(floor);


        const float lightLvl = 0.9f;
        return new Scene(camera, objects, new SDL_Color { r = 40, b = 30, g = 20, a = 255 }, ambientLight: new Vector3(lightLvl, lightLvl, lightLvl));
    }
}