using MathNet.Numerics.LinearAlgebra;
using Simple3dRenderer.Objects;

namespace Simple3dRenderer.Rendering
{   // Sutherland-Hodgman Algo
        public static class Clipper
        {
            public static List<(Vertex v1, Vertex v2, Vertex v3)> ClipTriangles(
                List<(Vertex v1, Vertex v2, Vertex v3)> triangles)
            {
                var clipped = new List<(Vertex v1, Vertex v2, Vertex v3)>();

                foreach (var triangle in triangles)
                {
                    var poly = new List<Vertex> { triangle.v1, triangle.v2, triangle.v3 };

                    foreach (var plane in GetClipPlanes())
                    {
                        var inputList = poly;
                        poly = [];

                        for (int i = 0; i < inputList.Count; i++)
                        {
                            var current = inputList[i];
                            var previous = inputList[(i - 1 + inputList.Count) % inputList.Count];

                            bool currentInside = IsInside(current, plane);
                            bool previousInside = IsInside(previous, plane);

                            // Console.WriteLine("Is valid?:  " + (currentInside || previousInside));

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

                        if (poly.Count < 3)
                            break; // completely clipped
                    }

                    for (int i = 1; i < poly.Count - 1; i++)
                    {
                        clipped.Add((poly[0], poly[i], poly[i + 1]));
                    }
                }

                return clipped;
            }

            private static bool IsInside(Vertex v, Vector<float> plane)
            {
                return plane[0] * v.clipPosition.X + plane[1] * v.clipPosition.Y + plane[2] * v.clipPosition.Z + plane[3] * v.clipPosition.W >= 0;
            }

            private static Vertex Intersect(Vertex a, Vertex b, Vector<float> plane)
            {
                float da = plane[0] * a.clipPosition.X + plane[1] * a.clipPosition.Y + plane[2] * a.clipPosition.Z + plane[3] * a.clipPosition.W;
                float db = plane[0] * b.clipPosition.X + plane[1] * b.clipPosition.Y + plane[2] * b.clipPosition.Z + plane[3] * b.clipPosition.W;

                float t = da / (da - db); // robust interpolation in 4D

                return Vertex.Lerp(a, b, t);
            }


            private static IEnumerable<Vector<float>> GetClipPlanes()
            {
                // Each Vector4 = [x, y, z, w] coefficients of the plane: Ax + By + Cz + Dw >= 0
                yield return Vector<float>.Build.DenseOfArray([-1, 0, 0, 1]); // Right: x ≤ +w
                yield return Vector<float>.Build.DenseOfArray([1, 0, 0, 1]); // Left:  x ≥ -w
                yield return Vector<float>.Build.DenseOfArray([0, -1, 0, 1]); // Top:   y ≤ +w
                yield return Vector<float>.Build.DenseOfArray([0, 1, 0, 1]); // Bottom:y ≥ -w
                yield return Vector<float>.Build.DenseOfArray([0, 0, -1, 1]); // Far:   z ≤ +w
                yield return Vector<float>.Build.DenseOfArray([0, 0, 1, 1]); // Near:  z ≥ -w
            }

        }
}