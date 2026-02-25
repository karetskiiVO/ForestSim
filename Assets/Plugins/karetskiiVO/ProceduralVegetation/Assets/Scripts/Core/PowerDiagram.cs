// Based on the algorithm by Alexandre Devert (MIT License):
// https://gist.github.com/marmakoide/45d5389252683ae09c2df49d0548a627
//
// Computes a Laguerre–Voronoi diagram (power diagram) in 2D via a 3D convex
// hull of the lifted weighted points.  Convex hull provided by MIConvexHull.

using System;
using System.Collections.Generic;

using MIConvexHull;

using UnityEngine;

namespace ProceduralVegetation.Core {
    class PowerDiagram {
        /// <summary>
        /// One oriented edge of a Voronoi cell.
        /// The edge is the set  { Origin + t * Direction  :  TMin ≤ t ≤ TMax }.
        /// TMin = float.NegativeInfinity means the half-line extends to -∞.
        /// TMax = float.PositiveInfinity means the half-line extends to +∞.
        /// FromTri / ToTri are indices into <see cref="VoronoiVertices"/>;
        /// -1 means the end is at infinity.
        /// </summary>
        public readonly struct VoronoiCellEdge {
            public readonly int FromTri;
            public readonly int ToTri;
            public readonly Vector2 Origin;
            public readonly Vector2 Direction;
            public readonly float TMin;
            public readonly float TMax;

            public VoronoiCellEdge(
                int fromTri, int toTri,
                Vector2 origin, Vector2 direction,
                float tMin, float tMax) {
                FromTri = fromTri;
                ToTri = toTri;
                Origin = origin;
                Direction = direction;
                TMin = tMin;
                TMax = tMax;
            }
        }

        // ------------------------------------------------------------------ //
        //  Result data                                                         //
        // ------------------------------------------------------------------ //

        /// <summary>Power-Delaunay triangulation.  Each element is [a, b, c] in CCW order.</summary>
        public int[][] Triangles { get; private set; }

        /// <summary>Voronoi vertex (circumcenter) for each triangle in <see cref="Triangles"/>.</summary>
        public Vector2[] VoronoiVertices { get; private set; }

        /// <summary>
        /// For each site index that participates in the triangulation: an
        /// ordered list of <see cref="VoronoiCellEdge"/> forming the cell boundary.
        /// </summary>
        public IReadOnlyDictionary<int, List<VoronoiCellEdge>> Cells { get; private set; }

        // ------------------------------------------------------------------ //
        //  Factory                                                             //
        // ------------------------------------------------------------------ //

        public static PowerDiagram FromPoints((Vector2 pos, float weight)[] points) {
            int n = points.Length;
            var S = new Vector2[n];
            var R = new float[n];
            for (int i = 0; i < n; i++) {
                S[i] = points[i].pos;
                R[i] = points[i].weight;
            }

            var (triList, V) = GetPowerTriangulation(S, R);
            var cells = GetVoronoiCells(S, V, triList);

            return new PowerDiagram {
                Triangles = triList,
                VoronoiVertices = V,
                Cells = cells,
            };
        }

        // ------------------------------------------------------------------ //
        //  Internal vertex type for MIConvexHull                              //
        // ------------------------------------------------------------------ //

        private sealed class LiftedVertex : IVertex {
            public double[] Position { get; }   // (x, y,  x²+y²−r²)
            public int Index { get; }

            public LiftedVertex(int index, double x, double y, double r) {
                Index = index;
                double z = x * x + y * y - r * r;
                Position = new double[] { x, y, z };
            }
        }

        // ------------------------------------------------------------------ //
        //  Geometry helpers                                                    //
        // ------------------------------------------------------------------ //

