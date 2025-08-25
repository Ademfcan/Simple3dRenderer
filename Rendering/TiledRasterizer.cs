using System.Numerics;
using Simple3dRenderer.Objects;
using System.Collections.Concurrent; // Required for BlockingCollection
using System.Threading;           // Required for Thread and CountdownEvent

namespace Simple3dRenderer.Rendering
{
    public class TiledRasterizer<TProcessor, TState> : IDisposable
        where TProcessor : struct, IFragmentProcessor<TProcessor, TState>
        where TState : ITiledRasterizable<TState>
    {
        // --- Configuration ---
        private const int TILE_WIDTH = 32;
        private const int TILE_HEIGHT = 32;

        // --- Persistent Infrastructure (created once) ---
        private readonly int _width;
        private readonly int _height;
        private readonly int _numTilesX;
        private readonly int _numTilesY;
        private readonly Tile[] _tiles;
        private readonly ThreadLocal<TState> _localTileStates;
        private readonly StateWrapper<TState> _mainStateWrapper;
        // --- Thread Pool Infrastructure ---
        private readonly List<Thread> _workerThreads;
        private readonly BlockingCollection<Tile> _workQueue;
        private readonly CountdownEvent _countdownEvent;

        [ThreadStatic]
        private static Vector<float>? _threadLocalIndexOffsets;

        private class Tile
        {
            public readonly int MinX;
            public readonly int MinY;
            public readonly int MaxX;
            public readonly int MaxY;
            public readonly List<(Vertex v0, Vertex v1, Vertex v2)> Triangles = [];

            public Tile(int x, int y, int screenWidth, int screenHeight)
            {
                MinX = x * TILE_WIDTH;
                MinY = y * TILE_HEIGHT;
                MaxX = Math.Min(MinX + TILE_WIDTH - 1, screenWidth - 1);
                MaxY = Math.Min(MinY + TILE_HEIGHT - 1, screenHeight - 1);
            }
        }

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
            _localTileStates = new ThreadLocal<TState>(() =>
                _mainStateWrapper.State.CreateThreadLocalState(TILE_WIDTH, TILE_HEIGHT));

            // --- Initialize Thread Pool ---
            int workerCount = Environment.ProcessorCount;
            _workQueue = new BlockingCollection<Tile>();
            _countdownEvent = new CountdownEvent(1); // Start with a dummy count of 1
            _workerThreads = new List<Thread>(workerCount);

            for (int i = 0; i < workerCount; i++)
            {
                var thread = new Thread(WorkerLoop)
                {
                    IsBackground = true, // Ensure threads don't prevent application exit
                    Name = $"Rasterizer Worker {i}"
                };
                thread.Start();
                _workerThreads.Add(thread);
            }

        }

        /// <summary>
        /// The main loop for each persistent worker thread.
        /// </summary>
        private void WorkerLoop()
        {
            // The Take() method will block until an item is available or the queue is marked as complete.
            foreach (var tile in _workQueue.GetConsumingEnumerable())
            {
                try
                {
                    // Same work as before, but now in a dedicated thread loop.
                    var localState = _localTileStates.Value!;

                    localState.SyncPerFrameState(_mainStateWrapper.State);

                    localState.Reset();

                    foreach (var tri in tile.Triangles)
                    {
                        RasterizeTriangleInTile(tile, ref localState, tri.v0, tri.v1, tri.v2);
                    }

                    _mainStateWrapper.State.MergeTile(localState, tile.MinX, tile.MinY);
                }
                finally
                {
                    // Signal that this piece of work is done.
                    _countdownEvent.Signal();
                }
            }
        }

        public void Render(IEnumerable<(Vertex v0, Vertex v1, Vertex v2)> triangles)
        {
            _mainStateWrapper.State.Reset();
            BinTriangles(triangles);
            ProcessTiles();
        }

        private void ProcessTiles()
        {
            var activeTiles = _tiles.Where(t => t.Triangles.Count > 0).ToList();
            if (activeTiles.Count == 0) return;

            // 1. Reset the countdown for the new frame's work.
            _countdownEvent.Reset(activeTiles.Count);

            // 2. Add all active tiles to the queue for the worker threads to pick up.
            foreach (var tile in activeTiles)
            {
                _workQueue.Add(tile);
            }

            // 3. Wait until the countdown reaches zero, meaning all tiles have been processed.
            _countdownEvent.Wait();
        }

