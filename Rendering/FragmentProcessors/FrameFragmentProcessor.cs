using System.Numerics;
using System.Runtime.InteropServices;
using SDL;
using Simple3dRenderer.Extensions;
using Simple3dRenderer.Lighting;
using Simple3dRenderer.Objects;
using Simple3dRenderer.Shaders;
using Simple3dRenderer.Textures;

namespace Simple3dRenderer.Rendering
{
    public struct FrameFragmentProcessor<T> : IFragmentProcessor<FrameFragmentProcessor<T>, FrameData> where T : IFragmentShader<T, FrameData>
    {
         public static void ProcessFragment(ref FrameData state, int x, int y, float z,
        float fw0, float fw1, float fw2, Vertex v0, Vertex v1, Vertex v2, bool isMultithreaded)
        {
            int index = y * state.GetWidth() + x; // Use getWidth() for safety
            SDL_Color pixelColor = ShadeBlinnPhong(ref state, v0, v1, v2, fw0, fw1, fw2);

            if (pixelColor.a >= 254) // Opaque
            {
                state.FrameBuffer[index] = pixelColor;
                state.depthBuffer[index] = z;
            }
            else // Transparent
            {
                state.FrameBuffer[index] = AlphaBlend(pixelColor, state.FrameBuffer[index]);
                // Not writing to depth buffer for simple transparency
            }
        }

        private static SDL_Color ShadeBlinnPhong(ref FrameData state,
            Vertex v0, Vertex v1, Vertex v2, float w0, float w1, float w2)
        {
            // --- 1) Perspective-Correct Interpolation (Done ONCE) ---
            float interpolatedInvW = v0.invW * w0 + v1.invW * w1 + v2.invW * w2;

            // --- ROBUSTNESS FIX ---
            // Prevent division by zero, which causes the flicker.
            // If invW is near zero, it means the point is at infinity; it can't be lit.
            if (MathF.Abs(interpolatedInvW) < 1e-6f)
            {
                return T.getPixelColor(ref state, v0, v1, v2, w0, w1, w2); // Return unlit albedo
            }
            float interpolatedW = 1.0f / interpolatedInvW;

            // Interpolate pre-divided attributes
            Vector4 worldPosOverW = v0.worldPositionOverW * w0 + v1.worldPositionOverW * w1 + v2.worldPositionOverW * w2;
            Vector3 normalOverW = v0.normalOverW * w0 + v1.normalOverW * w1 + v2.normalOverW * w2;

            // Recover final attributes by multiplying by W
            Vector3 worldPos = new Vector3(worldPosOverW.X, worldPosOverW.Y, worldPosOverW.Z) * interpolatedW;
            Vector3 N = Vector3.Normalize(normalOverW * interpolatedW);

            // --- 2) Base Albedo & View Vector ---
            SDL_Color baseSdl = T.getPixelColor(ref state, v0, v1, v2, w0, w1, w2);
            Vector3 albedo = ColorLin.FromSDL(baseSdl);
            Vector3 V = Vector3.Normalize(state.CameraPosition - worldPos);

            // --- 3) Lighting Calculation ---
            Vector3 totalLight = state.AmbientColor * albedo;
            int lightCount = state.Lights.Count;
            for (int i = 0; i < lightCount; i++)
            {
                // Pass the already-calculated W to the visibility function. This is a huge optimization.
                float visibility = SampleVisibilityForLight(ref state, i, v0, v1, v2, w0, w1, w2, interpolatedW);
                if (visibility <= 0.001f) continue;

                var light = state.Lights[i];
                Vector3 lightVector = light.Position - worldPos;
                float distSq = lightVector.LengthSquared();

                // Normalize the light vector AFTER getting distance
                Vector3 L = Vector3.Normalize(lightVector);

                // Spotlight check
                float spotFactor = SpotFactor(in light, in L);
                if (spotFactor <= 0.001f) continue;

                // Diffuse
                float NdotL = MathF.Max(0f, Vector3.Dot(N, L));

                // Specular (Blinn-Phong)
                Vector3 H = Vector3.Normalize(L + V);
                float NdotH = MathF.Max(0f, Vector3.Dot(N, H));

                // look up table not much better for speed
                // int lutIndex = (int)(NdotH * FrameData.lutScale);
                // float specFactor = state.specularLut[lutIndex];

                float specFactor = MathF.Pow(NdotH, state.Shininess);


                // Combine terms
                float attenuation = 1.0f / (1.0f + light.Quadratic * distSq);
                Vector3 diffuse = albedo * light.Color * NdotL;
                Vector3 specular = light.Color * state.SpecularStrength * specFactor;

                totalLight += (diffuse + specular) * light.Intensity * attenuation * visibility * spotFactor;
            }

            // --- 4) Convert back to SDL_Color ---
            return ColorLin.ToSDL(totalLight, baseSdl.a);
        }

        private static float SampleVisibilityForLight(ref FrameData state, int lightIndex,
         Vertex v0, Vertex v1, Vertex v2, float fw0, float fw1, float fw2, float interpolatedW)
        {
            // Interpolate that light’s pre-divided clip-space position
            Vector4 clipOverW = v0.lightClipSpacesOverW[lightIndex] * fw0 +
                                v1.lightClipSpacesOverW[lightIndex] * fw1 +
                                v2.lightClipSpacesOverW[lightIndex] * fw2;

            // Recover the perspective-correct light-space clip position using the pre-calculated W
            Vector4 clip = clipOverW * interpolatedW;

            if (MathF.Abs(clip.W) < 1e-6f) return 0f;

            // Check if fragment is within the light's frustum (clip space check)
            if (!(clip.Z >= 0 && clip.Z <= clip.W && MathF.Abs(clip.X) <= clip.W && MathF.Abs(clip.Y) <= clip.W))
                return 0f;

            // Perform perspective divide to get NDC
            Vector3 ndc = new Vector3(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W);

            // Convert NDC to shadow map texture coordinates
            var smap = state.Maps[lightIndex];
            float sx = (ndc.X + 1.0f) * 0.5f * smap._width;
            float sy = (1.0f - ndc.Y) * 0.5f * smap._height;
            float sz = ndc.Z;

            // Boundary check for the shadow map
            if (sx < 0 || sx >= smap._width || sy < 0 || sy >= smap._height)
                return 0f;

            return smap.SampleVisibility((int)sx, (int)sy, sz);
        }
        private static float SpotFactor(in PerspectiveLight Lgt, in Vector3 L_unit_fromFragToLight)
        {
            // L is frag->light. Spotlight “looks” along Lgt.Direction (forward).
            // So compare light.Direction with -L (light->frag).
            float c = Vector3.Dot(Lgt.Direction, -L_unit_fromFragToLight); // cosine of angle to cone axis

            if (c <= Lgt.OuterCutoffCos) return 0f; // outside the cone

            // Smooth falloff between outer and inner
            if (c >= Lgt.InnerCutoffCos) return 1f; // fully inside inner cone

            float t = (c - Lgt.OuterCutoffCos) / (Lgt.InnerCutoffCos - Lgt.OuterCutoffCos);
            // Optional smootherstep for a softer edge:
            // t = t * t * (3f - 2f * t);
            return Math.Clamp(t, 0f, 1f);
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