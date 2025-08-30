using System.Numerics;
using SDL;
using Simple3dRenderer.Objects;
using Simple3dRenderer.Textures;
using Simple3dRenderer.Lighting;
using Simple3dRenderer.Extensions;

namespace Simple3dRenderer.Rendering
{
    public class Pipeline
    {
        private struct TriangleBatch
        {
            public Texture? Texture;
            public List<(Vertex v1, Vertex v2, Vertex v3)> Triangles;
        }

        // --- PERMANENT, REUSABLE OBJECTS ---
        private readonly StateWrapper<FrameData> _mainFrameDataWrapper;
        private readonly TiledRasterizer<FrameFragmentProcessor<MaterialShader<FrameData>>, FrameData> _mainRasterizer;
        private readonly TiledRasterizer<DepthPrePassProcessor, FrameData> _depthRasterizer;

        // Pool of resources for shadow mapping
        private readonly List<StateWrapper<LightData>> _lightDataWrappers;
        private readonly List<TiledRasterizer<LightFragmentProcessor<MaterialShader<LightData>>, LightData>> _shadowRasterizers;
        private readonly List<DeepShadowMap> _reusableShadowMapsList;

        private readonly List<PerspectiveLight> lights;

        public Pipeline(int screenWidth, int screenHeight, List<PerspectiveLight> lights)
        {
            // 1. Create the main render target and its associated rasterizers
            var frameData = FrameData.Create(screenWidth, screenHeight);
            _mainFrameDataWrapper = new StateWrapper<FrameData> { State = frameData };
            _mainRasterizer = new TiledRasterizer<FrameFragmentProcessor<MaterialShader<FrameData>>, FrameData>(screenWidth, screenHeight, _mainFrameDataWrapper);
            _depthRasterizer = new TiledRasterizer<DepthPrePassProcessor, FrameData>(screenWidth, screenHeight, _mainFrameDataWrapper);

            // 2. Pre-allocate a pool of resources for shadow mapping based on the provided lights.
            int numLights = lights.Count;
            _lightDataWrappers = new List<StateWrapper<LightData>>(numLights);
            _shadowRasterizers = new List<TiledRasterizer<LightFragmentProcessor<MaterialShader<LightData>>, LightData>>(numLights);
            _reusableShadowMapsList = new List<DeepShadowMap>(numLights);

            this.lights = lights;

            foreach (var light in lights)
            {
                var lightData = LightData.Create(light.Width, light.Height);
                var lightDataWrapper = new StateWrapper<LightData> { State = lightData };

                _lightDataWrappers.Add(lightDataWrapper);
                _shadowRasterizers.Add(new TiledRasterizer<LightFragmentProcessor<MaterialShader<LightData>>, LightData>(light.Width, light.Height, lightDataWrapper));
            }
        }

