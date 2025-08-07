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

        // Thread-local storage for SIMD vectors to avoid allocations
        [ThreadStatic]
        private static Vector<float>? _threadLocalIndexOffsets;
        
        private static Vector<float> GetIndexOffsets()
        {
            if (_threadLocalIndexOffsets == null)
            {
                int simdWidth = Vector<float>.Count;
                _threadLocalIndexOffsets = new Vector<float>(
                    Enumerable.Range(0, simdWidth).Select(i => (float)i).ToArray());
            }
            return _threadLocalIndexOffsets.Value;
        }

        // Top-left fill rule helper:
        // Returns true if the edge function value 'w' includes the pixel based on whether the edge is top-left.
        // This avoids cracks by consistently deciding pixel coverage on shared edges.
        // NEW: Multithreaded large triangle rasterizer optimized for maximum performance
        public static void RasterizeTriangleMultithreaded(
            Vertex v0, Vertex v1, Vertex v2,
            SDL_Color[,] framebuffer, float[,] depthBuffer,
            FragmentShader shader, int? threadCount = null)
        {
            float x0 = v0.Position.X, y0 = v0.Position.Y, z0 = v0.Position.Z;
            float x1 = v1.Position.X, y1 = v1.Position.Y, z1 = v1.Position.Z;
            float x2 = v2.Position.X, y2 = v2.Position.Y, z2 = v2.Position.Z;

            float area = Edge(x0, y0, x1, y1, x2, y2);
            if (area <= 0) return;

            int width = framebuffer.GetLength(1);
            int height = framebuffer.GetLength(0);

            // Calculate triangle coverage to decide threading strategy
            float screenArea = width * height;
            float triangleScreenCoverage = Math.Abs(area) / screenArea;

            float invArea = 1.0f / area;

            bool edge0IsTopLeft = IsTopLeftEdge(x1, y1, x2, y2);
            bool edge1IsTopLeft = IsTopLeftEdge(x2, y2, x0, y0);
            bool edge2IsTopLeft = IsTopLeftEdge(x0, y0, x1, y1);

            int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(x0, MathF.Min(x1, x2))));
            int maxX = Math.Min(width - 1, (int)MathF.Ceiling(MathF.Max(x0, MathF.Max(x1, x2))));
            int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(y0, MathF.Min(y1, y2))));
            int maxY = Math.Min(height - 1, (int)MathF.Ceiling(MathF.Max(y0, MathF.Max(y1, y2))));

            int triangleHeight = maxY - minY + 1;
            int triangleWidth = maxX - minX + 1;

            // Determine optimal threading strategy based on triangle dimensions
            int numThreads = threadCount ?? Environment.ProcessorCount;
            numThreads = Math.Min(numThreads, triangleHeight / 4); // Don't oversplit small triangles

            if (numThreads <= 1)
            {
                RasterizeTriangleLinear(v0, v1, v2, framebuffer, depthBuffer, shader);
                return;
            }

            // Precompute edge derivatives for all threads
            float dw0_dx = (y2 - y1);
            float dw1_dx = (y0 - y2);
            float dw2_dx = (y1 - y0);
            float dz_dx = ((z1 - z0) * dw1_dx + (z2 - z0) * dw2_dx) * invArea;

            // Choose between scanline-parallel and tile-parallel based on triangle aspect ratio
            float aspectRatio = (float)triangleWidth / triangleHeight;
            
            if (aspectRatio > 4.0f || triangleHeight < numThreads * 8)
            {
                // Wide triangles or insufficient height: use tile-based parallelism
                RasterizeTriangleMultithreadedTiled(
                    v0, v1, v2, framebuffer, depthBuffer, shader, 
                    minX, maxX, minY, maxY, invArea, 
                    edge0IsTopLeft, edge1IsTopLeft, edge2IsTopLeft,
                    dw0_dx, dw1_dx, dw2_dx, dz_dx, numThreads);
            }
            else
            {
                // Tall triangles: use scanline-parallel approach
                RasterizeTriangleMultithreadedScanlines(
                    v0, v1, v2, framebuffer, depthBuffer, shader,
                    minX, maxX, minY, maxY, invArea,
                    edge0IsTopLeft, edge1IsTopLeft, edge2IsTopLeft,
                    dw0_dx, dw1_dx, dw2_dx, dz_dx, numThreads);
            }
        }

        // Scanline-parallel rasterization for tall triangles
        private static void RasterizeTriangleMultithreadedScanlines(
            Vertex v0, Vertex v1, Vertex v2,
            SDL_Color[,] framebuffer, float[,] depthBuffer,
            FragmentShader shader,
            int minX, int maxX, int minY, int maxY, float invArea,
            bool edge0IsTopLeft, bool edge1IsTopLeft, bool edge2IsTopLeft,
            float dw0_dx, float dw1_dx, float dw2_dx, float dz_dx,
            int numThreads)
        {
            float x0 = v0.Position.X, y0 = v0.Position.Y, z0 = v0.Position.Z;
            float x1 = v1.Position.X, y1 = v1.Position.Y, z1 = v1.Position.Z;
            float x2 = v2.Position.X, y2 = v2.Position.Y, z2 = v2.Position.Z;

            int triangleHeight = maxY - minY + 1;
            int rowsPerThread = Math.Max(1, triangleHeight / numThreads);

            ParallelOptions parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = numThreads
            };

            Parallel.For(0, numThreads, parallelOptions, threadIndex =>
            {
                int startY = minY + threadIndex * rowsPerThread;
                int endY = (threadIndex == numThreads - 1) 
                    ? maxY 
                    : Math.Min(maxY, startY + rowsPerThread - 1);

                if (startY > maxY) return;

                int simdWidth = Vector<float>.Count;
                Vector<float> indexOffsets = GetIndexOffsets();

                // Process assigned scanlines
                for (int y = startY; y <= endY; y++)
                {
                    float py = y + 0.5f;

                    // Calculate row starting values
                    float w0_row = Edge(x1, y1, x2, y2, minX + 0.5f, py);
                    float w1_row = Edge(x2, y2, x0, y0, minX + 0.5f, py);
                    float w2_row = Edge(x0, y0, x1, y1, minX + 0.5f, py);
                    float z_row = (w0_row * z0 + w1_row * z1 + w2_row * z2) * invArea;

                    // SIMD processing across the scanline
                    for (int x = minX; x <= maxX; x += simdWidth)
                    {
                        int remaining = Math.Min(simdWidth, maxX - x + 1);

                        Vector<float> vw0 = new Vector<float>(w0_row) + indexOffsets * dw0_dx;
                        Vector<float> vw1 = new Vector<float>(w1_row) + indexOffsets * dw1_dx;
                        Vector<float> vw2 = new Vector<float>(w2_row) + indexOffsets * dw2_dx;
                        Vector<float> vz = new Vector<float>(z_row) + indexOffsets * dz_dx;

                        // Process pixels in SIMD batch
                        for (int i = 0; i < remaining; i++)
                        {
                            float w0 = vw0[i];
                            float w1 = vw1[i];
                            float w2 = vw2[i];

                            if (!(EdgeTest(w0, edge0IsTopLeft) &&
                                  EdgeTest(w1, edge1IsTopLeft) &&
                                  EdgeTest(w2, edge2IsTopLeft)))
                                continue;

                            float z = vz[i];
                            int xi = x + i;

                            // Thread-safe depth test and write
                            float currentDepth = depthBuffer[y, xi];
                            if (z < currentDepth)
                            {
                                // Use Interlocked.CompareExchange for thread-safe depth buffer updates
                                if (Interlocked.CompareExchange(ref depthBuffer[y, xi], z, currentDepth) == currentDepth)
                                {
                                    float fw0 = w0 * invArea;
                                    float fw1 = w1 * invArea;
                                    float fw2 = w2 * invArea;

                                    SDL_Color pixelColor = shader(v0, v1, v2, fw0, fw1, fw2);

                                    if (pixelColor.a >= 254)
                                    {
                                        framebuffer[y, xi] = pixelColor;
                                    }
                                    else
                                    {
                                        // Note: Alpha blending is not thread-safe in this implementation
                                        // For proper alpha blending in multithreaded scenarios,
                                        // consider using atomic operations or order-independent transparency
                                        framebuffer[y, xi] = AlphaBlend(pixelColor, framebuffer[y, xi]);
                                    }
                                }
                            }
                        }

                        w0_row += dw0_dx * simdWidth;
                        w1_row += dw1_dx * simdWidth;
                        w2_row += dw2_dx * simdWidth;
                        z_row += dz_dx * simdWidth;
                    }
                }
            });
        }

        // Tile-parallel rasterization for wide triangles
        private static void RasterizeTriangleMultithreadedTiled(
            Vertex v0, Vertex v1, Vertex v2,
            SDL_Color[,] framebuffer, float[,] depthBuffer,
            FragmentShader shader,
            int minX, int maxX, int minY, int maxY, float invArea,
            bool edge0IsTopLeft, bool edge1IsTopLeft, bool edge2IsTopLeft,
            float dw0_dx, float dw1_dx, float dw2_dx, float dz_dx,
            int numThreads)
        {
            float x0 = v0.Position.X, y0 = v0.Position.Y, z0 = v0.Position.Z;
            float x1 = v1.Position.X, y1 = v1.Position.Y, z1 = v1.Position.Z;
            float x2 = v2.Position.X, y2 = v2.Position.Y, z2 = v2.Position.Z;

            // Adaptive tile size based on triangle dimensions
            int triangleWidth = maxX - minX + 1;
            int triangleHeight = maxY - minY + 1;
            int tileSize = Math.Max(32, Math.Min(128, Math.Max(triangleWidth, triangleHeight) / (numThreads * 2)));

            // Generate tile work items
            var tiles = new List<(int startX, int endX, int startY, int endY)>();
            
            for (int tileY = minY; tileY <= maxY; tileY += tileSize)
            {
                for (int tileX = minX; tileX <= maxX; tileX += tileSize)
                {
                    int endX = Math.Min(tileX + tileSize - 1, maxX);
                    int endY = Math.Min(tileY + tileSize - 1, maxY);
                    
                    // Quick bounding box rejection
                    if (!IsTileIntersectsTriangle(tileX, endX, tileY, endY, x0, y0, x1, y1, x2, y2))
                        continue;
                        
                    tiles.Add((tileX, endX, tileY, endY));
                }
            }

            if (tiles.Count == 0) return;

            // Process tiles in parallel
            ParallelOptions parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = numThreads
            };

            Parallel.ForEach(tiles, parallelOptions, tile =>
            {
                int simdWidth = Vector<float>.Count;
                Vector<float> indexOffsets = GetIndexOffsets();

                for (int y = tile.startY; y <= tile.endY; y++)
                {
                    float py = y + 0.5f;

                    float w0_row = Edge(x1, y1, x2, y2, tile.startX + 0.5f, py);
                    float w1_row = Edge(x2, y2, x0, y0, tile.startX + 0.5f, py);
                    float w2_row = Edge(x0, y0, x1, y1, tile.startX + 0.5f, py);
                    float z_row = (w0_row * z0 + w1_row * z1 + w2_row * z2) * invArea;

                    for (int x = tile.startX; x <= tile.endX; x += simdWidth)
                    {
                        int remaining = Math.Min(simdWidth, tile.endX - x + 1);

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

                            float z = vz[i];
                            int xi = x + i;

                            float currentDepth = Volatile.Read(ref depthBuffer[y, xi]);
                            if (z < currentDepth)
                            {
                                float originalDepth = Interlocked.CompareExchange(ref depthBuffer[y, xi], z, currentDepth);
                                if (originalDepth == currentDepth)
                                {
                                    float fw0 = w0 * invArea;
                                    float fw1 = w1 * invArea;
                                    float fw2 = w2 * invArea;

                                    SDL_Color pixelColor = shader(v0, v1, v2, fw0, fw1, fw2);

                                    if (pixelColor.a >= 254)
                                    {
                                        WriteColorAtomic(framebuffer, y, xi, pixelColor);
                                    }
                                    else
                                    {
                                        WriteColorAtomicBlended(framebuffer, y, xi, pixelColor);
                                    }
                                }
                            }
                        }

                        w0_row += dw0_dx * simdWidth;
                        w1_row += dw1_dx * simdWidth;
                        w2_row += dw2_dx * simdWidth;
                        z_row += dz_dx * simdWidth;
                    }
                }
            });
        }

        // Thread-safe color writing methods
        private static void WriteColorAtomic(SDL_Color[,] framebuffer, int y, int x, SDL_Color color)
        {
            // Since SDL_Color is a small struct (4 bytes), the write should be atomic on most platforms
            // However, for guaranteed thread safety, we could use locks or other synchronization
            // For performance, we'll rely on the atomic nature of small struct writes in .NET
            framebuffer[y, x] = color;
        }

        private static void WriteColorAtomicBlended(SDL_Color[,] framebuffer, int y, int x, SDL_Color srcColor)
        {
            // For alpha blending in multithreaded scenarios, we need more careful handling
            // This implementation trades some accuracy for performance by avoiding locks
            // A more robust implementation would use spinlocks or other synchronization
            
            SDL_Color currentColor = framebuffer[y, x];
            SDL_Color blendedColor = AlphaBlend(srcColor, currentColor);
            framebuffer[y, x] = blendedColor;
            
            // Note: There's still a race condition here between read and write
            // For production use, consider:
            // 1. Using order-independent transparency techniques
            // 2. Rendering transparent objects in a separate pass
            // 3. Using atomic operations with packed color values
        }


        static bool IsTileIntersectsTriangle(int tileMinX, int tileMaxX, int tileMinY, int tileMaxY,
            float x0, float y0, float x1, float y1, float x2, float y2)
        {
            // Quick bounding box check
            float triMinX = MathF.Min(x0, MathF.Min(x1, x2));
            float triMaxX = MathF.Max(x0, MathF.Max(x1, x2));
            float triMinY = MathF.Min(y0, MathF.Min(y1, y2));
            float triMaxY = MathF.Max(y0, MathF.Max(y1, y2));

            return !(triMaxX < tileMinX || triMinX > tileMaxX ||
                     triMaxY < tileMinY || triMinY > tileMaxY);
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
            if(triangleScreenCoverage > 0.30f){
                RasterizeTriangleMultithreaded(v0, v1, v2, framebuffer, depthBuffer, shader, Environment.ProcessorCount);
            }
            else if (triangleScreenCoverage > 0.25f) // Triangle covers >25% of screen
            {
                RasterizeTriangleLinear(v0, v1, v2, framebuffer, depthBuffer, shader);
            }
            else if (triangleScreenCoverage > 0.01f) // Medium triangles
            {
                RasterizeTriangleSimdOnly(v0, v1, v2, framebuffer, depthBuffer, shader);
            }
            else // Small triangles
            {
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

                        if (pixelColor.a >= 254)
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

                            if (pixelColor.a >= 254)
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

                                    if (pixelColor.a >= 254)
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
