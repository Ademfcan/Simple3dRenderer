using System.Numerics;
using MathNet.Numerics.LinearAlgebra;
using SDL;
using Simple3dRenderer.Shaders;
using Simple3dRenderer.Objects;
using Simple3dRenderer.Extensions;


namespace Simple3dRenderer.Rendering
{
    public static class Pipeline
    {

        private static readonly TextureShader shader = new();

        public static SDL_Color[,] RenderScene(Camera camera, List<Mesh> scene, SDL_Color backgroundColor = default)
        {
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

            Matrix<float> worldtoview = camera.getViewMatrix().ToMathNet();
            Matrix<float> viewtoclip = camera.getProjectionMatrix().ToMathNet();
            Matrix<float> mmtv = viewtoclip * worldtoview;

            List<Mesh> opaques = [];
            List<Mesh> transparents = [];

            foreach (Mesh mesh in scene)
            {
                if (mesh.IsOpaque())
                {
                    opaques.Add(mesh);
                }
                else
                {
                    transparents.Add(mesh);
                }
            }

            // all opaques first
            // then transparents

            foreach (Mesh mesh in opaques)
            {
                RenderMesh(camera, mesh, frame_buf, depth_buf, mmtv);
            }

            foreach (Mesh mesh in transparents)
            {
                RenderMesh(camera, mesh, frame_buf, depth_buf, mmtv);
            }

            return frame_buf;
        }

        private static void RenderMesh(Camera camera, Mesh mesh, SDL_Color[,] frame_buf, float[,] depth_buf, Matrix<float> mmtv)
        {

            Matrix<float> vertexes = mesh.GetVertexMatrix();

            Matrix<float> objecttoworld = mesh.GetModelMatrix().ToMathNet();


            Matrix<float> mmtc = mmtv * objecttoworld;

            Matrix<float> clipVertexes = mmtc * vertexes;

            var triangles = FilterClipVertexes(clipVertexes, mesh.originalVertexes, mesh.indices);

            ApplyPerspectiveDivide(triangles);

            NdcToScreen(triangles, in camera.HRes, in camera.VRes);

            // Rasterizer.RasterizeTrianglesBatch(triangles, frame_buf, depth_buf, GradientShader);
            if (mesh.texture != null)
            {
                shader.texture = mesh.texture;
                Rasterizer.RasterizeTrianglesBatchOptimized(triangles, frame_buf, depth_buf, shader.TexturedShader, mesh.IsOpaque());
            }
            else
            {
                Rasterizer.RasterizeTrianglesBatchOptimized(triangles, frame_buf, depth_buf, GradientShader, mesh.IsOpaque());
            }
        }

        public static SDL_Color GradientShader(Vertex v0, Vertex v1, Vertex v2, float w0, float w1, float w2)
        {
            return SDLColorExtensions.Interpolate(v0.Color, v1.Color, v2.Color, w0, w1, w2);
        }



        private static List<(Vertex v1, Vertex v2, Vertex v3)> FilterClipVertexes(Matrix<float> ClipVertexes, List<Vertex> originalVerteces, List<(int, int, int)> indexes)
        {
            var triangles = new List<(Vertex v1, Vertex v2, Vertex v3)>();

            foreach ((int i1, int i2, int i3) in indexes)
            {
                Vertex v1 = originalVerteces[i1];
                Vertex v2 = originalVerteces[i2];
                Vertex v3 = originalVerteces[i3];

                // update position
                var c1 = ClipVertexes.Column(i1);
                v1.Position = new Vector4(c1[0], c1[1], c1[2], c1[3]);

                // update position
                var c2 = ClipVertexes.Column(i2);
                v2.Position = new Vector4(c2[0], c2[1], c2[2], c2[3]);

                // update position
                var c3 = ClipVertexes.Column(i3);
                v3.Position = new Vector4(c3[0], c3[1], c3[2], c3[3]);


                triangles.Add((v1, v2, v3));

            }


            return Clipper.ClipTriangles(triangles);
        }


        private static void ApplyPerspectiveDivide(List<(Vertex v1, Vertex v2, Vertex v3)> vertexes)
        {
            for (int i = 0; i < vertexes.Count; i++)
            {
                var (v1, v2, v3) = vertexes[i];

                DivideByWAndTrim(ref v1);
                DivideByWAndTrim(ref v2);
                DivideByWAndTrim(ref v3);

                vertexes[i] = (v1, v2, v3);
            }
        }

        private static void DivideByWAndTrim(ref Vertex v)
        {
            float w = v.Position.W;
            float x = v.Position.X, y = v.Position.Y, z = v.Position.Z;

            v.Position = new Vector4(x / w, y / w, z / w, 1);
        }



        private static void NdcToScreen(List<(Vertex v1, Vertex v2, Vertex v3)> ndc, ref readonly int width, ref readonly int height)
        {
            for (int i = 0; i < ndc.Count; i++)
            {
                var (v1, v2, v3) = ndc[i];
                ScaleVertex(ref v1, width, height);
                ScaleVertex(ref v2, width, height);
                ScaleVertex(ref v3, width, height);

                ndc[i] = (v1, v2, v3);
            }
        }


        private static void ScaleVertex(ref Vertex vertex, int width, int height)
        {

            float x = (vertex.Position.X + 1) * 0.5f * width;
            float y = (1 - vertex.Position.Y) * 0.5f * height;
            float z = vertex.Position.Z;

            vertex.Position = new Vector4(x, y, z, 1);
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