        public SDL_Color[] RenderScene(Scene scene)
        {
            // --- 1. SHADOW MAPPING PASS ---
            _reusableShadowMapsList.Clear();
            // Important: Use the pipeline's configured lights, not scene.lights,
            // as the resources were allocated based on them.
            for (int i = 0; i < _lightDataWrappers.Count; i++)
            {
                // This assumes the i-th light in the scene corresponds to the i-th allocated resource.
                // A more robust system might use a dictionary lookup if lights can be added/removed dynamically.
                _reusableShadowMapsList.Add(CreateShadowMapForLight(scene.objects, lights[i], i));
            }

            // --- 2. PREPARE MAIN RENDER STATE ---
            // Simplified struct modification: Because State is a public field, we can modify it directly.
            _mainFrameDataWrapper.State.Reset();
            _mainFrameDataWrapper.State.InitFrame(scene, _reusableShadowMapsList, lights);

            // --- 3. BATCH & PROCESS GEOMETRY ---
            var opaques = scene.objects.Where(m => m.IsOpaque()).ToList();
            var transparents = scene.objects.Where(m => !m.IsOpaque()).ToList();

            var opaqueBatches = ProcessAndBatchSceneObjects(opaques, scene.camera, lights);
            var transparentBatches = ProcessAndBatchSceneObjects(transparents, scene.camera, lights);

            // --- 4. RENDER PASSES ---
            // A) Optional Depth Pre-Pass
            if (opaques.Count > 25 || lights.Count > 0)
            {
                var allOpaqueTriangles = opaqueBatches.SelectMany(b => b.Triangles);
                _depthRasterizer.Render(allOpaqueTriangles);
            }

            // B) Opaque Color Pass
            var sortedOpaqueBatches = opaqueBatches.OrderBy(b => b.Triangles.Any() ? b.Triangles.Average(t => (t.v1.clipPosition.Z + t.v2.clipPosition.Z + t.v3.clipPosition.Z) / 3f) : 1.0f);
            foreach (var batch in sortedOpaqueBatches)
            {
                // Simplified struct modification
                _mainFrameDataWrapper.State.SetTexture(batch.Texture);
                _mainRasterizer.Render(batch.Triangles);

            }

            // C) Transparent Color Pass
            var sortedTransparentBatches = transparentBatches.OrderByDescending(b => b.Triangles.Any() ? b.Triangles.Average(t => (t.v1.clipPosition.Z + t.v2.clipPosition.Z + t.v3.clipPosition.Z) / 3f) : -1.0f);
            foreach (var batch in sortedTransparentBatches)
            {
                // Simplified struct modification
                _mainFrameDataWrapper.State.SetTexture(batch.Texture);
                Rasterizer.RasterizeTrianglesBatchOptimized<FrameFragmentProcessor<MaterialShader<FrameData>>, FrameData>(ref _mainFrameDataWrapper.State, batch.Triangles, null);
            }

            // return PostProcessing.ApplyFXAA(_mainFrameDataWrapper.State.FrameBuffer, _mainFrameDataWrapper.State.GetWidth(), _mainFrameDataWrapper.State.GetHeight()); 
            return _mainFrameDataWrapper.State.FrameBuffer;
        }

        private DeepShadowMap CreateShadowMapForLight(List<Mesh> objects, PerspectiveLight light, int lightIndex)
        {
            var lightDataWrapper = _lightDataWrappers[lightIndex];
            var shadowRasterizer = _shadowRasterizers[lightIndex];

            // Here you would update the perspective matrix if the light moves/rotates.
            // lightDataWrapper.State.wtoc = light.getWToC(); // Needs a public setter on LightData

            var allTrianglesForLight = ProcessAndBatchSceneObjects(objects, light, null).SelectMany(b => b.Triangles);

            // The rasterizer's Render call will clear the shadow map data for this frame.
            lightDataWrapper.State.Reset();
            shadowRasterizer.Render(allTrianglesForLight);


            // Rasterizer.RasterizeTrianglesBatchOptimized<LightFragmentProcessor<MaterialShader<LightData>>, LightData>(ref lightDataWrapper.State, allTrianglesForLight, null);

            // Finalize this light's shadow map for sampling.
            lightDataWrapper.State.shadowMap.Initialize();

            return lightDataWrapper.State.shadowMap;
        }

        // --- Static Geometry Processing Helpers ---
        private static readonly object s_nullTextureKey = new object();