        // Power circumcenter of three 3-D lifted points A, B, C.
        // Formula: N = normalise((B−A)×(C−A)),  circumcenter = (−0.5/Nz)·(Nx, Ny)
        private static Vector2 GetPowerCircumcenter(double[] A, double[] B, double[] C) {
            double abx = B[0] - A[0], aby = B[1] - A[1], abz = B[2] - A[2];
            double acx = C[0] - A[0], acy = C[1] - A[1], acz = C[2] - A[2];

            double nx = aby * acz - abz * acy;
            double ny = abz * acx - abx * acz;
            double nz = abx * acy - aby * acx;

            double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            nx /= len; ny /= len; nz /= len;

            double s = -0.5 / nz;
            return new Vector2((float)(s * nx), (float)(s * ny));
        }

        // True iff the 2-D triangle A→B→C is counter-clockwise.
        private static bool IsCCW(Vector2 a, Vector2 b, Vector2 c) {
            return (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y) > 0f;
        }

        private static float Norm2(Vector2 v) => v.magnitude;

        private static Vector2 Normalized2(Vector2 v) => v.normalized;

        // ------------------------------------------------------------------ //
        //  Step 1 — power triangulation via lower convex hull                 //
        // ------------------------------------------------------------------ //

        private static (int[][] triList, Vector2[] V) GetPowerTriangulation(
            Vector2[] S, float[] R) {
            int n = S.Length;

            // Build lifted points
            var lifted = new LiftedVertex[n];
            for (int i = 0; i < n; i++)
                lifted[i] = new LiftedVertex(i, S[i].x, S[i].y, R[i]);

            // Special-case: exactly 3 points → single triangle, no hull needed
            if (n == 3) {
                bool ccw = IsCCW(S[0], S[1], S[2]);
                int[] tri = ccw ? new[] { 0, 1, 2 } : new[] { 0, 2, 1 };
                var vc = GetPowerCircumcenter(
                    lifted[tri[0]].Position,
                    lifted[tri[1]].Position,
                    lifted[tri[2]].Position);
                return (new[] { tri }, new[] { vc });
            }

            // 3-D convex hull of the lifted points
            var hull = ConvexHull.Create<LiftedVertex, DefaultConvexFace<LiftedVertex>>(lifted);

            var triList = new List<int[]>();
            var verts = new List<Vector2>();

            foreach (var face in hull.Faces) {
                // Keep only lower-hull faces (outward normal has Nz ≤ 0)
                if (face.Normal[2] > 0.0) continue;

                int a = face.Vertices[0].Index;
                int b = face.Vertices[1].Index;
                int c = face.Vertices[2].Index;

                // Ensure CCW orientation in 2-D
                if (!IsCCW(S[a], S[b], S[c]))
                    (b, c) = (c, b);

                triList.Add(new[] { a, b, c });
                verts.Add(GetPowerCircumcenter(
                    lifted[a].Position,
                    lifted[b].Position,
                    lifted[c].Position));
            }

            return (triList.ToArray(), verts.ToArray());
        }

        // ------------------------------------------------------------------ //
        //  Step 2 — Voronoi cells from the triangulation                      //
        // ------------------------------------------------------------------ //

