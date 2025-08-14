using System;
using System.Collections.Generic;
using SDL;

namespace Simple3dRenderer.Lighting
{
    public struct VisibilityPoint
    {
        public float Depth;
        public float Visibility; // Stores 1-alpha initially, then cumulative visibility

        public override string ToString()
        {
            return $"Depth: {Depth} Visibility: {Visibility}";
        }
    }

    public struct VisibilityFunction
    {
        public List<VisibilityPoint> Points;
        public float? OpaqueDepth; // Nullable opaque depth

        public VisibilityFunction(List<VisibilityPoint> points, float? opaqueDepth = null)
        {
            Points = points;
            OpaqueDepth = opaqueDepth;
        }
    }

    public class DeepShadowMap
    {
        private readonly VisibilityFunction[,] _pixels; // now [height, width]
        public readonly int _width;
        public readonly int _height;
        private readonly float _compressionEpsilon;

        public DeepShadowMap(int width, int height, float compressionEpsilon = 0.125f)
        {
            _width = width;
            _height = height;
            _compressionEpsilon = compressionEpsilon;
            _pixels = new VisibilityFunction[_height, _width]; // row-major

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    _pixels[y, x] = new VisibilityFunction(
                        new List<VisibilityPoint> { new VisibilityPoint { Depth = 0f, Visibility = 1f } },
                        null
                    );
                }
            }
        }

        public void AddVisibilityPoint(int x, int y, float z, float alpha)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height) return;

            var vf = _pixels[y, x]; // row-major indexing

            if (alpha >= 1f) // fully opaque
            {
                if (!vf.OpaqueDepth.HasValue || z < vf.OpaqueDepth.Value)
                    vf.OpaqueDepth = z;
            }
            else
            {
                float transparency = 1f - alpha;
                if (transparency < 1f)
                {
                    if (!vf.OpaqueDepth.HasValue || z < vf.OpaqueDepth.Value)
                        vf.Points.Add(new VisibilityPoint { Depth = z, Visibility = transparency });
                }
            }

            _pixels[y, x] = vf;
        }

        public void Initialize()
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    var vf = _pixels[y, x];

                    if (vf.OpaqueDepth.HasValue)
                    {
                        vf.Points.Add(new VisibilityPoint { Depth = vf.OpaqueDepth.Value, Visibility = 0f });
                    }

                    if (vf.Points.Count > 1)
                    {
                        vf.Points.Sort((a, b) => a.Depth.CompareTo(b.Depth));
                        CalculateCumulativeVisibility(vf.Points);
                        vf.Points = Compress(vf.Points);
                    }

                    _pixels[y, x] = vf;
                }
            }
        }

        public float SampleVisibility(int x, int y, float z, float bias = 0.05f)
        {
            var vf = _pixels[y, x];

            // Console.WriteLine(z + " " + vf.OpaqueDepth.Value);


            float adjustedZ = z - bias;

            if (vf.OpaqueDepth.HasValue && adjustedZ >= vf.OpaqueDepth.Value)
                return 0f;


            return vf.Points.Count <= 25 ? SampleLinear(vf.Points, adjustedZ) : SampleBinary(vf.Points, adjustedZ);
        }

        private static float SampleLinear(List<VisibilityPoint> points, float z)
        {
            for (int i = 1; i < points.Count; i++)
                if (points[i].Depth > z)
                    return points[i - 1].Visibility;

            return points[points.Count - 1].Visibility;
        }

        private static float SampleBinary(List<VisibilityPoint> points, float z)
        {
            int low = 0, high = points.Count - 1, result = 0;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (points[mid].Depth <= z) { result = mid; low = mid + 1; }
                else { high = mid - 1; }
            }

            return points[result].Visibility;
        }

        private static void CalculateCumulativeVisibility(List<VisibilityPoint> points)
        {
            for (int i = 1; i < points.Count; i++)
            {
                var point = points[i];
                point.Visibility = Math.Max(0f, points[i - 1].Visibility * point.Visibility);
                points[i] = point;
            }
        }

        private List<VisibilityPoint> Compress(List<VisibilityPoint> rawFunction)
        {
            if (rawFunction.Count <= 1) return rawFunction;

            var compressed = new List<VisibilityPoint> { rawFunction[0] };
            int startIndex = 0;

            while (startIndex < rawFunction.Count - 1)
            {
                var origin = rawFunction[startIndex];
                float m_lo = float.NegativeInfinity, m_hi = float.PositiveInfinity;
                int endIndex = startIndex + 1;

                for (; endIndex < rawFunction.Count; endIndex++)
                {
                    var curr = rawFunction[endIndex];
                    float deltaZ = curr.Depth - origin.Depth;
                    if (deltaZ <= 0) continue;

                    float slopeUpper = (curr.Visibility + _compressionEpsilon - origin.Visibility) / deltaZ;
                    float slopeLower = (curr.Visibility - _compressionEpsilon - origin.Visibility) / deltaZ;

                    float next_lo = Math.Max(m_lo, slopeLower);
                    float next_hi = Math.Min(m_hi, slopeUpper);

                    if (next_lo > next_hi) break;

                    m_lo = next_lo;
                    m_hi = next_hi;
                }

                int lastValid = endIndex - 1;
                var lastPoint = rawFunction[lastValid];
                float outputSlope = (m_lo + m_hi) / 2f;
                float newVis = Math.Clamp(origin.Visibility + outputSlope * (lastPoint.Depth - origin.Depth), 0f, 1f);

                compressed.Add(new VisibilityPoint { Depth = lastPoint.Depth, Visibility = newVis });
                startIndex = lastValid;
            }

            return compressed;
        }

        public SDL_Color[,] ToColorArray()
        {
            var colors = new SDL_Color[_height, _width];

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    var vf = _pixels[y, x];

                    if (vf.Points.Count == 1 && !vf.OpaqueDepth.HasValue)
                        colors[y, x] = new SDL_Color { r = 0, g = 0, b = 0, a = 255 };
                    else if (vf.OpaqueDepth.HasValue)
                        colors[y, x] = new SDL_Color { r = 255, g = 255, b = 255, a = 255 };
                    else
                        colors[y, x] = new SDL_Color { r = 128, g = 128, b = 128, a = 255 };
                }
            }

            return colors;
        }
    }
}
