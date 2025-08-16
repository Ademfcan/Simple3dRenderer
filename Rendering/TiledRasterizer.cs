using System.Numerics;
using Simple3dRenderer.Objects;

namespace Simple3dRenderer.Rendering
{
    /// <summary>
    /// A high-performance, contention-free, tiled-based rasterizer engine.
    /// This rasterizer is a stateless engine that operates on a given TState object each frame.
    /// It partitions the screen into tiles and processes them in parallel.
    /// Each thread works on its own private buffers, eliminating the massive performance
    /// cost of atomic operations and cache contention found in fine-grained parallelism.
    /// </summary>
    public class TiledRasterizer<TProcessor, TState>
        where TProcessor : struct, IFragmentProcessor<TProcessor, TState>
        where TState : ITiledRasterizable<TState>
    {
        // --- Configuration ---
        private const int TILE_WIDTH = 80;
        private const int TILE_HEIGHT = 80;

        // --- Persistent Infrastructure (created once) ---
        private readonly int _width;
        private readonly int _height;
        private readonly int _numTilesX;
        private readonly int _numTilesY;
        private readonly Tile[] _tiles;
        private readonly ParallelOptions _parallelOptions;

        // --- Thread-local storage for performance ---
        // This gives each thread its own reusable state object and buffers,
        // avoiding constant reallocation.
        private readonly ThreadLocal<TState> _localTileStates;

        [ThreadStatic]
        private static Vector<float>? _threadLocalIndexOffsets;

        private readonly StateWrapper<TState> _mainStateWrapper;

        private class Tile
        {
            public readonly int MinX;
            public readonly int MinY;
            public readonly int MaxX;
            public readonly int MaxY;
            // List is pre-allocated once and cleared each frame, avoiding GC pressure.
            public readonly List<(Vertex v0, Vertex v1, Vertex v2)> Triangles = new(256);

            public Tile(int x, int y, int screenWidth, int screenHeight)
            {
                MinX = x * TILE_WIDTH;
                MinY = y * TILE_HEIGHT;
                MaxX = Math.Min(MinX + TILE_WIDTH - 1, screenWidth - 1);
                MaxY = Math.Min(MinY + TILE_HEIGHT - 1, screenHeight - 1);
            }
        }

        /// <summary>
        /// Creates a reusable Tiled Rasterizer engine.
        /// </summary>
        /// <param name="screenWidth">The width of the render target.</param>
        /// <param name="screenHeight">The height of the render target.</param>
        /// <param name="templateState">A sample TState object used as a factory for creating thread-local states.</param>
        public TiledRasterizer(int screenWidth, int screenHeight, StateWrapper<TState> mainStateWrapper)
        {
            _width = screenWidth;
            _height = screenHeight;

            _numTilesX = (_width + TILE_WIDTH - 1) / TILE_WIDTH;
            _numTilesY = (_height + TILE_HEIGHT - 1) / TILE_HEIGHT;

            _tiles = new Tile[_numTilesX * _numTilesY];
            for (int y = 0; y < _numTilesY; y++)
            {
                for (int x = 0; x < _numTilesX; x++)
                {
                    _tiles[y * _numTilesX + x] = new Tile(x, y, _width, _height);
                }
            }

            _mainStateWrapper = mainStateWrapper;

            _parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

            // The ThreadLocal initialization is now beautifully simple and encapsulated!
            _localTileStates = new ThreadLocal<TState>(() =>
                _mainStateWrapper.State.CreateThreadLocalState(TILE_WIDTH, TILE_HEIGHT));

        }

        /// <summary>
        /// Renders a collection of triangles to the provided main state object for one frame.
        /// </summary>
        public void Render(IEnumerable<(Vertex v0, Vertex v1, Vertex v2)> triangles)
        {
            // Console.WriteLine("Resetting state");
            _mainStateWrapper.State.Reset();
            // Console.WriteLine("Binning triangles");
            BinTriangles(triangles);
            // Console.WriteLine("Processing triangles");
            ProcessTiles();
        }

        private void BinTriangles(IEnumerable<(Vertex v0, Vertex v1, Vertex v2)> triangles)
        {
            foreach (Tile tile in _tiles)
            {
                tile.Triangles.Clear();
            }

            // This is a serial step, but it's very fast (mostly bounding box checks).
            foreach (var tri in triangles)
            {
                float minX = MathF.Min(tri.v0.clipPosition.X, MathF.Min(tri.v1.clipPosition.X, tri.v2.clipPosition.X));
                float maxX = MathF.Max(tri.v0.clipPosition.X, MathF.Max(tri.v1.clipPosition.X, tri.v2.clipPosition.X));
                float minY = MathF.Min(tri.v0.clipPosition.Y, MathF.Min(tri.v1.clipPosition.Y, tri.v2.clipPosition.Y));
                float maxY = MathF.Max(tri.v0.clipPosition.Y, MathF.Max(tri.v1.clipPosition.Y, tri.v2.clipPosition.Y));

                int startTileX = Math.Max(0, (int)(minX / TILE_WIDTH));
                int endTileX = Math.Min(_numTilesX - 1, (int)(maxX / TILE_WIDTH));
                int startTileY = Math.Max(0, (int)(minY / TILE_HEIGHT));
                int endTileY = Math.Min(_numTilesY - 1, (int)(maxY / TILE_HEIGHT));

                for (int y = startTileY; y <= endTileY; y++)
                {
                    for (int x = startTileX; x <= endTileX; x++)
                    {
                        _tiles[y * _numTilesX + x].Triangles.Add(tri);
                    }
                }
            }
        }

        private void ProcessTiles()
        {
            var activeTiles = _tiles.Where(t => t.Triangles.Count > 0).ToList();

            // Console.WriteLine($"{activeTiles.Count} active tiles");

            Parallel.ForEach(activeTiles, _parallelOptions, tile =>
            {
                if (tile.Triangles.Count == 0) return;

                var localState = _localTileStates.Value!;
                localState.Reset();

                // THE EXPLICIT CLEAR LOOP IS GONE!
                // var localDepth = localState.depthBuffer;
                // for (int y = 0; y < TILE_HEIGHT; y++)
                // for (int x = 0; x < TILE_WIDTH; x++)
                //     localDepth[y, x] = float.MaxValue;

                // Console.WriteLine("Rasterizing in a tile!");

                foreach (var tri in tile.Triangles)
                {
                    RasterizeTriangleInTile(tile, ref localState, tri.v0, tri.v1, tri.v2);
                }

                // Console.WriteLine("Merging tiles!");

                _mainStateWrapper.State.MergeTile(localState, tile.MinX, tile.MinY);



                // CALL THE NEW COMBINED METHOD
            });
        }

        private static float Edge(float ax, float ay, float bx, float by, float cx, float cy)
        {
            return (cx - ax) * (by - ay) - (cy - ay) * (bx - ax);
        }

        private void RasterizeTriangleInTile(Tile tile, ref TState localState, Vertex v0, Vertex v1, Vertex v2)
        {
            float x0 = v0.clipPosition.X, y0 = v0.clipPosition.Y, z0 = v0.clipPosition.Z;
            float x1 = v1.clipPosition.X, y1 = v1.clipPosition.Y, z1 = v1.clipPosition.Z;
            float x2 = v2.clipPosition.X, y2 = v2.clipPosition.Y, z2 = v2.clipPosition.Z;

            float area = Edge(x0, y0, x1, y1, x2, y2);
            if (area <= 0) return;
            float invArea = 1.0f / area;

            bool edge0IsTopLeft = (y1 == y2 && x2 > x1) || y2 > y1; // v1-v2
            bool edge1IsTopLeft = (y2 == y0 && x0 > x2) || y0 > y2; // v2-v0
            bool edge2IsTopLeft = (y0 == y1 && x1 > x0) || y1 > y0; // v0-v1

            int minX = Math.Max(tile.MinX, (int)MathF.Floor(MathF.Min(x0, MathF.Min(x1, x2))));
            int maxX = Math.Min(tile.MaxX, (int)MathF.Ceiling(MathF.Max(x0, MathF.Max(x1, x2))));
            int minY = Math.Max(tile.MinY, (int)MathF.Floor(MathF.Min(y0, MathF.Min(y1, y2))));
            int maxY = Math.Min(tile.MaxY, (int)MathF.Ceiling(MathF.Max(y0, MathF.Max(y1, y2))));

            if (minX > maxX || minY > maxY) return;

            // Use the known-good Edge function to find the starting values
            float p_start_x = minX + 0.5f;
            float p_start_y = minY + 0.5f;

            // --- REPLACED WITH EDGE FUNCTION ---
            float w0_row = Edge(x1, y1, x2, y2, p_start_x, p_start_y);
            float w1_row = Edge(x2, y2, x0, y0, p_start_x, p_start_y);
            float w2_row = Edge(x0, y0, x1, y1, p_start_x, p_start_y);

            // The incremental updates are likely correct, as they are standard.
            float dw0_dx = y2 - y1; float dw0_dy = x1 - x2;
            float dw1_dx = y0 - y2; float dw1_dy = x2 - x0;
            float dw2_dx = y1 - y0; float dw2_dy = x0 - x1;

            int simdWidth = Vector<float>.Count;
            Vector<float> indexOffsets = GetIndexOffsets();

            for (int y = minY; y <= maxY; y++)
            {
                float w0 = w0_row;
                float w1 = w1_row;
                float w2 = w2_row;

                for (int x = minX; x <= maxX; x += simdWidth)
                {
                    Vector<float> vw0 = new Vector<float>(w0) + indexOffsets * dw0_dx;
                    Vector<float> vw1 = new Vector<float>(w1) + indexOffsets * dw1_dx;
                    Vector<float> vw2 = new Vector<float>(w2) + indexOffsets * dw2_dx;

                    // With the known-good initialization, we can trust the weights are positive inside.
                    Vector<int> coverageMask = Vector.GreaterThanOrEqual(vw0, Vector<float>.Zero) &
                                               Vector.GreaterThanOrEqual(vw1, Vector<float>.Zero) &
                                               Vector.GreaterThanOrEqual(vw2, Vector<float>.Zero);


                    if (Vector.EqualsAll(coverageMask, Vector<int>.Zero))
                    {
                        w0 += dw0_dx * simdWidth;
                        w1 += dw1_dx * simdWidth;
                        w2 += dw2_dx * simdWidth;
                        continue;
                    }

                    int remaining = Math.Min(simdWidth, maxX - x + 1);
                    for (int i = 0; i < remaining; i++)
                    {
                        int currentX = x + i;
                        float cur_w0 = vw0[i];
                        float cur_w1 = vw1[i];
                        float cur_w2 = vw2[i];

                        if ((cur_w0 > 0 || (cur_w0 == 0 && edge0IsTopLeft)) &&
                            (cur_w1 > 0 || (cur_w1 == 0 && edge1IsTopLeft)) &&
                            (cur_w2 > 0 || (cur_w2 == 0 && edge2IsTopLeft)))
                        {
                            float fw0 = cur_w0 * invArea;
                            float fw1 = cur_w1 * invArea;
                            float fw2 = cur_w2 * invArea;
                            float z = fw0 * z0 + fw1 * z1 + fw2 * z2;

                            TProcessor.ProcessFragment(ref localState, currentX - tile.MinX, y - tile.MinY, z, fw0, fw1, fw2, v0, v1, v2, isMultithreaded: false);
                        }
                    }

                    w0 += dw0_dx * simdWidth;
                    w1 += dw1_dx * simdWidth;
                    w2 += dw2_dx * simdWidth;
                }
                w0_row += dw0_dy;
                w1_row += dw1_dy;
                w2_row += dw2_dy;
            }

        }

        private static Vector<float> GetIndexOffsets()
        {
            _threadLocalIndexOffsets ??= new Vector<float>(
                Enumerable.Range(0, Vector<float>.Count).Select(i => (float)i).ToArray());
            return _threadLocalIndexOffsets.Value;
        }
    }
}