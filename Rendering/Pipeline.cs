using System.Numerics;
using MathNet.Numerics.LinearAlgebra;
using SDL;
using Simple3dRenderer.Objects;
using Simple3dRenderer.Extensions;
using Simple3dRenderer.Textures;
using Simple3dRenderer.Lighting;


namespace Simple3dRenderer.Rendering
{
    public static class Pipeline
    {
        public static SDL_Color[,] RenderScene(Scene scene)
        {
            List<DeepShadowMap> maps = [];

            foreach (IPerspective light in scene.lights)
            {
                maps.Add(createShadowMap(scene.objects, light));
            }

            FrameData state = FrameData.createEmpty(scene.camera.HRes, scene.camera.VRes, scene.backgroundColor, scene.ambientLight, scene.camera.Position, maps, scene.lights);
            List<Mesh> opaques = [];
            List<Mesh> transparents = [];

            foreach (Mesh mesh in scene.objects)
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

            if (opaques.Count > 100 || scene.lights.Count > 0)
            {
                // make use of depth pre pass
                RasterizeScene<DepthPrePassProcessor, FrameData>(scene.camera, opaques, ref state, false);
            }

            RasterizeScene<FrameFragmentProcessor<MaterialShader<FrameData>>, FrameData>(scene.camera, opaques, ref state, false, scene.lights);
            RasterizeScene<FrameFragmentProcessor<MaterialShader<FrameData>>, FrameData>(scene.camera, transparents, ref state, true, scene.lights);


            Console.WriteLine(state.w);

            return state.frameBuffer;
            // return maps[0].ToColorArray();
        }

        public static DeepShadowMap createShadowMap(List<Mesh> objects, IPerspective perspective)
        {
        
            LightData lightData = LightData.create(perspective);

            RasterizeScene<LightFragmentProcessor<MaterialShader<LightData>>, LightData>(perspective, objects, ref lightData, false);

            lightData.shadowMap.Initialize();

            return lightData.shadowMap;
        }

        public static void RasterizeScene<TProcessor, TState>
     (
         IPerspective perspective,
         List<Mesh> objects,
         ref TState state,
         bool sortOnOpaqueNess,
         List<PerspectiveLight>? lights = null
      )
          where TProcessor : struct, IFragmentProcessor<TProcessor, TState>
          where TState : IRasterizable, ITextured
        {
            Matrix<float> worldtoview = perspective.getViewMatrix().ToMathNet();
            Matrix<float> viewtoclip = perspective.getProjectionMatrix().ToMathNet();
            Matrix<float> mmtv = viewtoclip * worldtoview;


            foreach (Mesh mesh in objects)
            {
                state.SetTexture(mesh.texture);
                RenderMesh<TProcessor, TState>(perspective.getWidth(), perspective.getHeight(), mesh, ref state, mmtv, sortOnOpaqueNess ? mesh.IsOpaque() : null, lights);
            }

        }

        private static void RenderMesh<TProcessor, TState>
        (int width, int height, Mesh mesh, ref TState state, Matrix<float> wtoc, bool? sortFrontToBack, List<PerspectiveLight>? lights = null)
        where TProcessor : struct, IFragmentProcessor<TProcessor, TState>
            where TState : IRasterizable
        {
            Matrix<float> vertexes = mesh.GetVertexMatrix();
            Matrix<float> objecttoworld = mesh.GetModelMatrix().ToMathNet();

            Matrix<float> worldVertexes = objecttoworld * vertexes;

            Matrix<float> clipVertexes = wtoc * worldVertexes;

            var triangles = FilterClipVertexes(clipVertexes, worldVertexes, mesh.Vertexes, mesh.indices);

            if (lights != null)
                AddOptionalLightClips(triangles, lights);

            ApplyPerspectiveDivide(triangles);
            NdcToScreen(triangles, width, height);

            Rasterizer.RasterizeTrianglesBatchOptimized<TProcessor, TState>(ref state, triangles, sortFrontToBack);
        }





        private static List<(Vertex v1, Vertex v2, Vertex v3)> FilterClipVertexes(Matrix<float> ClipVertexes, Matrix<float> WorldVertexes, List<Vertex> originalVerteces, List<(int, int, int)> indexes)
        {
            var triangles = new List<(Vertex v1, Vertex v2, Vertex v3)>();

            foreach ((int i1, int i2, int i3) in indexes)
            {
                Vertex v1 = originalVerteces[i1];
                Vertex v2 = originalVerteces[i2];
                Vertex v3 = originalVerteces[i3];

                // update position
                var c1 = ClipVertexes.Column(i1);
                var w1 = WorldVertexes.Column(i1);
                v1.PreClipInit(new Vector4(c1[0], c1[1], c1[2], c1[3]),
                                new Vector4(w1[0], w1[1], w1[2], w1[3]));

                // update position
                var c2 = ClipVertexes.Column(i2);
                var w2 = WorldVertexes.Column(i2);
                v2.PreClipInit(new Vector4(c2[0], c2[1], c2[2], c2[3]),
                                new Vector4(w2[0], w2[1], w2[2], w2[3]));

                // update position
                var c3 = ClipVertexes.Column(i3);
                var w3 = WorldVertexes.Column(i3);
                v3.PreClipInit(new Vector4(c3[0], c3[1], c3[2], c3[3]),
                                new Vector4(w3[0], w3[1], w3[2], w3[3]));
                triangles.Add((v1, v2, v3));

            }

            var clippedVertices = Clipper.ClipTriangles(triangles);

            return clippedVertices;

        }


        private static void AddOptionalLightClips(List<(Vertex v1, Vertex v2, Vertex v3)> vertexes, List<PerspectiveLight> lights) {
            for (int i = 0; i < vertexes.Count; i++)
            {
                var (v1, v2, v3) = vertexes[i];

                AddOptionalLightClip(ref v1, lights);
                AddOptionalLightClip(ref v2, lights);
                AddOptionalLightClip(ref v3, lights);

                vertexes[i] = (v1, v2, v3);
            }
        }

        private static void AddOptionalLightClip(ref Vertex v, List<PerspectiveLight> lights)
        {
            v.lightClipSpaces = new Vector4[lights.Count];

            for (int i = 0; i < lights.Count; i++)
            {
                var light = lights[i];
                Matrix<float> wtoc = light.getWToC();
                Vector4 worldPos = v.worldPosition;

                // Manually perform the transformation from world-space to the light's clip-space
                var worldVec = MathNet.Numerics.LinearAlgebra.Vector<float>.Build.DenseOfArray(new float[] { worldPos.X, worldPos.Y, worldPos.Z, worldPos.W });
                var clipVec = wtoc * worldVec;

                // Store the resulting 4D clip-space vector
                v.lightClipSpaces[i] = new Vector4(clipVec[0], clipVec[1], clipVec[2], clipVec[3]);
            }
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



        private static void NdcToScreen(List<(Vertex v1, Vertex v2, Vertex v3)> ndc, int width, int height)
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