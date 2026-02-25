using System.Collections.Generic;
using System.Linq;

using ProceduralVegetation.Core;

using Sirenix.OdinInspector;

using UnityEngine;

public class Benchmark : MonoBehaviour {
    PowerDiagram _diagram;

    // Site positions parallel to _diagram.Cells keys (stored for Gizmos)
    Vector2[] _sites;

    [SerializeField, Range(1, 10000000)] int count = 100000;
    [SerializeField] float infiniteEdgeClip = 200f;
    [SerializeField] float siteRadius = 0.4f;

    [Button]
    void RunBenchmark() {
        float boxSize = 10000f;

        var points = Enumerable.Range(0, count)
            .Select(_ => (boxSize * new Vector2(Random.value, Random.value), (float)(Random.value * 100)))
            .ToArray();

        var stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        PowerDiagram.FromPoints(points);

        stopwatch.Stop();
        Debug.Log($"Added {count} points in {stopwatch.ElapsedMilliseconds / 1000.0f} seconds");
    }

    [Button]
    void RunTriangulationTest() {
        float boxSize = 100f;

        var points = Enumerable.Range(0, count)
            .Select(_ => (boxSize * new Vector2(Random.value, Random.value), (float)(Random.value * 100)))
            .ToArray();

        _diagram = PowerDiagram.FromPoints(points);

        // Cache site positions so OnDrawGizmos can access them
        _sites = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
            _sites[i] = points[i].Item1;

        Debug.Log($"Triangulation built: {points.Length} points, {_diagram.Cells.Count} cells");
    }

    void OnDrawGizmos() {
        if (_diagram == null) return;

        var voronoiVerts = _diagram.VoronoiVertices;
        var cells = _diagram.Cells;

        // Draw Voronoi edges — each undirected edge is stored in two cells
        // (as a directed pair), so track drawn pairs to avoid duplicates.
        var drawn = new HashSet<(int, int)>();

        Gizmos.color = Color.cyan;
        foreach (var kv in cells) {
            foreach (var edge in kv.Value) {
                // Canonical key: (min, max) of the two triangle indices
                int a = edge.FromTri, b = edge.ToTri;
                int lo = a < b ? a : b;
                int hi = a < b ? b : a;
                var key = (lo, hi);
                if (!drawn.Add(key)) continue;   // already drawn from other cell

                // Resolve endpoints, clipping away infinities
                Vector2 start = ResolvePoint(edge, edge.TMin, infiniteEdgeClip);
                Vector2 end = ResolvePoint(edge, edge.TMax, infiniteEdgeClip);

                Gizmos.DrawLine(To3D(start), To3D(end));
            }
        }

        // Draw site spheres
        if (_sites == null) return;
        Gizmos.color = Color.yellow;
        foreach (var kv in cells) {
            int siteIdx = kv.Key;
            if (siteIdx < 0 || siteIdx >= _sites.Length) continue;
            Gizmos.DrawSphere(To3D(_sites[siteIdx]), siteRadius);
        }
    }

    private static Vector2 ResolvePoint(PowerDiagram.VoronoiCellEdge edge, float t, float clip) {
        if (float.IsNegativeInfinity(t)) return edge.Origin - edge.Direction * clip;
        if (float.IsPositiveInfinity(t)) return edge.Origin + edge.Direction * clip;
        return edge.Origin + edge.Direction * t;
    }

    private Vector3 To3D(Vector2 p) =>
        transform.TransformPoint(new Vector3(p.x, 0f, p.y));
}
