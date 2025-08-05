using MathNet.Numerics.LinearAlgebra;

namespace Simple3dRenderer
{   // Sutherland-Hodgman Algo
    public static class Clipper
    {
        public static List<(Vector<float> v1, Vector<float> v2, Vector<float> v3)> ClipTriangles(
            List<(Vector<float> v1, Vector<float> v2, Vector<float> v3)> triangles)
        {
            var clipped = new List<(Vector<float> v1, Vector<float> v2, Vector<float> v3)>();

            foreach (var triangle in triangles)
            {
                var poly = new List<Vector<float>> { triangle.v1, triangle.v2, triangle.v3 };

                foreach (var plane in GetClipPlanes())
                {
                    var inputList = poly;
                    poly = [];

                    for (int i = 0; i < inputList.Count; i++)
                    {
                        var current = inputList[i];
                        var previous = inputList[(i - 1 + inputList.Count) % inputList.Count];

                        bool currentInside = plane.IsInside(current);
                        bool previousInside = plane.IsInside(previous);

                        if (currentInside)
                        {
                            if (!previousInside)
                            {
                                poly.Add(Intersect(previous, current, plane));
                            }
                            poly.Add(current);
                        }
                        else if (previousInside)
                        {
                            poly.Add(Intersect(previous, current, plane));
                        }
                    }

                    if (poly.Count < 3) break;
                }

                // Triangulate clipped polygon (fan)
                for (int i = 1; i < poly.Count - 1; i++)
                {
                    clipped.Add((poly[0], poly[i], poly[i + 1]));
                }
            }

            return clipped;
        }

        private static Vector<float> Intersect(Vector<float> a, Vector<float> b, ClipPlane plane)
        {
            // Find t such that (1 - t)a + t*b is on the plane
            float t = plane.ComputeIntersectionT(a, b);
            return a + (b - a) * t;
        }

        private static IEnumerable<ClipPlane> GetClipPlanes()
        {
            yield return new ClipPlane(0, +1); // x ≤  w
            yield return new ClipPlane(0, -1); // x ≥ -w
            yield return new ClipPlane(1, +1); // y ≤  w
            yield return new ClipPlane(1, -1); // y ≥ -w
            yield return new ClipPlane(2, +1); // z ≤  w
            yield return new ClipPlane(2, -1); // z ≥ -w
        }

        private class ClipPlane
        {
            private readonly int axis;
            private readonly int sign; // +1 for ≤ w, -1 for ≥ -w

            public ClipPlane(int axis, int sign)
            {
                this.axis = axis;
                this.sign = sign;
            }

            public bool IsInside(Vector<float> v)
            {
                float coord = v[axis];
                float w = v[3];
                return sign * coord <= w;
            }

            public float ComputeIntersectionT(Vector<float> a, Vector<float> b)
            {
                float aCoord = a[axis], bCoord = b[axis];
                float aW = a[3], bW = b[3];

                float num = sign * aW - aCoord;
                float den = aCoord - bCoord - sign * (aW - bW);

                if (Math.Abs(den) < 1e-6f) return 0.5f; // Avoid div by zero

                return num / den;
            }
        }
    }

}