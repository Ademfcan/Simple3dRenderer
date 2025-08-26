using System.Numerics;
using Simple3dRenderer.Objects;

namespace Simple3dRenderer.Rendering
{
    class FrameDataWrapper<TState>(TState data) where TState : IRasterizable
    {
        public TState Data = data;
    }

    public static class Rasterizer
    {
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


        // NEW: Multithreaded large triangle rasterizer optimized for maximum performance
        public static void RasterizeTriangleMultithreaded<TProcessor, TState>(
            ref TState state, Vertex v0, Vertex v1, Vertex v2,
            int? threadCount = null)
            where TProcessor : struct, IFragmentProcessor<TProcessor, TState>
            where TState : IRasterizable
        {
            float x0 = v0.clipPosition.X, y0 = v0.clipPosition.Y;
            float x1 = v1.clipPosition.X, y1 = v1.clipPosition.Y;
            float x2 = v2.clipPosition.X, y2 = v2.clipPosition.Y;

            float area = Edge(x0, y0, x1, y1, x2, y2);
            if (area <= 0) return;

            int width = state.GetWidth();
            int height = state.GetHeight();

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

            int numThreads = threadCount ?? Environment.ProcessorCount;
            numThreads = Math.Min(numThreads, triangleHeight / 4); // Don't oversplit small triangles

            if (numThreads <= 1)
            {
                // Fallback to single-threaded for small or degenerate cases
                RasterizeTriangleLinear<TProcessor, TState>(ref state, v0, v1, v2);
                return;
            }

            float z0 = v0.clipPosition.Z, z1 = v1.clipPosition.Z, z2 = v2.clipPosition.Z;

            float dw0_dx = y2 - y1;
            float dw1_dx = y0 - y2;
            float dw2_dx = y1 - y0;
            // Pre-calculate depth change over X, normalized by area
            float dz_dx = ((z1 - z0) * dw1_dx + (z2 - z0) * dw2_dx) * invArea;




            float aspectRatio = (float)triangleWidth / triangleHeight;

            // Choose parallelization strategy based on triangle shape
            if (aspectRatio > 4.0f || triangleHeight < numThreads * 8)
            {
                RasterizeTriangleMultithreadedTiled<TProcessor, TState>(
                    ref state, v0, v1, v2,
                    minX, maxX, minY, maxY, invArea,
                    edge0IsTopLeft, edge1IsTopLeft, edge2IsTopLeft,
                    dw0_dx, dw1_dx, dw2_dx, dz_dx, numThreads);
            }
            else
            {
                RasterizeTriangleMultithreadedScanlines<TProcessor, TState>(
                    ref state, v0, v1, v2,
                    minX, maxX, minY, maxY, invArea,
                    edge0IsTopLeft, edge1IsTopLeft, edge2IsTopLeft,
                    dw0_dx, dw1_dx, dw2_dx, dz_dx, numThreads);
            }
        }

        // Scanline-parallel rasterization for tall triangles
        private static void RasterizeTriangleMultithreadedScanlines<TProcessor, TState>(
            ref TState state, Vertex v0, Vertex v1, Vertex v2,
            int minX, int maxX, int minY, int maxY, float invArea,
            bool edge0IsTopLeft, bool edge1IsTopLeft, bool edge2IsTopLeft,
            float dw0_dx, float dw1_dx, float dw2_dx, float dz_dx,
            int numThreads)
            where TProcessor : struct, IFragmentProcessor<TProcessor, TState>
            where TState : IRasterizable
        {
            float x0 = v0.clipPosition.X, y0 = v0.clipPosition.Y, z0 = v0.clipPosition.Z;
            float x1 = v1.clipPosition.X, y1 = v1.clipPosition.Y, z1 = v1.clipPosition.Z;
            float x2 = v2.clipPosition.X, y2 = v2.clipPosition.Y, z2 = v2.clipPosition.Z;

            int triangleHeight = maxY - minY + 1;
            int rowsPerThread = Math.Max(1, triangleHeight / numThreads);

            ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = numThreads };

            var frameWrapper = new FrameDataWrapper<TState>(state);

