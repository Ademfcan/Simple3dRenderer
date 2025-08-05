using System.Runtime.Intrinsics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization.LineSearch;
using SDL;
using Simple3dRenderer;

namespace Simple3dRenderer
{
    public static class Pipeline
    {

        public static SDL_Color[,] Run(Camera camera, Mesh mesh, SDL_Color backgroundColor = default)
        {
            Matrix<float> vertexes = mesh.GetVertexMatrix();
            PrintMatrix(vertexes, nameof(vertexes));

            Matrix<float> objecttoworld = mesh.GetModelMatrix().ToMathNet();
            PrintMatrix(objecttoworld, nameof(objecttoworld));

            Matrix<float> worldVertexes = objecttoworld * vertexes;

            PrintMatrix(worldVertexes, nameof(worldVertexes));

            Matrix<float> worldtoview = camera.getViewMatrix().ToMathNet();

            PrintMatrix(worldtoview, nameof(worldtoview));

            Matrix<float> viewVertexes = worldtoview * worldVertexes;

            PrintMatrix(viewVertexes, nameof(viewVertexes));

            Matrix<float> viewtoclip = camera.getProjectionMatrix().ToMathNet();

            PrintMatrix(viewtoclip, nameof(viewtoclip));

            Matrix<float> clipVertexes = viewtoclip * viewVertexes;

            PrintMatrix(clipVertexes, nameof(clipVertexes));

            var triangles = FilterClipVertexes(clipVertexes, mesh.indices);

            foreach (var triangle in triangles)
            {
                PrintVectors("Filtered triangles", triangle.v1, triangle.v2, triangle.v3);
            }

            Console.WriteLine("Finished filtering!");

            ApplyPerspectiveDivide(triangles);

            foreach (var triangle in triangles)
            {
                PrintVectors("Ndc triangles", triangle.v1, triangle.v2, triangle.v3);
            }

            Console.WriteLine("Finished to ndc!");

            NdcToScreen(triangles, in camera.HRes, in camera.VRes);

            foreach (var triangle in triangles)
            {
                PrintVectors("Screen triangles", triangle.v1, triangle.v2, triangle.v3);
            }

            Console.WriteLine("Finished ndc to screen");

            SDL_Color[,] frame_buf = new SDL_Color[camera.VRes, camera.HRes];
            float[,] depth_buf = new float[camera.VRes, camera.HRes];

            // Initialize depth buffer to far depth (float.MaxValue)
            // Initialize the frame buffer to background color
            for (int y = 0; y < camera.VRes; y++)
            {
                for (int x = 0; x < camera.HRes; x++)
                {
                    depth_buf[y, x] = float.MaxValue;
                    frame_buf[y, x] = backgroundColor;
                }
            }


            foreach (var (v1, v2, v3) in triangles)
            {
                Rasterizer.RasterizeTriangle(v1, v2, v3, frame_buf, depth_buf, FlatWhiteShader);
            }

            Console.WriteLine("Finished Rasterize!");


            Console.WriteLine("Finished!");

            PrintFrameBuffer(frame_buf);

            return frame_buf;
        }

        public static SDL_Color FlatWhiteShader(Vector<float> p, Vector<float> v0, Vector<float> v1, Vector<float> v2, float w0, float w1, float w2)
        {
            return new SDL_Color { r = 255, g = 255, b = 255, a = 255 };
        }


        private static List<(Vector<float> v1, Vector<float> v2, Vector<float> v3)> FilterClipVertexes(Matrix<float> ClipVertexes, List<(int, int, int)> indexes)
        {
            var triangles = new List<(Vector<float> v1, Vector<float> v2, Vector<float> v3)>();

            foreach ((int i1, int i2, int i3) in indexes)
            {
                triangles.Add((ClipVertexes.Column(i1), ClipVertexes.Column(i2), ClipVertexes.Column(i3)));

            }


            return Clipper.ClipTriangles(triangles);
        }


        private static void ApplyPerspectiveDivide(List<(Vector<float> v1, Vector<float> v2, Vector<float> v3)> vertexes)
        {
            for (int i = 0; i < vertexes.Count; i++)
            {
                var (v1, v2, v3) = vertexes[i];

                vertexes[i] = (
                    DivideByWAndTrim(v1),
                    DivideByWAndTrim(v2),
                    DivideByWAndTrim(v3)
                );
            }
        }

        private static Vector<float> DivideByWAndTrim(Vector<float> v)
        {
            int n = v.Count;
            float w = v[n - 1];

            var trimmed = Vector<float>.Build.Dense(n - 1);
            for (int i = 0; i < n - 1; i++)
            {
                trimmed[i] = v[i] / w;
            }

            return trimmed;
        }



        private static void NdcToScreen(List<(Vector<float> v1, Vector<float> v2, Vector<float> v3)> ndc, ref readonly int width, ref readonly int height)
        {
            for (int i = 0; i < ndc.Count; i++)
            {
                var (v1, v2, v3) = ndc[i];
                ndc[i] = (ScaleVertex(v1, width, height), ScaleVertex(v2, width, height), ScaleVertex(v3, width, height));
            }
        }


        private static Vector<float> ScaleVertex(Vector<float> vertex, int width, int height)
        {
            float x = (vertex[0] + 1) * 0.5f * width;
            float y = (1 - vertex[1]) * 0.5f * height;
            float z = (vertex[2] + 1) * 0.5f;

            return Vector<float>.Build.DenseOfArray([x, y, z]);
        }

        public static void PrintMatrix(Matrix<float> matrix, string? name = null)
        {
            if (!string.IsNullOrEmpty(name))
                Console.WriteLine($"Matrix: {name}");

            int rows = matrix.RowCount;
            int cols = matrix.ColumnCount;

            for (int r = 0; r < rows; r++)
            {
                Console.Write("[ ");
                for (int c = 0; c < cols; c++)
                {
                    Console.Write($"{matrix[r, c],8:0.####} ");
                }
                Console.WriteLine("]");
            }

            Console.WriteLine();
        }

        public static void PrintVectors(string title, params Vector<float>[] vectors)
        {
            Console.WriteLine($"--- {title} ---");
            for (int i = 0; i < vectors.Length; i++)
            {
                Console.WriteLine($"[{i}] {vectors[i]}");
            }
            Console.WriteLine();
        }

        public static void PrintFrameBuffer(SDL_Color[,] frameBuffer)
        {
            int height = frameBuffer.GetLength(0);
            int width = frameBuffer.GetLength(1);

            int c = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    SDL_Color pixel = frameBuffer[y, x];
                    int brightness = pixel.r + pixel.g + pixel.b;

                    if (brightness > 0)
                    {
                        c++;
                    }
                }
            }
            
            Console.WriteLine("C pixels!: " + c);

        }

    }
}