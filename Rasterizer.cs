using SDL;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics;

namespace Simple3dRenderer
{
    public static class Rasterizer
    {
        public delegate SDL_Color FragmentShader(
            Vertex p,
            Vertex v0, Vertex v1, Vertex v2,
            float w0, float w1, float w2);

        public static void RasterizeTriangle(
            Vertex v0, Vertex v1, Vertex v2,
            SDL_Color[,] framebuffer, float[,] depthBuffer,
            FragmentShader shader)
        {
            float x0 = v0.Position.X, y0 = v0.Position.Y, z0 = v0.Position.Z;
            float x1 = v1.Position.X, y1 = v1.Position.Y, z1 = v1.Position.Z;
            float x2 = v2.Position.X, y2 = v2.Position.Y, z2 = v2.Position.Z;

            int minX = Math.Max(0, (int)Math.Floor(Math.Min(x0, Math.Min(x1, x2))));
            int maxX = Math.Min(framebuffer.GetLength(1) - 1, (int)Math.Ceiling(Math.Max(x0, Math.Max(x1, x2))));
            int minY = Math.Max(0, (int)Math.Floor(Math.Min(y0, Math.Min(y1, y2))));
            int maxY = Math.Min(framebuffer.GetLength(0) - 1, (int)Math.Ceiling(Math.Max(y0, Math.Max(y1, y2))));

            float Edge(float ax, float ay, float bx, float by, float cx, float cy) =>
                (cx - ax) * (by - ay) - (cy - ay) * (bx - ax);

            float area = Edge(x0, y0, x1, y1, x2, y2);
            if (area == 0) return;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;

                    float w0 = Edge(x1, y1, x2, y2, px, py);
                    float w1 = Edge(x2, y2, x0, y0, px, py);
                    float w2 = Edge(x0, y0, x1, y1, px, py);

                    if (w0 >= 0 && w1 >= 0 && w2 >= 0)
                    {
                        w0 /= area;
                        w1 /= area;
                        w2 /= area;

                        float z = w0 * z0 + w1 * z1 + w2 * z2;

                        if (z < depthBuffer[y, x])
                        {
                            depthBuffer[y, x] = z;

                            // Interpolated point (for lighting etc.)
                            Vertex p = Vertex.Interpolate(v0, v1, v2, w0, w1, w2);
                            framebuffer[y, x] = shader(p, v0, v1, v2, w0, w1, w2);
                        }
                    }
                }
            }
        }
    }
}
