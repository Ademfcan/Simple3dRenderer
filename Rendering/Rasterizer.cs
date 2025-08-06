using SDL;
using System.Numerics;
using Simple3dRenderer.Objects;
using System.Linq;

namespace Simple3dRenderer.Rendering
{
    public static class Rasterizer
    {
        public delegate SDL_Color FragmentShader(
            Vertex v0, Vertex v1, Vertex v2,
            float w0, float w1, float w2);

        // Top-left fill rule helper:
        // Returns true if the edge function value 'w' includes the pixel based on whether the edge is top-left.
        // This avoids cracks by consistently deciding pixel coverage on shared edges.
        private static bool IsTopLeftEdge(float x0, float y0, float x1, float y1)
        {
            // Edge is top if horizontal and y0 == y1 and x1 > x0
            if (y0 == y1)
                return x1 > x0;
            // Edge is left if it goes downward in screen coords (y1 > y0)
            return y1 > y0;
        }

        private static bool EdgeTest(float w, bool isTopLeft)
        {
            if (w > 0) return true;
            if (w == 0) return isTopLeft;
            return false;
        }

        private static float Edge(float ax, float ay, float bx, float by, float cx, float cy)
        {
            return (cx - ax) * (by - ay) - (cy - ay) * (bx - ax);
        }

        public static void RasterizeTrianglesBatchOptimized(
    IEnumerable<(Vertex v1, Vertex v2, Vertex v3)> triangles,
    SDL_Color[,] framebuffer, float[,] depthBuffer,
    FragmentShader shader, bool isOpaque)
        {
            IEnumerable<(Vertex v1, Vertex v2, Vertex v3)>? sortedTriangles = null;
            if (isOpaque)
            {
                sortedTriangles = triangles
                    .Select(t => new
                    {
                        Triangle = t,
                        AvgZ = (t.v1.Position.Z + t.v2.Position.Z + t.v3.Position.Z) / 3.0f
                    })
                    .OrderBy(t => t.AvgZ) // Front to back
                    .Select(t => t.Triangle);
            }
            else
            {
                sortedTriangles = triangles
                    .Select(t => new
                    {
                        Triangle = t,
                        AvgZ = (t.v1.Position.Z + t.v2.Position.Z + t.v3.Position.Z) / 3.0f
                    })
                    .OrderByDescending(t => t.AvgZ) // Back to front
                    .Select(t => t.Triangle);
            }

            foreach (var (v1, v2, v3) in sortedTriangles)
            {
                RasterizeTriangleOptimized(v1, v2, v3, framebuffer, depthBuffer, shader);
            }
        }



        // Optimized batch: Sort triangles by depth for better early-z rejection
        public static void RasterizeTriangleOptimized(
    Vertex v0, Vertex v1, Vertex v2,
    SDL_Color[,] framebuffer, float[,] depthBuffer,
    FragmentShader shader)
        {
            float x0 = v0.Position.X, y0 = v0.Position.Y, z0 = v0.Position.Z;
            float x1 = v1.Position.X, y1 = v1.Position.Y, z1 = v1.Position.Z;
            float x2 = v2.Position.X, y2 = v2.Position.Y, z2 = v2.Position.Z;

            float area = Edge(x0, y0, x1, y1, x2, y2);
            if (area <= 0) return;

            int width = framebuffer.GetLength(1);
            int height = framebuffer.GetLength(0);

            // Calculate screen coverage percentage
            float screenArea = width * height;
            float triangleScreenCoverage = Math.Abs(area) / screenArea;

            // Use different strategies based on triangle size
            if (triangleScreenCoverage > 0.25f) // Triangle covers >25% of screen
            {
                Console.WriteLine("big triangles");
                RasterizeTriangleLinear(v0, v1, v2, framebuffer, depthBuffer, shader);
            }
            else if (triangleScreenCoverage > 0.01f) // Medium triangles
            {
                Console.WriteLine("simd no tiles");
                RasterizeTriangleSimdOnly(v0, v1, v2, framebuffer, depthBuffer, shader);
            }
            else // Small triangles
            {
                Console.WriteLine("simd4");
                RasterizeTriangleSimd4(v0, v1, v2, framebuffer, depthBuffer, shader);
            }
        }