            Parallel.For(0, numThreads, parallelOptions, threadIndex =>
            {
                int startY = minY + threadIndex * rowsPerThread;
                int endY = (threadIndex == numThreads - 1)
                    ? maxY
                    : Math.Min(maxY, startY + rowsPerThread - 1);

                if (startY > maxY) return;

                int simdWidth = Vector<float>.Count;
                Vector<float> indexOffsets = GetIndexOffsets();

                for (int y = startY; y <= endY; y++)
                {
                    float py = y + 0.5f;
                    float w0_row = Edge(x1, y1, x2, y2, minX + 0.5f, py);
                    float w1_row = Edge(x2, y2, x0, y0, minX + 0.5f, py);
                    float w2_row = Edge(x0, y0, x1, y1, minX + 0.5f, py);
                    float z_row = (w0_row * z0 + w1_row * z1 + w2_row * z2) * invArea;

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
                            int xi = x + i;

                            if (EdgeTest(w0, edge0IsTopLeft) &&
                                EdgeTest(w1, edge1IsTopLeft) &&
                                EdgeTest(w2, edge2IsTopLeft))
                            {
                                float z = vz[i];
                                // normalize to 0-1
                                float fw0 = w0 * invArea;
                                float fw1 = w1 * invArea;
                                float fw2 = w2 * invArea;

                                TProcessor.ProcessFragment(ref frameWrapper.Data, xi, y, z, fw0, fw1, fw2, v0, v1, v2, false);

                                
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
        private static void RasterizeTriangleMultithreadedTiled<TProcessor, TState>(
            ref TState state, Vertex v0, Vertex v1, Vertex v2,
            int minX, int maxX, int minY, int maxY, float invArea,
            bool edge0IsTopLeft, bool edge1IsTopLeft, bool edge2IsTopLeft,
            float dw0_dx, float dw1_dx, float dw2_dx, float dz_dx,
            int numThreads)
            where TProcessor : struct, IFragmentProcessor<TProcessor, TState>
            where TState : IRasterizable
        {
            float x0 = v0.clipPosition.X, y0 = v0.clipPosition.Y, z0 = v0.clipPosition.Z;
            float x1 = v1.clipPosition.X, y1 = v1.clipPosition.Y, z1 = v1.clipPosition.Z;
            float x2 = v2.clipPosition.X, y2 = v2.clipPosition.Y, z2 = v2.clipPosition.Z;

            int triangleWidth = maxX - minX + 1;
            int triangleHeight = maxY - minY + 1;
            int tileSize = Math.Max(32, Math.Min(128, Math.Max(triangleWidth, triangleHeight) / (numThreads * 2)));

            var tiles = new List<(int startX, int endX, int startY, int endY)>();
            for (int tileY = minY; tileY <= maxY; tileY += tileSize)
            {
                for (int tileX = minX; tileX <= maxX; tileX += tileSize)
                {
                    int endX = Math.Min(tileX + tileSize - 1, maxX);
                    int endY = Math.Min(tileY + tileSize - 1, maxY);

                    if (!IsTileIntersectsTriangle(tileX, endX, tileY, endY, x0, y0, x1, y1, x2, y2))
                        continue;

                    tiles.Add((tileX, endX, tileY, endY));
                }
            }

            if (tiles.Count == 0) return;

            ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = numThreads };

            var frameWrapper = new FrameDataWrapper<TState>(state);

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
                            int xi = x + i;

                            if (EdgeTest(w0, edge0IsTopLeft) &&
                                EdgeTest(w1, edge1IsTopLeft) &&
                                EdgeTest(w2, edge2IsTopLeft))
                            {
                                float z = vz[i];
                                // normalize to 0-1
                                float fw0 = w0 * invArea;
                                float fw1 = w1 * invArea;
                                float fw2 = w2 * invArea;

                                TProcessor.ProcessFragment(ref frameWrapper.Data, xi, y, z, fw0, fw1, fw2, v0, v1, v2, false);
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


        static bool IsTileIntersectsTriangle(int tileMinX, int tileMaxX, int tileMinY, int tileMaxY,
            float x0, float y0, float x1, float y1, float x2, float y2)
        {
            float triMinX = MathF.Min(x0, MathF.Min(x1, x2));
            float triMaxX = MathF.Max(x0, MathF.Max(x1, x2));
            float triMinY = MathF.Min(y0, MathF.Min(y1, y2));
            float triMaxY = MathF.Max(y0, MathF.Max(y1, y2));

            return !(triMaxX < tileMinX || triMinX > tileMaxX ||
                     triMaxY < tileMinY || triMinY > tileMaxY);
        }

        public static void RasterizeTrianglesBatchOptimized<TProcessor, TState>(
            ref TState state, IEnumerable<(Vertex v1, Vertex v2, Vertex v3)> triangles, bool? sortFrontToBack)
             where TProcessor : struct, IFragmentProcessor<TProcessor, TState>
             where TState : IRasterizable
        {
            if (sortFrontToBack != null)
            {
                if (sortFrontToBack.Value)
                {
                    triangles = triangles.Select(t => new { Triangle = t, AvgZ = (t.v1.clipPosition.Z + t.v2.clipPosition.Z + t.v3.clipPosition.Z) / 3.0f })
                            .OrderBy(t => t.AvgZ) // Front to back
                            .Select(t => t.Triangle);
                }
                else {
                    triangles = triangles.Select(t => new { Triangle = t, AvgZ = (t.v1.clipPosition.Z + t.v2.clipPosition.Z + t.v3.clipPosition.Z) / 3.0f })
                             .OrderByDescending(t => t.AvgZ) // Back to front
                             .Select(t => t.Triangle);
                }
            }

            foreach (var (v1, v2, v3) in triangles)
            {
                RasterizeTriangleOptimized<TProcessor, TState>(ref state, v1, v2, v3);
            }
        }

        // Optimized batch: Sort triangles by depth for better early-z rejection
        public static void RasterizeTriangleOptimized<TProcessor, TState>(
            ref TState state, Vertex v0, Vertex v1, Vertex v2)
            where TProcessor : struct, IFragmentProcessor<TProcessor, TState>
            where TState : IRasterizable
        {
            float x0 = v0.clipPosition.X, y0 = v0.clipPosition.Y;
            float x1 = v1.clipPosition.X, y1 = v1.clipPosition.Y;
            float x2 = v2.clipPosition.X, y2 = v2.clipPosition.Y;

            float area = Edge(x0, y0, x1, y1, x2, y2);
            if (area <= 0) return;

            int width = state.GetWidth();
            int height = state.GetHeight();

            float screenArea = width * height;
            float triangleScreenCoverage = Math.Abs(area) / screenArea;

            RasterizeTriangleMultithreaded<TProcessor, TState>(ref state, v0, v1, v2, Environment.ProcessorCount);


            // // Choose rasterizer based on triangle screen coverage
            // if (triangleScreenCoverage > 0.30f)
            // {
            //    RasterizeTriangleMultithreaded<TProcessor, TState>(ref state, v0, v1, v2, Environment.ProcessorCount);
            // }
            // else if (triangleScreenCoverage > 0.25f)
            // {
            //     RasterizeTriangleLinear<TProcessor, TState>(ref state, v0, v1, v2);
            // }
            // else if (triangleScreenCoverage > 0.01f)
            // {
            //     RasterizeTriangleSimdOnly<TProcessor, TState>(ref state, v0, v1, v2);
            // }
            // else
            // {
            //     RasterizeTriangleSimd4<TProcessor, TState>(ref state, v0, v1, v2);
            // }
        }

        // Linear rasterization for very large triangles - no tiling overhead
        private static void RasterizeTriangleLinear<TProcessor, TState>(
            ref TState state, Vertex v0, Vertex v1, Vertex v2)
            where TProcessor : struct, IFragmentProcessor<TProcessor, TState>
            where TState : IRasterizable
        {
            float x0 = v0.clipPosition.X, y0 = v0.clipPosition.Y, z0 = v0.clipPosition.Z;
            float x1 = v1.clipPosition.X, y1 = v1.clipPosition.Y, z1 = v1.clipPosition.Z;
            float x2 = v2.clipPosition.X, y2 = v2.clipPosition.Y, z2 = v2.clipPosition.Z;

            float area = Edge(x0, y0, x1, y1, x2, y2);
            float invArea = 1.0f / area;

            bool edge0IsTopLeft = IsTopLeftEdge(x1, y1, x2, y2);
            bool edge1IsTopLeft = IsTopLeftEdge(x2, y2, x0, y0);
            bool edge2IsTopLeft = IsTopLeftEdge(x0, y0, x1, y1);

            int width = state.GetWidth();
            int height = state.GetHeight();

            int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(x0, MathF.Min(x1, x2))));
            int maxX = Math.Min(width - 1, (int)MathF.Ceiling(MathF.Max(x0, MathF.Max(x1, x2))));
            int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(y0, MathF.Min(y1, y2))));
            int maxY = Math.Min(height - 1, (int)MathF.Ceiling(MathF.Max(y0, MathF.Max(y1, y2))));

            float dw0_dx = y2 - y1;
            float dw1_dx = y0 - y2;
            float dw2_dx = y1 - y0;
            float dz_dx = ((z1 - z0) * dw1_dx + (z2 - z0) * dw2_dx) * invArea;

            for (int y = minY; y <= maxY; y++)
            {
                float py = y + 0.5f;
                float w0_row = Edge(x1, y1, x2, y2, minX + 0.5f, py);
                float w1_row = Edge(x2, y2, x0, y0, minX + 0.5f, py);
                float w2_row = Edge(x0, y0, x1, y1, minX + 0.5f, py);
                float z_row = (w0_row * z0 + w1_row * z1 + w2_row * z2) * invArea;

                int startX = minX;
                for (int x = minX; x <= maxX; x++)
                {
                    if (EdgeTest(w0_row, edge0IsTopLeft) && EdgeTest(w1_row, edge1IsTopLeft) && EdgeTest(w2_row, edge2IsTopLeft))
                    {
                        startX = x;
                        break;
                    }
                    w0_row += dw0_dx; w1_row += dw1_dx; w2_row += dw2_dx; z_row += dz_dx;
                }

                for (int x = startX; x <= maxX; x++)
                {
                    if (!(EdgeTest(w0_row, edge0IsTopLeft) && EdgeTest(w1_row, edge1IsTopLeft) && EdgeTest(w2_row, edge2IsTopLeft)))
                    {
                        break;
                    }

                    // normalize to 0-1
                    float fw0 = w0_row * invArea;
                    float fw1 = w1_row * invArea;
                    float fw2 = w2_row * invArea;

                    TProcessor.ProcessFragment(ref state, x, y, z_row, fw0, fw1, fw2, v0, v1, v2, false);

                    w0_row += dw0_dx; w1_row += dw1_dx; w2_row += dw2_dx; z_row += dz_dx;
                }
            }
        }

        // SIMD without tiling for medium triangles
        private static void RasterizeTriangleSimdOnly<TProcessor, TState>(
            ref TState state, Vertex v0, Vertex v1, Vertex v2)
            where TProcessor : struct, IFragmentProcessor<TProcessor, TState>
            where TState : IRasterizable
        {
            float x0 = v0.clipPosition.X, y0 = v0.clipPosition.Y, z0 = v0.clipPosition.Z;
            float x1 = v1.clipPosition.X, y1 = v1.clipPosition.Y, z1 = v1.clipPosition.Z;
            float x2 = v2.clipPosition.X, y2 = v2.clipPosition.Y, z2 = v2.clipPosition.Z;

            float area = Edge(x0, y0, x1, y1, x2, y2);
            float invArea = 1.0f / area;

            bool edge0IsTopLeft = IsTopLeftEdge(x1, y1, x2, y2);
            bool edge1IsTopLeft = IsTopLeftEdge(x2, y2, x0, y0);
            bool edge2IsTopLeft = IsTopLeftEdge(x0, y0, x1, y1);

            int width = state.GetWidth();
            int height = state.GetHeight();

            int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(x0, MathF.Min(x1, x2))));
            int maxX = Math.Min(width - 1, (int)MathF.Ceiling(MathF.Max(x0, MathF.Max(x1, x2))));
            int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(y0, MathF.Min(y1, y2))));
            int maxY = Math.Min(height - 1, (int)MathF.Ceiling(MathF.Max(y0, MathF.Max(y1, y2))));

            int simdWidth = Vector<float>.Count;
            Vector<float> indexOffsets = new Vector<float>(Enumerable.Range(0, simdWidth).Select(i => (float)i).ToArray());

            float dw0_dx = y2 - y1;
            float dw1_dx = y0 - y2;
            float dw2_dx = y1 - y0;
            float dz_dx = ((z1 - z0) * dw1_dx + (z2 - z0) * dw2_dx) * invArea;

            for (int y = minY; y <= maxY; y++)
            {
                float py = y + 0.5f;
                float w0_row = Edge(x1, y1, x2, y2, minX + 0.5f, py);
                float w1_row = Edge(x2, y2, x0, y0, minX + 0.5f, py);
                float w2_row = Edge(x0, y0, x1, y1, minX + 0.5f, py);
                float z_row = (w0_row * z0 + w1_row * z1 + w2_row * z2) * invArea;

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

                        if (EdgeTest(w0, edge0IsTopLeft) && EdgeTest(w1, edge1IsTopLeft) && EdgeTest(w2, edge2IsTopLeft))
                        {
                            float z = vz[i];
                            int xi = x + i;
                            // normalize to 0-1
                            float fw0 = w0 * invArea;
                            float fw1 = w1 * invArea;
                            float fw2 = w2 * invArea;

                            TProcessor.ProcessFragment(ref state, xi, y, z, fw0, fw1, fw2, v0, v1, v2, false);
                        }
                    }
                    w0_row += dw0_dx * simdWidth;
                    w1_row += dw1_dx * simdWidth;
                    w2_row += dw2_dx * simdWidth;
                    z_row += dz_dx * simdWidth;
                }
            }
        }

        public static void RasterizeTriangleSimd4<TProcessor, TState>(
            ref TState state, Vertex v0, Vertex v1, Vertex v2)
            where TProcessor : struct, IFragmentProcessor<TProcessor, TState>
            where TState : IRasterizable
        {
            float x0 = v0.clipPosition.X, y0 = v0.clipPosition.Y, z0 = v0.clipPosition.Z;
            float x1 = v1.clipPosition.X, y1 = v1.clipPosition.Y, z1 = v1.clipPosition.Z;
            float x2 = v2.clipPosition.X, y2 = v2.clipPosition.Y, z2 = v2.clipPosition.Z;

            float area = Edge(x0, y0, x1, y1, x2, y2);
            if (area <= 0) return;
            int tileSize = GetAdaptiveTileSize(Math.Abs(area));
            float invArea = 1.0f / area;

            bool edge0IsTopLeft = IsTopLeftEdge(x1, y1, x2, y2);
            bool edge1IsTopLeft = IsTopLeftEdge(x2, y2, x0, y0);
            bool edge2IsTopLeft = IsTopLeftEdge(x0, y0, x1, y1);

            int width = state.GetWidth();
            int height = state.GetHeight();

            int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(x0, MathF.Min(x1, x2))));
            int maxX = Math.Min(width - 1, (int)MathF.Ceiling(MathF.Max(x0, MathF.Max(x1, x2))));
            int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(y0, MathF.Min(y1, y2))));
            int maxY = Math.Min(height - 1, (int)MathF.Ceiling(MathF.Max(y0, MathF.Max(y1, y2))));

            int simdWidth = Vector<float>.Count;
            Vector<float> indexOffsets = new Vector<float>(Enumerable.Range(0, simdWidth).Select(i => (float)i).ToArray());

            float dw0_dx = y2 - y1;
            float dw1_dx = y0 - y2;
            float dw2_dx = y1 - y0;
            float dz_dx = ((z1 - z0) * dw1_dx + (z2 - z0) * dw2_dx) * invArea;

            bool IsTileFullyOutside(int tileX, int tileY)
            {
                float triMinX = MathF.Min(x0, MathF.Min(x1, x2));
                float triMaxX = MathF.Max(x0, MathF.Max(x1, x2));
                float triMinY = MathF.Min(y0, MathF.Min(y1, y2));
                float triMaxY = MathF.Max(y0, MathF.Max(y1, y2));

                float tileMinX = tileX;
                float tileMaxX = tileX + tileSize;
                float tileMinY = tileY;
                float tileMaxY = tileY + tileSize;

                return triMaxX < tileMinX || triMinX > tileMaxX || triMaxY < tileMinY || triMinY > tileMaxY;
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

                                if (EdgeTest(w0, edge0IsTopLeft) && EdgeTest(w1, edge1IsTopLeft) && EdgeTest(w2, edge2IsTopLeft))
                                {
                                    float z = vz[i];
                                    int xi = x + i;

                                    // normalize to 0-1
                                    float fw0 = w0 * invArea;
                                    float fw1 = w1 * invArea;
                                    float fw2 = w2 * invArea;

                                    TProcessor.ProcessFragment(ref state, xi, y, z, fw0, fw1, fw2, v0, v1, v2, false);
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
            if (triangleArea < 64) return 4;
            if (triangleArea < 256) return 8;
            if (triangleArea < 1024) return 16;
            if (triangleArea < 4096) return 32;
            return 64;
        }


    }
}