        private static IReadOnlyDictionary<int, List<VoronoiCellEdge>> GetVoronoiCells(
            Vector2[] S, Vector2[] V, int[][] triList) {
            // --- Build edge → triangle(s) map --------------------------------
            // Key  : (min, max) vertex pair
            // Value: list of triangle indices sharing that edge (1 or 2)
            var edgeMap = new Dictionary<(int, int), List<int>>();

            for (int i = 0; i < triList.Length; i++) {
                int[] tri = triList[i];
                for (int p = 0; p < 3; p++) {
                    int lo = tri[p], hi = tri[(p + 1) % 3];
                    if (lo > hi) (lo, hi) = (hi, lo);
                    var key = (lo, hi);
                    if (!edgeMap.TryGetValue(key, out var list))
                        edgeMap[key] = list = new List<int>(2);
                    list.Add(i);
                }
            }

            // --- Collect edges per site ---------------------------------------
            var cellMap = new Dictionary<int, List<VoronoiCellEdge>>();

            // Pre-populate with every site that appears in the triangulation
            foreach (var tri in triList)
                foreach (var idx in tri)
                    if (!cellMap.ContainsKey(idx))
                        cellMap[idx] = new List<VoronoiCellEdge>();

            for (int i = 0; i < triList.Length; i++) {
                int ta = triList[i][0];
                int tb = triList[i][1];
                int tc = triList[i][2];

                // Directed edge permutations: (u, v, w) = edge u-v, opposite vertex w
                // Equivalent to Python: for u, v, w in ((a,b,c),(b,c,a),(c,a,b))
                for (int p = 0; p < 3; p++) {
                    int u = (p == 0) ? ta : (p == 1) ? tb : tc;
                    int v = (p == 0) ? tb : (p == 1) ? tc : ta;
                    int w = (p == 0) ? tc : (p == 1) ? ta : tb;

                    int lo2 = u < v ? u : v;
                    int hi2 = u < v ? v : u;
                    var key2 = (lo2, hi2);
                    var sharers = edgeMap[key2];

                    if (sharers.Count == 2) {
                        // --- Finite Voronoi edge ---
                        // Add to cell[u]: directed from V[i] toward adjacent circumcenter
                        int j = sharers[0], k = sharers[1];
                        if (k == i) (j, k) = (k, j);   // ensure j == i

                        Vector2 edgeVec = V[k] - V[j];
                        float len = Norm2(edgeVec);
                        Vector2 dir = len > 1e-12f ? edgeVec / len : Vector2.right;

                        cellMap[u].Add(new VoronoiCellEdge(j, k, V[j], dir, 0f, len));
                    } else {
                        // --- Infinite (boundary) Voronoi edge ---
                        // Each boundary edge belongs to exactly one triangle and is
                        // visited exactly once in this loop, so no deduplication needed.
                        // Both u and v receive a half-line from the Voronoi vertex D.

                        Vector2 posA = S[u];
                        Vector2 posB = S[v];
                        Vector2 posC = S[w];
                        Vector2 D = V[i];   // Voronoi vertex of this triangle

                        Vector2 edgeDir = Normalized2(posB - posA);
                        Vector2 I = posA + Vector2.Dot(D - posA, edgeDir) * edgeDir;
                        Vector2 W = Normalized2(I - D);

                        // W must point away from the interior (away from C)
                        if (Vector2.Dot(W, I - posC) < 0f)
                            W = -W;

                        int triIdx = sharers[0];

                        // Half-line for u: from D in direction +W, t ∈ [0, +∞)
                        cellMap[u].Add(new VoronoiCellEdge(triIdx, -1, D, W, 0f, float.PositiveInfinity));
                        // Half-line for v: from D in direction -W, t ∈ (-∞, 0]
                        cellMap[v].Add(new VoronoiCellEdge(-1, triIdx, D, -W, float.NegativeInfinity, 0f));
                    }
                }
            }

            // --- Order each cell's edge list into a CCW chain ----------------
            foreach (var kv in cellMap)
                OrderSegmentList(kv.Value);

            return cellMap;
        }

        // Sort segment_list so that segment[i].ToTri == segment[i+1].FromTri
        private static void OrderSegmentList(List<VoronoiCellEdge> segs) {
            if (segs.Count <= 1) return;

            // Choose the segment whose FromTri is smallest as the first
            int firstIdx = 0;
            int minFrom = segs[0].FromTri;
            for (int i = 1; i < segs.Count; i++) {
                if (segs[i].FromTri < minFrom) {
                    minFrom = segs[i].FromTri;
                    firstIdx = i;
                }
            }
            // Swap first element into position 0
            (segs[0], segs[firstIdx]) = (segs[firstIdx], segs[0]);

            // Insertion-sort style chain ordering
            for (int i = 0; i < segs.Count - 1; i++) {
                int nextFrom = segs[i].ToTri;
                for (int j = i + 1; j < segs.Count; j++) {
                    if (segs[j].FromTri == nextFrom) {
                        (segs[i + 1], segs[j]) = (segs[j], segs[i + 1]);
                        break;
                    }
                }
            }
        }
    }
}
