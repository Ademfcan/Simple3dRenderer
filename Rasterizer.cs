using SDL;
using MathNet.Numerics.LinearAlgebra;
using System.Numerics;

namespace Simple3dRenderer
{
    public static class Rasterizer
    {
        public delegate SDL_Color FragmentShader(
            Vertex v0, Vertex v1, Vertex v2,
            float w0, float w1, float w2);

        public static void RasterizeTrianglesBatch(
            IEnumerable<(Vertex v1, Vertex v2, Vertex v3)> triangles,
            SDL_Color[,] framebuffer, float[,] depthBuffer,
            FragmentShader shader)
        {
            foreach (var (v1, v2, v3) in triangles)
            {
                RasterizeTriangleSimd3(v1, v2, v3, framebuffer, depthBuffer, shader);
            }
        }

        // Optimized batch: Sort triangles by depth for better early-z rejection
        public static void RasterizeTrianglesBatchOptimized(
            IEnumerable<(Vertex v1, Vertex v2, Vertex v3)> triangles,
            SDL_Color[,] framebuffer, float[,] depthBuffer,
            FragmentShader shader)
        {
            // Sort triangles by average Z depth (front to back for early-z)
            var sortedTriangles = triangles
                .Select(t => new
                {
                    Triangle = t,
                    AvgZ = (t.v1.Position.Z + t.v2.Position.Z + t.v3.Position.Z) / 3.0f
                })
                .OrderBy(t => t.AvgZ) // Front to back (assuming smaller Z = closer)
                .Select(t => t.Triangle);


            foreach (var (v1, v2, v3) in sortedTriangles)
            {
                RasterizeTriangleSimd3(v1, v2, v3, framebuffer, depthBuffer, shader);
            }
        }

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
            if (area <= 0) return;

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
                            framebuffer[y, x] = shader(v0, v1, v2, w0, w1, w2);
                        }
                    }
                }
            }
        }

        public static void RasterizeTriangleSimd(
            Vertex v0, Vertex v1, Vertex v2,
            SDL_Color[,] framebuffer, float[,] depthBuffer,
            FragmentShader shader)
        {
            float x0 = v0.Position.X, y0 = v0.Position.Y, z0 = v0.Position.Z;
            float x1 = v1.Position.X, y1 = v1.Position.Y, z1 = v1.Position.Z;
            float x2 = v2.Position.X, y2 = v2.Position.Y, z2 = v2.Position.Z;

            // Use the same Edge function as the baseline
            float Edge(float ax, float ay, float bx, float by, float cx, float cy) =>
                (cx - ax) * (by - ay) - (cy - ay) * (bx - ax);

            float area = Edge(x0, y0, x1, y1, x2, y2);
            if (area <= 0) return;

            float invArea = 1.0f / area;

            int width = framebuffer.GetLength(1);
            int height = framebuffer.GetLength(0);

            int minX = Math.Max(0, (int)Math.Floor(Math.Min(x0, Math.Min(x1, x2))));
            int maxX = Math.Min(width - 1, (int)Math.Ceiling(Math.Max(x0, Math.Max(x1, x2))));
            int minY = Math.Max(0, (int)Math.Floor(Math.Min(y0, Math.Min(y1, y2))));
            int maxY = Math.Min(height - 1, (int)Math.Ceiling(Math.Max(y0, Math.Max(y1, y2))));

            int simdWidth = System.Numerics.Vector<float>.Count;
            float[] temp = new float[simdWidth];

            for (int y = minY; y <= maxY; y++)
            {
                float py = y + 0.5f;

                // Calculate edge values for the start of the row using the same Edge function
                float w0_start = Edge(x1, y1, x2, y2, minX + 0.5f, py);
                float w1_start = Edge(x2, y2, x0, y0, minX + 0.5f, py);
                float w2_start = Edge(x0, y0, x1, y1, minX + 0.5f, py);

                // Edge derivatives (how much the edge values change per unit x)
                float dw0_dx = (y2 - y1);
                float dw1_dx = (y0 - y2);
                float dw2_dx = (y1 - y0);

                float w0_row = w0_start;
                float w1_row = w1_start;
                float w2_row = w2_start;

                for (int x = minX; x <= maxX; x += simdWidth)
                {
                    int remaining = Math.Min(simdWidth, maxX - x + 1);

                    // Setup SIMD edge values for this batch of pixels
                    for (int i = 0; i < simdWidth; i++)
                    {
                        temp[i] = w0_row + i * dw0_dx;
                    }
                    var vw0 = new System.Numerics.Vector<float>(temp);

                    for (int i = 0; i < simdWidth; i++)
                    {
                        temp[i] = w1_row + i * dw1_dx;
                    }
                    var vw1 = new System.Numerics.Vector<float>(temp);

                    for (int i = 0; i < simdWidth; i++)
                    {
                        temp[i] = w2_row + i * dw2_dx;
                    }
                    var vw2 = new System.Numerics.Vector<float>(temp);

                    // Inside triangle mask
                    var mask = Vector.GreaterThanOrEqual(vw0, System.Numerics.Vector<float>.Zero) &
                               Vector.GreaterThanOrEqual(vw1, System.Numerics.Vector<float>.Zero) &
                               Vector.GreaterThanOrEqual(vw2, System.Numerics.Vector<float>.Zero);

                    for (int i = 0; i < remaining; i++)
                    {
                        if (mask[i] == 0) continue;

                        float fw0 = vw0[i] * invArea;
                        float fw1 = vw1[i] * invArea;
                        float fw2 = vw2[i] * invArea;

                        float z = fw0 * z0 + fw1 * z1 + fw2 * z2;

                        int xi = x + i;
                        if (z < depthBuffer[y, xi])
                        {
                            depthBuffer[y, xi] = z;

                            framebuffer[y, xi] = shader(v0, v1, v2, fw0, fw1, fw2);
                        }
                    }

                    // Advance to next batch
                    w0_row += dw0_dx * simdWidth;
                    w1_row += dw1_dx * simdWidth;
                    w2_row += dw2_dx * simdWidth;
                }
            }
        }

        public static void RasterizeTriangleSimd2(
            Vertex v0, Vertex v1, Vertex v2,
            SDL_Color[,] framebuffer, float[,] depthBuffer,
            FragmentShader shader)
        {
            float x0 = v0.Position.X, y0 = v0.Position.Y, z0 = v0.Position.Z;
            float x1 = v1.Position.X, y1 = v1.Position.Y, z1 = v1.Position.Z;
            float x2 = v2.Position.X, y2 = v2.Position.Y, z2 = v2.Position.Z;

            float Edge(float ax, float ay, float bx, float by, float cx, float cy) =>
                (cx - ax) * (by - ay) - (cy - ay) * (bx - ax);

            float area = Edge(x0, y0, x1, y1, x2, y2);
            if (area <= 0) return;

            float invArea = 1.0f / area;

            int width = framebuffer.GetLength(1);
            int height = framebuffer.GetLength(0);

            int minX = Math.Max(0, (int)Math.Floor(Math.Min(x0, Math.Min(x1, x2))));
            int maxX = Math.Min(width - 1, (int)Math.Ceiling(Math.Max(x0, Math.Max(x1, x2))));
            int minY = Math.Max(0, (int)Math.Floor(Math.Min(y0, Math.Min(y1, y2))));
            int maxY = Math.Min(height - 1, (int)Math.Ceiling(Math.Max(y0, Math.Max(y1, y2))));

            int simdWidth = System.Numerics.Vector<float>.Count;

            // Precompute edge deltas
            float dw0_dx = (y2 - y1);
            float dw1_dx = (y0 - y2);
            float dw2_dx = (y1 - y0);

            // Precompute Z interpolation derivative
            float dz_dx =
                ((z1 - z0) * dw1_dx + (z2 - z0) * dw2_dx) * invArea;

            // Allocate SIMD temp buffers once per triangle
            Span<float> temp_w0 = stackalloc float[simdWidth];
            Span<float> temp_w1 = stackalloc float[simdWidth];
            Span<float> temp_w2 = stackalloc float[simdWidth];
            Span<float> temp_z = stackalloc float[simdWidth];

            for (int y = minY; y <= maxY; y++)
            {
                float py = y + 0.5f;

                float w0_start = Edge(x1, y1, x2, y2, minX + 0.5f, py);
                float w1_start = Edge(x2, y2, x0, y0, minX + 0.5f, py);
                float w2_start = Edge(x0, y0, x1, y1, minX + 0.5f, py);

                float z_start = (w0_start * z0 + w1_start * z1 + w2_start * z2) * invArea;

                float w0_row = w0_start;
                float w1_row = w1_start;
                float w2_row = w2_start;
                float z_row = z_start;

                for (int x = minX; x <= maxX; x += simdWidth)
                {
                    int remaining = Math.Min(simdWidth, maxX - x + 1);

                    for (int i = 0; i < remaining; i++)
                    {
                        temp_w0[i] = w0_row + i * dw0_dx;
                        temp_w1[i] = w1_row + i * dw1_dx;
                        temp_w2[i] = w2_row + i * dw2_dx;
                        temp_z[i] = z_row + i * dz_dx;
                    }

                    var vw0 = new System.Numerics.Vector<float>(temp_w0);
                    var vw1 = new System.Numerics.Vector<float>(temp_w1);
                    var vw2 = new System.Numerics.Vector<float>(temp_w2);
                    var vz = new System.Numerics.Vector<float>(temp_z);

                    var mask = Vector.GreaterThanOrEqual(vw0, System.Numerics.Vector<float>.Zero) &
                            Vector.GreaterThanOrEqual(vw1, System.Numerics.Vector<float>.Zero) &
                            Vector.GreaterThanOrEqual(vw2, System.Numerics.Vector<float>.Zero);

                    for (int i = 0; i < remaining; i++)
                    {
                        if (mask[i] == 0) continue;

                        float fw0 = temp_w0[i] * invArea;
                        float fw1 = temp_w1[i] * invArea;
                        float fw2 = temp_w2[i] * invArea;
                        float z = temp_z[i];

                        int xi = x + i;
                        if (z < depthBuffer[y, xi])
                        {
                            depthBuffer[y, xi] = z;

                            framebuffer[y, xi] = shader(v0, v1, v2, fw0, fw1, fw2);
                        }
                    }

                    w0_row += dw0_dx * simdWidth;
                    w1_row += dw1_dx * simdWidth;
                    w2_row += dw2_dx * simdWidth;
                    z_row += dz_dx * simdWidth;
                }
            }
        }
        
        public static void RasterizeTriangleSimd3(
            Vertex v0, Vertex v1, Vertex v2,
            SDL_Color[,] framebuffer, float[,] depthBuffer,
            Rasterizer.FragmentShader shader)
        {
            float x0 = v0.Position.X, y0 = v0.Position.Y, z0 = v0.Position.Z;
            float x1 = v1.Position.X, y1 = v1.Position.Y, z1 = v1.Position.Z;
            float x2 = v2.Position.X, y2 = v2.Position.Y, z2 = v2.Position.Z;

            float Edge(float ax, float ay, float bx, float by, float cx, float cy) =>
                (cx - ax) * (by - ay) - (cy - ay) * (bx - ax);

            float area = Edge(x0, y0, x1, y1, x2, y2);
            if (area <= 0) return;

            float invArea = 1.0f / area;

            int width = framebuffer.GetLength(1);
            int height = framebuffer.GetLength(0);

            int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(x0, MathF.Min(x1, x2))));
            int maxX = Math.Min(width - 1, (int)MathF.Ceiling(MathF.Max(x0, MathF.Max(x1, x2))));
            int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(y0, MathF.Min(y1, y2))));
            int maxY = Math.Min(height - 1, (int)MathF.Ceiling(MathF.Max(y0, MathF.Max(y1, y2))));

            int simdWidth = System.Numerics.Vector<float>.Count;

            // Precompute [0, 1, 2, ..., simdWidth-1] once
            System.Numerics.Vector<float> indexOffsets = new System.Numerics.Vector<float>(Enumerable.Range(0, simdWidth).Select(i => (float)i).ToArray());

            // Edge function x-derivatives
            float dw0_dx = (y2 - y1);
            float dw1_dx = (y0 - y2);
            float dw2_dx = (y1 - y0);

            float dz_dx = ((z1 - z0) * dw1_dx + (z2 - z0) * dw2_dx) * invArea;

            for (int y = minY; y <= maxY; y++)
            {
                float py = y + 0.5f;

                float w0_start = Edge(x1, y1, x2, y2, minX + 0.5f, py);
                float w1_start = Edge(x2, y2, x0, y0, minX + 0.5f, py);
                float w2_start = Edge(x0, y0, x1, y1, minX + 0.5f, py);

                float z_start = (w0_start * z0 + w1_start * z1 + w2_start * z2) * invArea;

                float w0_row = w0_start;
                float w1_row = w1_start;
                float w2_row = w2_start;
                float z_row = z_start;

                for (int x = minX; x <= maxX; x += simdWidth)
                {
                    int remaining = Math.Min(simdWidth, maxX - x + 1);

                    // Vectorized edge values: wN_row + dx * i
                    System.Numerics.Vector<float> vw0 = new System.Numerics.Vector<float>(w0_row) + indexOffsets * dw0_dx;
                    System.Numerics.Vector<float> vw1 = new System.Numerics.Vector<float>(w1_row) + indexOffsets * dw1_dx;
                    System.Numerics.Vector<float> vw2 = new System.Numerics.Vector<float>(w2_row) + indexOffsets * dw2_dx;

                    System.Numerics.Vector<float> vz = new System.Numerics.Vector<float>(z_row) + indexOffsets * dz_dx;

                    // Mask for pixels inside triangle
                    var inside = Vector.GreaterThanOrEqual(vw0, System.Numerics.Vector<float>.Zero)
                            & Vector.GreaterThanOrEqual(vw1, System.Numerics.Vector<float>.Zero)
                            & Vector.GreaterThanOrEqual(vw2, System.Numerics.Vector<float>.Zero);

                    for (int i = 0; i < remaining; i++)
                    {
                        if (inside[i] == 0) continue;

                        float fw0 = vw0[i] * invArea;
                        float fw1 = vw1[i] * invArea;
                        float fw2 = vw2[i] * invArea;
                        float z   = vz[i];

                        int xi = x + i;

                        if (z < depthBuffer[y, xi])
                        {
                            depthBuffer[y, xi] = z;

                            // If you removed full Vertex.Interpolate, interpolate only needed values
                            framebuffer[y, xi] = shader(v0, v1, v2, fw0, fw1, fw2);
                        }
                    }

                    w0_row += dw0_dx * simdWidth;
                    w1_row += dw1_dx * simdWidth;
                    w2_row += dw2_dx * simdWidth;
                    z_row  += dz_dx  * simdWidth;
                }
            }
        }


    }
}