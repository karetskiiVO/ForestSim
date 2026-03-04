using System;
using System.Collections.Generic;
using System.Linq;

using ProceduralVegetation.Core;

using UnityEngine;

using static ProceduralVegetation.Simulation;

using Event = ProceduralVegetation.Simulation.Event;

namespace ProceduralVegetation {
    public class GrowthEvent : Event {
        public override void Execute(ref SimulationContext ctx) {
            for (int i = 0; i < ctx.points.Count; i++) {
                var point = ctx.points[i];
                var descriptor = ctx.speciesDescriptors[point.speciesID];
                descriptor.Grow(ref point.foliageInstance);
                ctx.points[i] = point;
            }
        }
    }

    public class SeedingEvent : Event {
        public override void Execute(ref SimulationContext ctx) {
            var newFoliage = new List<SimulationPoint>();

            for (int i = 0; i < ctx.points.Count; i++) {
                var point = ctx.points[i];
                var descriptor = ctx.speciesDescriptors[point.speciesID];
                var seeds = descriptor.Seed(ref point.foliageInstance);

                newFoliage.AddRange(seeds?.Select(seed => new SimulationPoint(
                    seed.position,
                    point.speciesID,
                    seed.age,
                    seed.stress,
                    seed.energy,
                    seed.strength
                )) ?? Array.Empty<SimulationPoint>());

                ctx.points[i] = point;
            }

            ctx.points.AddRange(newFoliage);
        }
    }

    public class DeathEvent : Event {
        public override void Execute(ref SimulationContext ctx) {
            // Mark dying first, then remove — avoids index shifting issues.
            for (int i = 0; i < ctx.points.Count; i++) {
                var point = ctx.points[i];
                var descriptor = ctx.speciesDescriptors[point.speciesID];
                if (point.foliageInstance.type != FoliageInstance.FoliageType.Dying &&
                    !descriptor.Alive(in point.foliageInstance)) {
                    point.foliageInstance.type = FoliageInstance.FoliageType.Dying;
                    point.foliageInstance.strength = 0f;
                    ctx.points[i] = point;
                }
            }
            ctx.points.RemoveAll(p => p.foliageInstance.type == FoliageInstance.FoliageType.Dying);
        }
    }

    public class ResourceDistributionEvent : Event {
        /// <summary>Total energy distributed across all cells each tick.</summary>
        public float totalEnergy = 2000f;
        /// <summary>Total water distributed across all cells each tick.
        /// Should be roughly >= 0.5 * expected population to avoid chronic stress.</summary>
        public float totalWater = 70000f;

        public override void Execute(ref SimulationContext ctx) {
            int n = ctx.points.Count;
            if (n == 0) return;

            // --- Degenerate cases: skip power diagram ---
            if (n == 1) {
                var pt = ctx.points[0];
                ctx.speciesDescriptors[pt.speciesID].AddResources(ref pt.foliageInstance, totalEnergy, totalWater);
                ctx.points[0] = pt;
                return;
            }

            // --- Clip rect from landscape bounds or from point AABB ---
            Rect clipRect = ComputeClipRect(ctx);

            // --- Build power diagram only from active (non-Seed, non-Dying) trees ---
            // This prevents dead/dormant points from stealing resource shares.
            var activeIndices = new List<int>(n);
            for (int i = 0; i < n; i++) {
                var t = ctx.points[i].foliageInstance.type;
                if (t == FoliageInstance.FoliageType.Sapling ||
                    t == FoliageInstance.FoliageType.Mature)
                    activeIndices.Add(i);
            }

            if (activeIndices.Count == 0) return;
            if (activeIndices.Count == 1) {
                var pt = ctx.points[activeIndices[0]];
                ctx.speciesDescriptors[pt.speciesID].AddResources(ref pt.foliageInstance, totalEnergy, totalWater);
                ctx.points[activeIndices[0]] = pt;
                return;
            }

            var inputPoints = new (Vector2 pos, float weight)[activeIndices.Count];
            for (int j = 0; j < activeIndices.Count; j++) {
                var fi = ctx.points[activeIndices[j]].foliageInstance;
                inputPoints[j] = (fi.position, Mathf.Max(0f, fi.strength));
            }

            PowerDiagram diagram;
            try {
                diagram = PowerDiagram.FromPoints(inputPoints);
            } catch {
                return; // degenerate configuration (e.g. all points collinear)
            }

            // --- Compute clipped area of each active cell ---
            float[] areas = new float[activeIndices.Count];
            float totalArea = 0f;
            for (int j = 0; j < activeIndices.Count; j++) {
                if (!diagram.Cells.TryGetValue(j, out var edges) || edges.Count == 0)
                    continue;
                areas[j] = ComputeClippedCellArea(edges, clipRect);
                totalArea += areas[j];
            }

            if (totalArea <= 0f) return;

            // --- Distribute resources proportionally to cell area ---
            for (int j = 0; j < activeIndices.Count; j++) {
                if (areas[j] <= 0f) continue;
                float fraction = areas[j] / totalArea;
                var point = ctx.points[activeIndices[j]];
                ctx.speciesDescriptors[point.speciesID]
                    .AddResources(ref point.foliageInstance,
                                  totalEnergy * fraction,
                                  totalWater * fraction);
                ctx.points[activeIndices[j]] = point;
            }
        }