        private static List<TriangleBatch> ProcessAndBatchSceneObjects(List<Mesh> objects, Viewport perspective, List<PerspectiveLight>? lights)
        {
            // 1. Change the dictionary's key type from Texture? to object.
            var batches = new Dictionary<object, List<(Vertex, Vertex, Vertex)>>();
            var wtoc = perspective.GetWorldToClipMatrix();

            foreach (var mesh in objects)
            {
                // 2. Use the null-coalescing operator (??) to substitute our sentinel key when mesh.texture is null.
                object key = mesh.texture ?? s_nullTextureKey;

                // This is a more efficient way to get or create the list in a dictionary.
                if (!batches.TryGetValue(key, out var triangleList))
                {
                    triangleList = new List<(Vertex, Vertex, Vertex)>();
                    batches[key] = triangleList;
                }

                var meshTriangles = new List<(Vertex v1, Vertex v2, Vertex v3)>();

                // --- The rest of your geometry processing logic is PERFECT and remains unchanged ---
                var vertexes = mesh.GetVertexMatrix();
                var objecttoworld = mesh.GetModelMatrix().ToMathNet();
                var worldVertexes = objecttoworld * vertexes;
                var clipVertexes = wtoc * worldVertexes;

                foreach (var (i1, i2, i3) in mesh.indices)
                {
                    var c1 = clipVertexes.Column(i1); var w1 = worldVertexes.Column(i1);
                    var c2 = clipVertexes.Column(i2); var w2 = worldVertexes.Column(i2);
                    var c3 = clipVertexes.Column(i3); var w3 = worldVertexes.Column(i3);

                    var v1 = mesh.Vertexes[i1]; v1.PreClipInit(new Vector4(c1[0], c1[1], c1[2], c1[3]), new Vector4(w1[0], w1[1], w1[2], w1[3]));
                    var v2 = mesh.Vertexes[i2]; v2.PreClipInit(new Vector4(c2[0], c2[1], c2[2], c2[3]), new Vector4(w2[0], w2[1], w2[2], w2[3]));
                    var v3 = mesh.Vertexes[i3]; v3.PreClipInit(new Vector4(c3[0], c3[1], c3[2], c3[3]), new Vector4(w3[0], w3[1], w3[2], w3[3]));
                    meshTriangles.Add((v1, v2, v3));
                }
                var clippedTriangles = Clipper.ClipTriangles(meshTriangles);

                if (lights != null) AddOptionalLightClips(clippedTriangles, lights);
                ApplyPerspectiveDivideAndScreenTransform(clippedTriangles, perspective.Width, perspective.Height);

                // Add the processed triangles to the correct batch list.
                triangleList.AddRange(clippedTriangles);
            }

            // 3. Convert the dictionary back to the final list of TriangleBatch.
            //    Here, we check for our sentinel key and convert it back to a null texture.
            var triangles = batches.Select(kvp => new TriangleBatch
            {
                Texture = (kvp.Key == s_nullTextureKey) ? null : (Texture)kvp.Key,
                Triangles = kvp.Value
            }).ToList();

            return triangles;
        }

        private static void AddOptionalLightClips(List<(Vertex v1, Vertex v2, Vertex v3)> triangles, List<PerspectiveLight> lights)
        {
            for (int i = 0; i < triangles.Count; i++)
            {
                var (v1, v2, v3) = triangles[i];
                AddOptionalLightClip(ref v1, lights);
                AddOptionalLightClip(ref v2, lights);
                AddOptionalLightClip(ref v3, lights);
                triangles[i] = (v1, v2, v3);
            }
        }

        private static void AddOptionalLightClip(ref Vertex v, List<PerspectiveLight> lights)
        {
            var clipSpaces = new Vector4[lights.Count];
            for (int i = 0; i < lights.Count; i++)
            {
                var worldVec = MathNet.Numerics.LinearAlgebra.Vector<float>.Build.DenseOfArray(new float[] { v.worldPosition.X, v.worldPosition.Y, v.worldPosition.Z, v.worldPosition.W });
                var clipVec = lights[i].GetWorldToClipMatrix() * worldVec;
                clipSpaces[i] = new Vector4(clipVec[0], clipVec[1], clipVec[2], clipVec[3]);
            }
            v.setLightClipSpaces(clipSpaces);
        }

        private static void ApplyPerspectiveDivideAndScreenTransform(List<(Vertex v1, Vertex v2, Vertex v3)> triangles, int width, int height)
        {
            for (int i = 0; i < triangles.Count; i++)
            {
                var (v1, v2, v3) = triangles[i];

                float w1 = v1.clipPosition.W; v1.clipPosition = new Vector4(v1.clipPosition.X / w1, v1.clipPosition.Y / w1, v1.clipPosition.Z / w1, 1);
                float w2 = v2.clipPosition.W; v2.clipPosition = new Vector4(v2.clipPosition.X / w2, v2.clipPosition.Y / w2, v2.clipPosition.Z / w2, 1);
                float w3 = v3.clipPosition.W; v3.clipPosition = new Vector4(v3.clipPosition.X / w3, v3.clipPosition.Y / w3, v3.clipPosition.Z / w3, 1);

                v1.clipPosition = new Vector4((v1.clipPosition.X + 1) * 0.5f * width, (1 - v1.clipPosition.Y) * 0.5f * height, v1.clipPosition.Z, 1);
                v2.clipPosition = new Vector4((v2.clipPosition.X + 1) * 0.5f * width, (1 - v2.clipPosition.Y) * 0.5f * height, v2.clipPosition.Z, 1);
                v3.clipPosition = new Vector4((v3.clipPosition.X + 1) * 0.5f * width, (1 - v3.clipPosition.Y) * 0.5f * height, v3.clipPosition.Z, 1);

                triangles[i] = (v1, v2, v3);
            }
        }
    }
}