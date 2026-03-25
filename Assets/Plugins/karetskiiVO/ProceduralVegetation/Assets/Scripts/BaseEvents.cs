using System;
using System.Collections.Generic;
using System.Linq;

using ProceduralVegetation.Core;
using ProceduralVegetation.Utilities;

using MIConvexHull;

using UnityEngine;
using UnityEngine.Rendering;

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

            for (int i = 0; i < ctx.speciesDescriptors.Count; i++) {
                ctx.speciesDescriptors[i].ResetPopulationCounters();
            }

            for (int i = 0; i < ctx.points.Count; i++) {
                var point = ctx.points[i];
                if (point.foliageInstance.type == FoliageInstance.FoliageType.Dying) {
                    continue;
                }

                var descriptor = ctx.speciesDescriptors[point.speciesID];
                descriptor.RegisterInstance(in point.foliageInstance);
            }

            for (int i = 0; i < ctx.points.Count; i++) {
                var point = ctx.points[i];
                var descriptor = ctx.speciesDescriptors[point.speciesID];
                var seeds = descriptor.Seed(ref point.foliageInstance);
                seeds = descriptor.ScaleSeedsByPopulation(seeds, point.foliageInstance);

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
        private const int SparseSpeciesThreshold = 2;
        private const float SparseMatureSurvivalChance = 0.9f;

        public override void Execute(ref SimulationContext ctx) {
            var aliveBySpecies = new Dictionary<int, int>();
            for (int i = 0; i < ctx.points.Count; i++) {
                var p = ctx.points[i];
                if (p.foliageInstance.type == FoliageInstance.FoliageType.Dying) {
                    continue;
                }

                if (!aliveBySpecies.ContainsKey(p.speciesID)) {
                    aliveBySpecies[p.speciesID] = 0;
                }

                aliveBySpecies[p.speciesID]++;
            }

            // Mark dying first, then remove — avoids index shifting issues.
            for (int i = 0; i < ctx.points.Count; i++) {
                var point = ctx.points[i];
                var descriptor = ctx.speciesDescriptors[point.speciesID];
                if (point.foliageInstance.type != FoliageInstance.FoliageType.Dying &&
                    !descriptor.Alive(in point.foliageInstance)) {
                    // Protect isolated mature trees on very sparse populations from stochastic wipeout.
                    if (point.foliageInstance.type == FoliageInstance.FoliageType.Mature &&
                        aliveBySpecies.TryGetValue(point.speciesID, out int speciesAliveCount) &&
                        speciesAliveCount <= SparseSpeciesThreshold &&
                        Simulation.Random.Chance(SparseMatureSurvivalChance)) {
                        continue;
                    }

                    point.foliageInstance.type = FoliageInstance.FoliageType.Dying;
                    point.foliageInstance.strength = 0f;
                    ctx.points[i] = point;
                }
            }

            // Collect dying/dead trees in a separate queue before cleanup.
            foreach (var deadTree in ctx.points.Where(p => p.foliageInstance.type == FoliageInstance.FoliageType.Dying)) {
                ctx.deadPoints.Enqueue(deadTree);
                while (ctx.deadPoints.Count > ctx.deadPointsMaxSize) {
                    ctx.deadPoints.Dequeue();
                }
            }

            ctx.points.RemoveAll(p => p.foliageInstance.type == FoliageInstance.FoliageType.Dying);
        }
    }

    public class CrowdingStressEvent : Event {
        private const float OvercrowdingDistance = 4.0f;
        private const float MaxStressPerEdge = 0.15f;

        private class DelaunayVertex : IVertex {
            public double[] Position { get; set; }
            public int PointIndex;
        }

        public override void Execute(ref SimulationContext ctx) {
            var vertices = new List<DelaunayVertex>();
            vertices.Capacity = ctx.points.Count;

            for (int i = 0; i < ctx.points.Count; i++) {
                var fi = ctx.points[i].foliageInstance;
                if (fi.type != FoliageInstance.FoliageType.Sapling && fi.type != FoliageInstance.FoliageType.Mature) {
                    continue;
                }

                vertices.Add(new DelaunayVertex {
                    Position = new double[] { fi.position.x, fi.position.y },
                    PointIndex = i,
                });
            }

            if (vertices.Count < 3) {
                return;
            }

            ITriangulation<DelaunayVertex, DefaultTriangulationCell<DelaunayVertex>> triangulation;
            try {
                triangulation = Triangulation.CreateDelaunay<DelaunayVertex>(vertices);
            } catch {
                return;
            }

            var edgeSet = new HashSet<(int a, int b)>();
            foreach (var cell in triangulation.Cells) {
                if (cell.Vertices == null || cell.Vertices.Length < 3) continue;

                for (int e = 0; e < 3; e++) {
                    int i1 = cell.Vertices[e].PointIndex;
                    int i2 = cell.Vertices[(e + 1) % 3].PointIndex;
                    if (i1 == i2) continue;
                    if (i1 > i2) (i1, i2) = (i2, i1);
                    edgeSet.Add((i1, i2));
                }
            }

            foreach (var edge in edgeSet) {
                var point1 = ctx.points[edge.a];
                var point2 = ctx.points[edge.b];

                float d = Vector2.Distance(point1.foliageInstance.position, point2.foliageInstance.position);
                if (d >= OvercrowdingDistance) continue;

                float stressDelta = (1f - Mathf.Clamp01(d / OvercrowdingDistance)) * MaxStressPerEdge;

                point1.foliageInstance.stress = Mathf.Min(1f, point1.foliageInstance.stress + stressDelta);
                point2.foliageInstance.stress = Mathf.Min(1f, point2.foliageInstance.stress + stressDelta);

                ctx.points[edge.a] = point1;
                ctx.points[edge.b] = point2;
            }
        }
    }

    public class ResourceDistributionEvent : Event {
        public float waterFromIntegral = 100f;

        private const uint FixedPointScale = 65536u;
        private const string IntegralShaderResourcePath = "ProceduralVegetation/FruitfulnessIntegral";
        private const string CellIndexBakeShaderResourcePath = "ProceduralVegetation/CellIndexBake";

        private static ComputeShader integralShader;
        private static int integralKernel = -1;
        private static Material cellIndexBakeMaterial;

        public override void Execute(ref SimulationContext ctx) {
            int n = ctx.points.Count;
            if (n == 0) return;

            Rect clipRect = ComputeClipRect(ctx);
            var fruitSampler = MapSampler.Create(
                ctx.fruitfulness?.fruitfulnessMap,
                ctx.fruitfulness?.fruitfulnessScale ?? 1f,
                clipRect
            );
            var waterSampler = MapSampler.Create(
                ctx.water?.waterMap,
                ctx.water?.waterScale ?? 1f,
                clipRect
            );
            var lightSampler = MapSampler.Create(
                ctx.lighting?.lightMap,
                ctx.lighting?.lightScale ?? 1f,
                clipRect
            );

            var activeIndices = new List<int>(n);
            for (int i = 0; i < n; i++) {
                var t = ctx.points[i].foliageInstance.type;
                if (t == FoliageInstance.FoliageType.Sapling ||
                    t == FoliageInstance.FoliageType.Mature) {
                    activeIndices.Add(i);
                }
            }

            if (activeIndices.Count == 0) return;
            if (!fruitSampler.hasMap || !EnsureIntegralKernelReady()) return;

            var polygons = new List<Vector2>[activeIndices.Count];

            if (activeIndices.Count == 1) {
                polygons[0] = BuildRectanglePolygon(clipRect);
            } else {
                var inputPoints = new (Vector2 pos, float weight)[activeIndices.Count];
                for (int j = 0; j < activeIndices.Count; j++) {
                    var fi = ctx.points[activeIndices[j]].foliageInstance;
                    inputPoints[j] = (fi.position, Mathf.Max(0f, fi.strength));
                }

                PowerDiagram diagram;
                try {
                    diagram = PowerDiagram.FromPoints(inputPoints);
                } catch {
                    return;
                }

                for (int j = 0; j < activeIndices.Count; j++) {
                    if (!diagram.Cells.TryGetValue(j, out var edges) || edges.Count == 0) {
                        polygons[j] = new List<Vector2>();
                        continue;
                    }

                    polygons[j] = BuildClippedCellPolygon(edges, clipRect);
                }
            }

            float[] fruitIntegrals = IntegratePolygonsGpu(polygons, fruitSampler);
            if (fruitIntegrals == null) return;

            float[] waterIntegrals = waterSampler.hasMap
                ? IntegratePolygonsGpu(polygons, waterSampler)
                : fruitIntegrals;
            if (waterSampler.hasMap && waterIntegrals == null) return;

            float[] lightIntegrals = lightSampler.hasMap
                ? IntegratePolygonsGpu(polygons, lightSampler)
                : fruitIntegrals;
            if (lightSampler.hasMap && lightIntegrals == null) return;

            for (int j = 0; j < activeIndices.Count; j++) {
                float energyIntegral = fruitIntegrals[j];
                float waterIntegral = waterIntegrals[j];
                float lightIntegral = lightIntegrals[j];

                if (energyIntegral <= 0f && waterIntegral <= 0f && lightIntegral <= 0f) continue;

                int pointIdx = activeIndices[j];
                var point = ctx.points[pointIdx];
                ctx.speciesDescriptors[point.speciesID].AddResources(
                    ref point.foliageInstance,
                    energyIntegral * fruitSampler.scale,
                    waterIntegral * (waterSampler.hasMap ? waterSampler.scale : 1f) * waterFromIntegral,
                    lightIntegral * (lightSampler.hasMap ? lightSampler.scale : 1f)
                );
                ctx.points[pointIdx] = point;
            }
        }

        private static bool EnsureIntegralKernelReady() {
            if (integralShader == null) {
                integralShader = Resources.Load<ComputeShader>(IntegralShaderResourcePath);
                if (integralShader == null) {
                    return false;
                }
            }

            if (integralKernel < 0) {
                integralKernel = integralShader.FindKernel("IntegrateAllCells");
            }

            return integralKernel >= 0;
        }

        private static bool EnsureCellIndexBakeMaterialReady() {
            if (cellIndexBakeMaterial != null) {
                return true;
            }

            var shader = Resources.Load<Shader>(CellIndexBakeShaderResourcePath);
            if (shader == null) {
                return false;
            }

            cellIndexBakeMaterial = new Material(shader) {
                hideFlags = HideFlags.HideAndDontSave,
            };
            return true;
        }

        private static Rect ComputeClipRect(SimulationContext ctx) {
            var bb = ctx.landscape.bbox;
            return new Rect(bb.min.x, bb.min.z, bb.size.x, bb.size.z);
        }

        private struct MapSampler {
            public bool hasMap;
            public Texture2D map;
            public int width;
            public int height;
            public Rect worldRect;
            public float texelArea;
            public float scale;

            public static MapSampler Create(Texture2D map, float scale, Rect worldRect) {
                return new MapSampler {
                    hasMap = map != null,
                    map = map,
                    width = map != null ? map.width : 0,
                    height = map != null ? map.height : 0,
                    worldRect = worldRect,
                    texelArea = map != null && map.width > 0 && map.height > 0
                        ? (worldRect.width / map.width) * (worldRect.height / map.height)
                        : 0f,
                    scale = scale,
                };
            }
        }

        private static float[] IntegratePolygonsGpu(List<Vector2>[] polygons, in MapSampler sampler) {
            if (!sampler.hasMap || sampler.map == null) {
                return null;
            }

            int cellCount = polygons.Length;
            if (cellCount == 0) return Array.Empty<float>();

            var indexMap = BakeCellIndexMap(polygons, sampler);
            if (indexMap == null) {
                return null;
            }

            var integralsBuffer = new ComputeBuffer(cellCount, sizeof(uint));

            try {
                integralsBuffer.SetData(new uint[cellCount]);

                integralShader.SetTexture(integralKernel, "FruitfulnessMap", sampler.map);
                integralShader.SetTexture(integralKernel, "CellIndexMap", indexMap);
                integralShader.SetBuffer(integralKernel, "CellIntegrals", integralsBuffer);

                integralShader.SetInt("MapWidth", sampler.width);
                integralShader.SetInt("MapHeight", sampler.height);
                integralShader.SetInt("CellCount", cellCount);
                integralShader.SetFloat("TexelArea", sampler.texelArea);
                integralShader.SetInt("FixedPointScale", (int)FixedPointScale);

                int gx = Mathf.CeilToInt(sampler.width / 8f);
                int gy = Mathf.CeilToInt(sampler.height / 8f);
                integralShader.Dispatch(integralKernel, gx, gy, 1);

                var fixedIntegrals = new uint[cellCount];
                integralsBuffer.GetData(fixedIntegrals);

                var result = new float[cellCount];
                for (int i = 0; i < cellCount; i++) {
                    result[i] = fixedIntegrals[i] / (float)FixedPointScale;
                }

                return result;
            } finally {
                integralsBuffer.Release();
                RenderTexture.ReleaseTemporary(indexMap);
            }
        }

        private static RenderTexture BakeCellIndexMap(List<Vector2>[] polygons, in MapSampler sampler) {
            if (!EnsureCellIndexBakeMaterialReady()) {
                return null;
            }

            var mesh = BuildCellIndexMesh(polygons);
            if (mesh == null) {
                return null;
            }

            var indexMap = RenderTexture.GetTemporary(
                sampler.width,
                sampler.height,
                0,
                RenderTextureFormat.RFloat,
                RenderTextureReadWrite.Linear
            );
            indexMap.filterMode = FilterMode.Point;
            indexMap.wrapMode = TextureWrapMode.Clamp;

            var prevRt = RenderTexture.active;
            Graphics.SetRenderTarget(indexMap);
            GL.Clear(false, true, Color.black);

            GL.PushMatrix();
            GL.LoadProjectionMatrix(Matrix4x4.Ortho(
                sampler.worldRect.xMin,
                sampler.worldRect.xMax,
                sampler.worldRect.yMin,
                sampler.worldRect.yMax,
                -1f,
                1f
            ));

            cellIndexBakeMaterial.SetPass(0);
            Graphics.DrawMeshNow(mesh, Matrix4x4.identity);

            GL.PopMatrix();
            Graphics.SetRenderTarget(prevRt);
            SafeDestroy(mesh);

            return indexMap;
        }

        private static void SafeDestroy(UnityEngine.Object obj) {
            if (obj == null) {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying) {
                UnityEngine.Object.DestroyImmediate(obj);
                return;
            }
#endif
            UnityEngine.Object.Destroy(obj);
        }

        private static Mesh BuildCellIndexMesh(List<Vector2>[] polygons) {
            var vertices = new List<Vector3>(polygons.Length * 12);
            var triangles = new List<int>(polygons.Length * 18);
            var uv1 = new List<Vector2>(polygons.Length * 12);

            for (int cellIndex = 0; cellIndex < polygons.Length; cellIndex++) {
                var poly = polygons[cellIndex];
                if (poly == null || poly.Count < 3) {
                    continue;
                }

                float encodedIndex = cellIndex + 1f;
                Vector2 p0 = poly[0];
                for (int i = 1; i < poly.Count - 1; i++) {
                    Vector2 p1 = poly[i];
                    Vector2 p2 = poly[i + 1];

                    int baseVertex = vertices.Count;
                    vertices.Add(new Vector3(p0.x, p0.y, 0f));
                    vertices.Add(new Vector3(p1.x, p1.y, 0f));
                    vertices.Add(new Vector3(p2.x, p2.y, 0f));

                    uv1.Add(new Vector2(encodedIndex, 0f));
                    uv1.Add(new Vector2(encodedIndex, 0f));
                    uv1.Add(new Vector2(encodedIndex, 0f));

                    triangles.Add(baseVertex);
                    triangles.Add(baseVertex + 1);
                    triangles.Add(baseVertex + 2);
                }
            }

            if (vertices.Count == 0) {
                return null;
            }

            var mesh = new Mesh {
                indexFormat = IndexFormat.UInt32,
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(1, uv1);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static List<Vector2> BuildClippedCellPolygon(
            List<PowerDiagram.VoronoiCellEdge> edges,
            Rect clip
        ) {
            float clipDist = Mathf.Sqrt(clip.width * clip.width + clip.height * clip.height) + 1f;

            var polygon = new List<Vector2>(edges.Count);
            foreach (var edge in edges) {
                float t = float.IsNegativeInfinity(edge.TMin) ? -clipDist : edge.TMin;
                polygon.Add(edge.Origin + edge.Direction * t);
            }

            if (polygon.Count < 3) return new List<Vector2>();

            polygon = ClipPolygonToRect(polygon, clip);
            return polygon;
        }

        private static List<Vector2> BuildRectanglePolygon(Rect r) {
            return new List<Vector2>(4) {
                new Vector2(r.xMin, r.yMin),
                new Vector2(r.xMax, r.yMin),
                new Vector2(r.xMax, r.yMax),
                new Vector2(r.xMin, r.yMax),
            };
        }

        private static List<Vector2> ClipPolygonToRect(List<Vector2> poly, Rect r) {
            poly = ClipByHalfPlane(poly, new Vector2(1f, 0f), r.xMin);
            poly = ClipByHalfPlane(poly, new Vector2(-1f, 0f), -r.xMax);
            poly = ClipByHalfPlane(poly, new Vector2(0f, 1f), r.yMin);
            poly = ClipByHalfPlane(poly, new Vector2(0f, -1f), -r.yMax);
            return poly;
        }

        private static List<Vector2> ClipByHalfPlane(List<Vector2> poly, Vector2 normal, float d) {
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

    }
}