        // ------------------------------------------------------------------ //

        private static Rect ComputeClipRect(SimulationContext ctx) {
            var bb = ctx.landscape.bbox;
            return new Rect(bb.min.x, bb.min.z, bb.size.x, bb.size.z);
        }

        /// <summary>
        /// Computes the area of a single power-diagram cell clipped to <paramref name="clip"/>.
        /// The ordered edge list is converted to a polygon (infinite rays clamped to a large
        /// distance), then Sutherland-Hodgman clips it to the rectangle.
        /// </summary>
        private static float ComputeClippedCellArea(
            List<PowerDiagram.VoronoiCellEdge> edges, Rect clip) {

            // Use the diagonal of the clip rect as the "infinity" clip distance;
            // this guarantees all clipped ray endpoints lie well outside the rect.
            float clipDist = Mathf.Sqrt(clip.width * clip.width + clip.height * clip.height) + 1f;

            // Each edge's polygon vertex is its start point (at t = TMin).
            // Infinite TMin is clamped to -clipDist so the far point is outside the rect.
            var polygon = new List<Vector2>(edges.Count);
            foreach (var edge in edges) {
                float t = float.IsNegativeInfinity(edge.TMin) ? -clipDist : edge.TMin;
                polygon.Add(edge.Origin + edge.Direction * t);
            }

            if (polygon.Count < 3) return 0f;

            polygon = ClipPolygonToRect(polygon, clip);
            if (polygon.Count < 3) return 0f;

            return ShoelaceArea(polygon);
        }

        // Sutherland-Hodgman: clips a convex polygon to an axis-aligned rectangle.
        private static List<Vector2> ClipPolygonToRect(List<Vector2> poly, Rect r) {
            poly = ClipByHalfPlane(poly, new Vector2(1f, 0f), r.xMin);  // x >= xMin
            poly = ClipByHalfPlane(poly, new Vector2(-1f, 0f), -r.xMax);  // x <= xMax
            poly = ClipByHalfPlane(poly, new Vector2(0f, 1f), r.yMin);  // y >= yMin
            poly = ClipByHalfPlane(poly, new Vector2(0f, -1f), -r.yMax);  // y <= yMax
            return poly;
        }

        // Keeps the half-plane { p : dot(normal, p) >= d }.
        private static List<Vector2> ClipByHalfPlane(
            List<Vector2> poly, Vector2 normal, float d) {

            if (poly.Count == 0) return poly;

            var result = new List<Vector2>(poly.Count);
            int n = poly.Count;
            for (int i = 0; i < n; i++) {
                Vector2 cur = poly[i];
                Vector2 nxt = poly[(i + 1) % n];
                float dCur = Vector2.Dot(normal, cur) - d;
                float dNxt = Vector2.Dot(normal, nxt) - d;

                if (dCur >= 0f) result.Add(cur);
                if ((dCur >= 0f) != (dNxt >= 0f)) {
                    float t = dCur / (dCur - dNxt);
                    result.Add(Vector2.LerpUnclamped(cur, nxt, t));
                }
            }
            return result;
        }

        // Shoelace formula for (possibly non-convex) polygon area.
        private static float ShoelaceArea(List<Vector2> poly) {
            float area = 0f;
            int n = poly.Count;
            for (int i = 0; i < n; i++) {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % n];
                area += a.x * b.y - b.x * a.y;
            }
            return Mathf.Abs(area) * 0.5f;
        }
    }
}