        // Linear rasterization for very large triangles - no tiling overhead
        private static void RasterizeTriangleLinear(
            Vertex v0, Vertex v1, Vertex v2,
            SDL_Color[,] framebuffer, float[,] depthBuffer,
            FragmentShader shader)
        {
            float x0 = v0.Position.X, y0 = v0.Position.Y, z0 = v0.Position.Z;
            float x1 = v1.Position.X, y1 = v1.Position.Y, z1 = v1.Position.Z;
            float x2 = v2.Position.X, y2 = v2.Position.Y, z2 = v2.Position.Z;

            float area = Edge(x0, y0, x1, y1, x2, y2);
            float invArea = 1.0f / area;

            bool edge0IsTopLeft = IsTopLeftEdge(x1, y1, x2, y2);
            bool edge1IsTopLeft = IsTopLeftEdge(x2, y2, x0, y0);
            bool edge2IsTopLeft = IsTopLeftEdge(x0, y0, x1, y1);

            int width = framebuffer.GetLength(1);
            int height = framebuffer.GetLength(0);

            // For large triangles, often the bounding box IS the screen
            int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(x0, MathF.Min(x1, x2))));
            int maxX = Math.Min(width - 1, (int)MathF.Ceiling(MathF.Max(x0, MathF.Max(x1, x2))));
            int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(y0, MathF.Min(y1, y2))));
            int maxY = Math.Min(height - 1, (int)MathF.Ceiling(MathF.Max(y0, MathF.Max(y1, y2))));

            // Precompute edge derivatives
            float dw0_dx = (y2 - y1);
            float dw1_dx = (y0 - y2);
            float dw2_dx = (y1 - y0);

            float dz_dx = ((z1 - z0) * dw1_dx + (z2 - z0) * dw2_dx) * invArea;

            // Process scanlines with minimal overhead
            for (int y = minY; y <= maxY; y++)
            {
                float py = y + 0.5f;

                // Calculate row starting values
                float w0_row = Edge(x1, y1, x2, y2, minX + 0.5f, py);
                float w1_row = Edge(x2, y2, x0, y0, minX + 0.5f, py);
                float w2_row = Edge(x0, y0, x1, y1, minX + 0.5f, py);
                float z_row = (w0_row * z0 + w1_row * z1 + w2_row * z2) * invArea;

                // Find first pixel in scanline (early out for large triangles)
                int startX = minX;
                for (int x = minX; x <= maxX; x++)
                {
                    if (EdgeTest(w0_row, edge0IsTopLeft) &&
                        EdgeTest(w1_row, edge1IsTopLeft) &&
                        EdgeTest(w2_row, edge2IsTopLeft))
                    {
                        startX = x;
                        break;
                    }
                    w0_row += dw0_dx;
                    w1_row += dw1_dx;
                    w2_row += dw2_dx;
                    z_row += dz_dx;
                }

                // Rasterize the span
                for (int x = startX; x <= maxX; x++)
                {
                    if (!(EdgeTest(w0_row, edge0IsTopLeft) &&
                          EdgeTest(w1_row, edge1IsTopLeft) &&
                          EdgeTest(w2_row, edge2IsTopLeft)))
                    {
                        // Early exit when we leave the triangle
                        break;
                    }

                    if (z_row < depthBuffer[y, x])
                    {
                        float fw0 = w0_row * invArea;
                        float fw1 = w1_row * invArea;
                        float fw2 = w2_row * invArea;

                        SDL_Color pixelColor = shader(v0, v1, v2, fw0, fw1, fw2);

                        if (pixelColor.a == 255)
                        {
                            framebuffer[y, x] = pixelColor;
                            depthBuffer[y, x] = z_row;
                        }
                        else
                        {
                            framebuffer[y, x] = AlphaBlend(pixelColor, framebuffer[y, x]);
                        }
                    }

                    w0_row += dw0_dx;
                    w1_row += dw1_dx;
                    w2_row += dw2_dx;
                    z_row += dz_dx;
                }
            }
        }

        // SIMD without tiling for medium triangles
        private static void RasterizeTriangleSimdOnly(
            Vertex v0, Vertex v1, Vertex v2,
            SDL_Color[,] framebuffer, float[,] depthBuffer,
            FragmentShader shader)
        {
            float x0 = v0.Position.X, y0 = v0.Position.Y, z0 = v0.Position.Z;
            float x1 = v1.Position.X, y1 = v1.Position.Y, z1 = v1.Position.Z;
            float x2 = v2.Position.X, y2 = v2.Position.Y, z2 = v2.Position.Z;

            float area = Edge(x0, y0, x1, y1, x2, y2);
            float invArea = 1.0f / area;

            bool edge0IsTopLeft = IsTopLeftEdge(x1, y1, x2, y2);
            bool edge1IsTopLeft = IsTopLeftEdge(x2, y2, x0, y0);
            bool edge2IsTopLeft = IsTopLeftEdge(x0, y0, x1, y1);

            int width = framebuffer.GetLength(1);
            int height = framebuffer.GetLength(0);

            int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(x0, MathF.Min(x1, x2))));
            int maxX = Math.Min(width - 1, (int)MathF.Ceiling(MathF.Max(x0, MathF.Max(x1, x2))));
            int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(y0, MathF.Min(y1, y2))));
            int maxY = Math.Min(height - 1, (int)MathF.Ceiling(MathF.Max(y0, MathF.Max(y1, y2))));

            int simdWidth = Vector<float>.Count;
            Vector<float> indexOffsets = new Vector<float>(Enumerable.Range(0, simdWidth).Select(i => (float)i).ToArray());

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

                    Vector<float> vw0 = new Vector<float>(w0_row) + indexOffsets * dw0_dx;
                    Vector<float> vw1 = new Vector<float>(w1_row) + indexOffsets * dw1_dx;
                    Vector<float> vw2 = new Vector<float>(w2_row) + indexOffsets * dw2_dx;
                    Vector<float> vz = new Vector<float>(z_row) + indexOffsets * dz_dx;

                    for (int i = 0; i < remaining; i++)
                    {
                        float w0 = vw0[i];
                        float w1 = vw1[i];
                        float w2 = vw2[i];

                        if (!(EdgeTest(w0, edge0IsTopLeft) &&
                              EdgeTest(w1, edge1IsTopLeft) &&
                              EdgeTest(w2, edge2IsTopLeft)))
                            continue;

                        float fw0 = w0 * invArea;
                        float fw1 = w1 * invArea;
                        float fw2 = w2 * invArea;
                        float z = vz[i];

                        int xi = x + i;

                        if (z < depthBuffer[y, xi])
                        {
                            SDL_Color pixelColor = shader(v0, v1, v2, fw0, fw1, fw2);

                            if (pixelColor.a == 255)
                            {
                                framebuffer[y, xi] = pixelColor;
                                depthBuffer[y, xi] = z;
                            }
                            else
                            {
                                framebuffer[y, xi] = AlphaBlend(pixelColor, framebuffer[y, xi]);
                            }
                        }
                    }

                    w0_row += dw0_dx * simdWidth;
                    w1_row += dw1_dx * simdWidth;
                    w2_row += dw2_dx * simdWidth;
                    z_row += dz_dx * simdWidth;
                }
            }
        }

        public static void RasterizeTriangleSimd4(
                    Vertex v0, Vertex v1, Vertex v2,
                    SDL_Color[,] framebuffer, float[,] depthBuffer,
                    FragmentShader shader) // tile size configurable
        {
            float x0 = v0.Position.X, y0 = v0.Position.Y, z0 = v0.Position.Z;
            float x1 = v1.Position.X, y1 = v1.Position.Y, z1 = v1.Position.Z;
            float x2 = v2.Position.X, y2 = v2.Position.Y, z2 = v2.Position.Z;


            float area = Edge(x0, y0, x1, y1, x2, y2);
            if (area <= 0) return;
            int tileSize = GetAdaptiveTileSize(Math.Abs(area));

            float invArea = 1.0f / area;

            bool edge0IsTopLeft = IsTopLeftEdge(x1, y1, x2, y2);
            bool edge1IsTopLeft = IsTopLeftEdge(x2, y2, x0, y0);
            bool edge2IsTopLeft = IsTopLeftEdge(x0, y0, x1, y1);

            int width = framebuffer.GetLength(1);
            int height = framebuffer.GetLength(0);

            int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(x0, MathF.Min(x1, x2))));
            int maxX = Math.Min(width - 1, (int)MathF.Ceiling(MathF.Max(x0, MathF.Max(x1, x2))));
            int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(y0, MathF.Min(y1, y2))));
            int maxY = Math.Min(height - 1, (int)MathF.Ceiling(MathF.Max(y0, MathF.Max(y1, y2))));

            int simdWidth = Vector<float>.Count;
            Vector<float> indexOffsets = new Vector<float>(Enumerable.Range(0, simdWidth).Select(i => (float)i).ToArray());

            float dw0_dx = (y2 - y1);
            float dw1_dx = (y0 - y2);
            float dw2_dx = (y1 - y0);

            float dw0_dy = (x1 - x2);
            float dw1_dy = (x2 - x0);
            float dw2_dy = (x0 - x1);

            float dz_dx = ((z1 - z0) * dw1_dx + (z2 - z0) * dw2_dx) * invArea;
            float dz_dy = ((z1 - z0) * dw1_dy + (z2 - z0) * dw2_dy) * invArea;

            bool IsTileFullyOutside(int tileX, int tileY)
            {
                // Check if triangle bounding box intersects tile bounding box
                float triMinX = MathF.Min(x0, MathF.Min(x1, x2));
                float triMaxX = MathF.Max(x0, MathF.Max(x1, x2));
                float triMinY = MathF.Min(y0, MathF.Min(y1, y2));
                float triMaxY = MathF.Max(y0, MathF.Max(y1, y2));

                float tileMinX = tileX;
                float tileMaxX = tileX + tileSize;
                float tileMinY = tileY;
                float tileMaxY = tileY + tileSize;

                // No intersection if bounding boxes don't overlap
                return triMaxX < tileMinX || triMinX > tileMaxX ||
                       triMaxY < tileMinY || triMinY > tileMaxY;
            }

            for (int tileY = minY; tileY <= maxY; tileY += tileSize)
            {
                for (int tileX = minX; tileX <= maxX; tileX += tileSize)
                {
                    int tileMaxX = Math.Min(tileX + tileSize - 1, maxX);
                    int tileMaxY = Math.Min(tileY + tileSize - 1, maxY);

                    if (IsTileFullyOutside(tileX, tileY))
                        continue;

                    for (int y = tileY; y <= tileMaxY; y++)
                    {
                        float py = y + 0.5f;

                        float w0_row = Edge(x1, y1, x2, y2, tileX + 0.5f, py);
                        float w1_row = Edge(x2, y2, x0, y0, tileX + 0.5f, py);
                        float w2_row = Edge(x0, y0, x1, y1, tileX + 0.5f, py);

                        float z_row = (w0_row * z0 + w1_row * z1 + w2_row * z2) * invArea;

                        for (int x = tileX; x <= tileMaxX; x += simdWidth)
                        {
                            int remaining = Math.Min(simdWidth, tileMaxX - x + 1);

                            Vector<float> vw0 = new Vector<float>(w0_row) + indexOffsets * dw0_dx;
                            Vector<float> vw1 = new Vector<float>(w1_row) + indexOffsets * dw1_dx;
                            Vector<float> vw2 = new Vector<float>(w2_row) + indexOffsets * dw2_dx;
                            Vector<float> vz = new Vector<float>(z_row) + indexOffsets * dz_dx;

                            for (int i = 0; i < remaining; i++)
                            {
                                float w0 = vw0[i];
                                float w1 = vw1[i];
                                float w2 = vw2[i];

                                if (!(EdgeTest(w0, edge0IsTopLeft) &&
                                      EdgeTest(w1, edge1IsTopLeft) &&
                                      EdgeTest(w2, edge2IsTopLeft)))
                                    continue;

                                float fw0 = w0 * invArea;
                                float fw1 = w1 * invArea;
                                float fw2 = w2 * invArea;
                                float z = vz[i];

                                int xi = x + i;

                                if (z < depthBuffer[y, xi])
                                {
                                    SDL_Color pixelColor = shader(v0, v1, v2, fw0, fw1, fw2);

                                    if (pixelColor.a == 255)
                                    {
                                        framebuffer[y, xi] = pixelColor;
                                        depthBuffer[y, xi] = z;
                                    }
                                    else
                                    {
                                        framebuffer[y, xi] = AlphaBlend(pixelColor, framebuffer[y, xi]);
                                    }
                                }
                            }

                            w0_row += dw0_dx * simdWidth;
                            w1_row += dw1_dx * simdWidth;
                            w2_row += dw2_dx * simdWidth;
                            z_row += dz_dx * simdWidth;
                        }
                    }
                }
            }
        }

        private static int GetAdaptiveTileSize(float triangleArea)
        {
            if (triangleArea < 64) return 4;   // Very small triangles
            if (triangleArea < 256) return 8;   // Small triangles  
            if (triangleArea < 1024) return 16;  // Medium triangles
            if (triangleArea < 4096) return 32;  // Large triangles
            return 64;                           // Very large triangles
        }

        static SDL_Color AlphaBlend(SDL_Color src, SDL_Color dst)
        {
            float alpha = src.a / 255f;
            float invAlpha = 1.0f - alpha;

            byte r = (byte)(src.r * alpha + dst.r * invAlpha);
            byte g = (byte)(src.g * alpha + dst.g * invAlpha);
            byte b = (byte)(src.b * alpha + dst.b * invAlpha);
            byte a = (byte)(src.a + dst.a * invAlpha);

            return new SDL_Color { r = r, g = g, b = b, a = a };
        }
    }
}
