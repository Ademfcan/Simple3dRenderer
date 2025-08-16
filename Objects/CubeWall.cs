using System;
using System.Numerics;
using SDL;
using Simple3dRenderer.Objects;

public static class CubeWall
{
    private static Random _rand = new Random();

    // Returns a random SDL_Color
    public static SDL_Color RandomColor()
    {
        return new SDL_Color
        {
            r = (byte)_rand.Next(0, 256),
            g = (byte)_rand.Next(0, 256),
            b = (byte)_rand.Next(0, 256),
            a = 255 // full opacity
        };
    }

    // Creates a wall of cubes, n x n, each 1 unit size
    public static Mesh[] CreateCubeWall(int width, int height, float spacing = 1.1f, float x_init = 0, float y_init = 0, float z_init = 0)
    {
        Mesh[] wall = new Mesh[height*width];

        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                // Create a cube of size 1 with a random color
                Mesh cube = MeshFactory.CreateSimpleCube(1, RandomColor());

                // Position it in a grid (x = column, y = row, z = -5 for depth)
                cube.SetPosition(new Vector3(
                    x: x_init + j * spacing,
                    y: y_init + i * spacing,
                    z: z_init
                ));

                wall[i * width + j] = cube;
            }
        }

        return wall;
    }
}
