using System.Numerics;
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
            // Compute final lit color once

            if (isMultithreaded)
            {
                SDL_Color pixelColor = ShadeBlinnPhong(ref state, v0, v1, v2, fw0, fw1, fw2);
                if (pixelColor.a >= 254)
                {
                    float currentDepth;
                    do
                    {
                        currentDepth = Volatile.Read(ref state.depthBuffer[y, x]);
                        if (z > currentDepth) return;
                    } while (Interlocked.CompareExchange(ref state.depthBuffer[y, x], z, currentDepth) != currentDepth);

                    WriteColorAtomic(state.FrameBuffer, y, x, pixelColor);
                }
                else
                {
                    if (z < Volatile.Read(ref state.depthBuffer[y, x]))
                        WriteColorAtomicBlended(state.FrameBuffer, y, x, pixelColor);
                }
            }
            else if (z <= state.depthBuffer[y, x])
            {
                SDL_Color pixelColor = ShadeBlinnPhong(ref state, v0, v1, v2, fw0, fw1, fw2);
                if (pixelColor.a >= 254)
                {
                    state.FrameBuffer[y, x] = pixelColor;
                    state.depthBuffer[y, x] = z;
                }
                else
                {
                    state.FrameBuffer[y, x] = AlphaBlend(pixelColor, state.FrameBuffer[y, x]);
                }
            }
        }

        private static SDL_Color ShadeBlinnPhong(ref FrameData state,
            Vertex v0, Vertex v1, Vertex v2, float w0, float w1, float w2)
        {
             // --- 1) Base albedo (linear) ---
            SDL_Color baseSdl = T.getPixelColor(ref state, v0, v1, v2, w0, w1, w2);
            Vector3 albedo = ColorLin.FromSDL(baseSdl);

            // --- CORRECT: Perspective-correct interpolation ---
            // First, interpolate invW
            float interpolatedInvW = v0.invW * w0 + v1.invW * w1 + v2.invW * w2;

            // Interpolate the pre-divided attributes
            Vector4 worldPosOverW = v0.worldPositionOverW * w0 + v1.worldPositionOverW * w1 + v2.worldPositionOverW * w2;
            Vector3 normalOverW = v0.normalOverW * w0 + v1.normalOverW * w1 + v2.normalOverW * w2;

            // Recover the perspective-correct values by dividing by invW (or multiplying by W)
            float interpolatedW = 1.0f / interpolatedInvW;
            Vector4 wp4 = worldPosOverW * interpolatedW;
            Vector3 worldPos = new Vector3(wp4.X, wp4.Y, wp4.Z);
            Vector3 N = Vector3.Normalize(normalOverW * interpolatedW);
            // --- End of correction ---

            if (float.IsNaN(N.X)) N = new Vector3(0, 0, -1);

            Vector3 V = Vector3.Normalize(state.CameraPosition - worldPos);

            // ... The rest of your lighting code remains the same ...
            Vector3 accum = state.AmbientColor * albedo;

            int lightCount = state.lights.Count;
            for (int i = 0; i < lightCount; i++)
            {
                var Lgt = state.lights[i];

                // Visibility from your deep shadow map (0..1).
                float vis = SampleVisibilityForLight(ref state, i, v0, v1, v2, w0, w1, w2, interpolatedInvW);
                if (vis <= 0f) continue;

                // Direction, distance, attenuation
                Vector3 L = Lgt.Position - worldPos;
                float dist = L.Length();
                if (dist <= 1e-6f) continue;
                L /= dist;

                float atten = 1f / (1f + Lgt.Quadratic * dist * dist);

                // --- NEW: spotlight (cone) factor ---
                float spot = SpotFactor(in Lgt, in L);
                if (spot <= 0f) continue;

                // Diffuse
                float NdotL = MathF.Max(Vector3.Dot(N, L), 0f);
                Vector3 diffuse = albedo * Lgt.Color * (Lgt.Intensity * NdotL);

                // Blinn–Phong specular (half-vector)
                Vector3 H = Vector3.Normalize(L + V);
                float NdotH = MathF.Max(Vector3.Dot(N, H), 0f);
                float specFactor = MathF.Pow(NdotH, state.Shininess);
                Vector3 specular = Lgt.Color * (Lgt.Intensity * state.SpecularStrength * specFactor);

                // Apply visibility, attenuation, and cone factor
                accum += (diffuse + specular) * atten * vis * spot;
            }

            // --- 5) Convert back to 8-bit ---
            return ColorLin.ToSDL(accum, baseSdl.a);
        }

        // Pull just the visibility value for a given light i using your existing math.
        private static float SampleVisibilityForLight(ref FrameData state, int lightIndex,
         Vertex v0, Vertex v1, Vertex v2, float fw0, float fw1, float fw2, float interpolatedInvW)

        {
            // --- CORRECT: Perspective-correct interpolation ---
            // Interpolate that light’s pre-divided clip-space position
            Vector4 c0_overW = v0.lightClipSpacesOverW[lightIndex];
            Vector4 c1_overW = v1.lightClipSpacesOverW[lightIndex];
            Vector4 c2_overW = v2.lightClipSpacesOverW[lightIndex];
            Vector4 clipOverW = c0_overW * fw0 + c1_overW * fw1 + c2_overW * fw2;

            // Recover the perspective-correct light-space clip position
            float interpolatedW = 1.0f / interpolatedInvW;
            Vector4 clip = clipOverW * interpolatedW;
            // --- End of correction ---

            // The rest of the function is the same
            if (!(clip.Z >= 0 && clip.Z <= clip.W &&
                MathF.Abs(clip.X) <= clip.W &&
                MathF.Abs(clip.Y) <= clip.W))
                return 0f;

            if (clip.W == 0) return 0f;

            // NDC
            Vector3 ndc = new Vector3(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W);

            // Screen coords for shadow map
            var smap = state.maps[lightIndex];
            float sx = (ndc.X + 1) * 0.5f * smap._width;
            float sy = (1 - ndc.Y) * 0.5f * smap._height;
            float sz = ndc.Z;

            if (sx < 0 || sx >= smap._width || sy < 0 || sy >= smap._height)
                return 0f;

            var vis = smap.SampleVisibility((int)sx, (int)sy, sz);
            return vis;
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

        // Thread-safe color writing methods
        private static void WriteColorAtomic(SDL_Color[,] framebuffer, int y, int x, SDL_Color color)
        {
            // Note: Struct assignment isn't truly atomic but is often sufficient.
            // For guaranteed atomicity, one might pack the color into a uint/long and use Interlocked.
            framebuffer[y, x] = color;
        }

        private static void WriteColorAtomicBlended(SDL_Color[,] framebuffer, int y, int x, SDL_Color srcColor)
        {
            // WARNING: This read-modify-write operation is not atomic and can cause race conditions.
            // A lock or a CAS loop on the color value would be required for correctness.
            SDL_Color currentColor = framebuffer[y, x];
            SDL_Color blendedColor = AlphaBlend(srcColor, currentColor);
            framebuffer[y, x] = blendedColor;
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

    public struct MaterialShader<TState> : IFragmentShader<MaterialShader<TState>, TState>
    where TState : IRasterizable, ITextured
    {
        public static SDL_Color getPixelColor(ref TState state, Vertex v0, Vertex v1, Vertex v2, float w0, float w1, float w2)
        {
            // Look up the texture from the state
            if (state.GetTexture() != null)
            {
                return TextureShader.getPixelColor(v0, v1, v2, w0, w1, w2, state.GetTexture());
            }
            else
            {
                return SDLColorExtensions.Interpolate(v0.Color, v1.Color, v2.Color, w0, w1, w2);
            }
        }

    }

}