        public void Dispose()
        {
            // Signal that no more items will be added to the queue.
            // This will cause GetConsumingEnumerable() to end in the worker threads.
            _workQueue.CompleteAdding();

            foreach (var thread in _workerThreads)
            {
                thread.Join(); // Wait for each thread to finish gracefully.
            }

            _workQueue.Dispose();
            _countdownEvent.Dispose();
            _localTileStates.Dispose();
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

            float p_start_x = minX + 0.5f;
            float p_start_y = minY + 0.5f;

            float w0_row = Edge(x1, y1, x2, y2, p_start_x, p_start_y);
            float w1_row = Edge(x2, y2, x0, y0, p_start_x, p_start_y);
            float w2_row = Edge(x0, y0, x1, y1, p_start_x, p_start_y);

            float dw0_dx = y2 - y1; float dw0_dy = x1 - x2;
            float dw1_dx = y0 - y2; float dw1_dy = x2 - x0;
            float dw2_dx = y1 - y0; float dw2_dy = x0 - x1;

            int simdWidth = Vector<float>.Count;
            Vector<float> indexOffsets = GetIndexOffsets();

            Vector<float> v_z = new Vector<float>(new float[] { v0.clipPosition.Z, v1.clipPosition.Z, v2.clipPosition.Z, 0, 0, 0, 0, 0 });

            for (int y = minY; y <= maxY; y++)
            {
                float w0 = w0_row;
                float w1 = w1_row;
                float w2 = w2_row;

                for (int x = minX; x <= maxX; x += simdWidth)
                {
                    // 1. Barycentric coordinates for 8 pixels
                    Vector<float> vw0 = new Vector<float>(w0) + indexOffsets * dw0_dx;
                    Vector<float> vw1 = new Vector<float>(w1) + indexOffsets * dw1_dx;
                    Vector<float> vw2 = new Vector<float>(w2) + indexOffsets * dw2_dx;

                    // 2. Triangle coverage mask
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

                    // 3. Vectorized Depth Test (This requires the FrameData changes from step 3)
                    // Assuming localState has a public float[] depthBuffer and int width
                    // This part is conceptual until FrameData is flattened.
                    // float[] localDepthBuffer = localState.depthBuffer;
                    // int tileWidth = localState.getWidth();
                    // Vector<float> existingDepths = new Vector<float>(localDepthBuffer, (y - tile.MinY) * tileWidth + (x - tile.MinX));

                    // Vector<float> normalized_w0 = vw0 * invArea;
                    // Vector<float> normalized_w1 = vw1 * invArea;
                    // Vector<float> normalized_w2 = vw2 * invArea;
                    // Vector<float> pixelDepth = normalized_w0 * v_z[0] + normalized_w1 * v_z[1] + normalized_w2 * v_z[2];

                    // Vector<int> depthMask = Vector.LessThan(pixelDepth, existingDepths);
                    // coverageMask &= depthMask;


                    // 4. Fallback to scalar loop on ONLY the pixels that passed the mask
                    int remaining = Math.Min(simdWidth, maxX - x + 1);
                    for (int i = 0; i < remaining; i++)
                    {
                        // Test the mask for this specific pixel
                        if ((coverageMask[i] & 0x1) != 0)
                        {
                            float cur_w0 = vw0[i];
                            float cur_w1 = vw1[i];
                            float cur_w2 = vw2[i];

                            // Top-left rule check
                            if ((cur_w0 > 0 || (cur_w0 == 0 && edge0IsTopLeft)) &&
                                (cur_w1 > 0 || (cur_w1 == 0 && edge1IsTopLeft)) &&
                                (cur_w2 > 0 || (cur_w2 == 0 && edge2IsTopLeft)))
                            {
                                float fw0 = cur_w0 * invArea;
                                float fw1 = cur_w1 * invArea;
                                float fw2 = cur_w2 * invArea;
                                float z = fw0 * z0 + fw1 * z1 + fw2 * z2;

                                // The fragment processor now does the depth test and write
                                TProcessor.ProcessFragment(ref localState, x + i - tile.MinX, y - tile.MinY, z, fw0, fw1, fw2, v0, v1, v2, isMultithreaded: false);
                            }
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

        private static float Edge(float ax, float ay, float bx, float by, float cx, float cy)
        {
            return (cx - ax) * (by - ay) - (cy - ay) * (bx - ax);
        }

        private static Vector<float> GetIndexOffsets()
        {
            _threadLocalIndexOffsets ??= new Vector<float>(
              Enumerable.Range(0, Vector<float>.Count).Select(i => (float)i).ToArray());
            return _threadLocalIndexOffsets.Value;
        }
    }
}