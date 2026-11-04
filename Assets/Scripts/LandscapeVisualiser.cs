
using UnityEngine;

using System.Collections.Generic;
using System;

[RequireComponent(typeof(Terrain))]
public class LandscapeVisualiser : MonoBehaviour {
    Terrain terrain;

    public Material landscapeMaterial;
    public Mesh instanceMesh;
    public Material instanceMaterial;

    // CPU preparation options
    public int simulationRandomSeed = 42;
    // If <= 0, a default of half-cell size will be used
    public float poissonMinDistance = -1f;
    // Bridson attempts per active point
    public int poissonK = 30;

    // CPU-filled structured buffer (float4: x,y,z,scale)
    ComputeBuffer instanceDataBuffer;

    public int maxInstances = 65536;
    public int instanceStride = 16;

    // Indirect args buffer for DrawMeshInstancedIndirect
    ComputeBuffer indirectArgsBuffer;   // DrawMeshInstancedIndirect args

    uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

    public int resolution = 1024;
    public float slopeStart = 0.05f;
    public float slopeEnd = 0.3f;
    public float densityThreshold = 0.1f; // порог для записи
    public float minScale = 0.5f;
    public float maxScale = 1.5f;

    void Start() {
        terrain = GetComponent<Terrain>();
        terrain.materialTemplate = landscapeMaterial;

        if (instanceMesh == null || instanceMaterial == null) {
            Debug.LogError("Instance mesh or material not assigned on " + name);
            return;
        }

        CreateBuffers();
        PrepareInstances();
    }

    // Bridson Poisson-disk sampling on unit square [0,1]^2
    List<Vector2> GeneratePoissonDiskSamples(float minDistNormalized, int k, int maxPoints, int seed) {
        List<Vector2> samples = new List<Vector2>();

        // fallback: random jitter if minDist is invalid
        if (minDistNormalized <= 0f) {
            System.Random rnd = new System.Random(seed);
            for (int i = 0; i < maxPoints; i++) samples.Add(new Vector2((float)rnd.NextDouble(), (float)rnd.NextDouble()));
            return samples;
        }

        if (minDistNormalized >= 1f) {
            // domain too small for more than one sample
            System.Random rnd = new System.Random(seed);
            samples.Add(new Vector2((float)rnd.NextDouble(), (float)rnd.NextDouble()));
            return samples;
        }

        float cellSize = minDistNormalized / Mathf.Sqrt(2f);
        int gridW = Mathf.Max(1, Mathf.CeilToInt(1f / cellSize));
        int gridH = gridW;
        int[,] grid = new int[gridW, gridH];
        for (int gx = 0; gx < gridW; gx++) for (int gy = 0; gy < gridH; gy++) grid[gx, gy] = -1;

        System.Random rng = new System.Random(seed);

        // initial sample
        Vector2 first = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
        samples.Add(first);
        List<Vector2> active = new List<Vector2>();
        active.Add(first);
        int fi = Mathf.Clamp((int)(first.x / cellSize), 0, gridW - 1);
        int fj = Mathf.Clamp((int)(first.y / cellSize), 0, gridH - 1);
        grid[fi, fj] = 0;

        while (active.Count > 0 && samples.Count < maxPoints) {
            int aIndex = rng.Next(active.Count);
            Vector2 a = active[aIndex];
            bool found = false;
            for (int i = 0; i < Mathf.Max(1, k); i++) {
                double radius = minDistNormalized * (1.0 + rng.NextDouble());
                double angle = rng.NextDouble() * Math.PI * 2.0;
                float nx = a.x + (float)(radius * Math.Cos(angle));
                float ny = a.y + (float)(radius * Math.Sin(angle));
                if (nx < 0f || nx >= 1f || ny < 0f || ny >= 1f) continue;

                int gx = Mathf.Clamp((int)(nx / cellSize), 0, gridW - 1);
                int gy = Mathf.Clamp((int)(ny / cellSize), 0, gridH - 1);

                bool ok = true;
                int search = 2;
                int minX = Mathf.Max(0, gx - search);
                int maxX = Mathf.Min(gridW - 1, gx + search);
                int minY = Mathf.Max(0, gy - search);
                int maxY = Mathf.Min(gridH - 1, gy + search);
                for (int sx = minX; sx <= maxX && ok; sx++) {
                    for (int sy = minY; sy <= maxY; sy++) {
                        int si = grid[sx, sy];
                        if (si != -1) {
                            Vector2 s = samples[si];
                            if ((s.x - nx) * (s.x - nx) + (s.y - ny) * (s.y - ny) < minDistNormalized * minDistNormalized) { ok = false; break; }
                        }
                    }
                }

                if (ok) {
                    Vector2 np = new Vector2(nx, ny);
                    samples.Add(np);
                    active.Add(np);
                    grid[gx, gy] = samples.Count - 1;
                    found = true;
                    break;
                }
            }

            if (!found) active.RemoveAt(aIndex);
        }

        return samples;
    }

    void PrepareInstances() {
        // ensure terrain reference
        if (terrain == null) terrain = GetComponent<Terrain>();
        if (terrain == null) {
            Debug.LogError("LandscapeVisualiser: Terrain component not found.");
            return;
        }

        // common terrain size used by both CPU and GPU paths
        Vector3 terrainSize = terrain.terrainData.size;

        // CPU path: generate Poisson-disk points over normalized terrain UV space [0..1]
        float maxDim = Mathf.Max(terrainSize.x, terrainSize.z);

        float minDistWorld;
        if (poissonMinDistance > 0f) minDistWorld = poissonMinDistance;
        else minDistWorld = (terrainSize.x / Mathf.Max(1, resolution)) * 0.5f;

        float minDistNormalized = minDistWorld / Mathf.Max(0.0001f, maxDim);

        List<Vector2> samples = GeneratePoissonDiskSamples(minDistNormalized, poissonK, maxInstances, simulationRandomSeed);

        List<Vector4> instances = new List<Vector4>(Mathf.Min(samples.Count, maxInstances));

        float texel = 1f / Mathf.Max(1, resolution);
        for (int i = 0; i < samples.Count && instances.Count < maxInstances; i++) {
            Vector2 uv = samples[i];

            // sample heights in world units, then normalize like the shader (heightTex holds 0..1)
            float hCw = terrain.terrainData.GetInterpolatedHeight(uv.x, uv.y);
            float hLw = terrain.terrainData.GetInterpolatedHeight(Mathf.Clamp01(uv.x - texel), uv.y);
            float hRw = terrain.terrainData.GetInterpolatedHeight(Mathf.Clamp01(uv.x + texel), uv.y);
            float hDw = terrain.terrainData.GetInterpolatedHeight(uv.x, Mathf.Clamp01(uv.y - texel));
            float hUw = terrain.terrainData.GetInterpolatedHeight(uv.x, Mathf.Clamp01(uv.y + texel));

            float terrainHeight = Mathf.Max(terrainSize.y, 0.0001f);
            float hC = hCw / terrainHeight;
            float hL = hLw / terrainHeight;
            float hR = hRw / terrainHeight;
            float hD = hDw / terrainHeight;
            float hU = hUw / terrainHeight;

            float dhdx = (hR - hL) * 0.5f;
            float dhdy = (hU - hD) * 0.5f;

            Vector3 n = new Vector3(-dhdx, 1.0f, -dhdy).normalized;
            float slope = Mathf.Clamp01(1.0f - Vector3.Dot(n, Vector3.up));
            float t = Mathf.SmoothStep(slopeStart, slopeEnd, slope);
            float grassMaskValue = 1.0f - t;

            if (grassMaskValue <= densityThreshold) continue;

            // deterministic per-sample random using seed + index
            System.Random rng = new System.Random(simulationRandomSeed + i * 374761393);
            float rnd = (float)rng.NextDouble();
            float scale = Mathf.Lerp(minScale, maxScale, rnd);

            float worldX = terrain.transform.position.x + uv.x * terrainSize.x;
            float worldZ = terrain.transform.position.z + uv.y * terrainSize.z;
            float worldY = terrain.transform.position.y + hCw;

            instances.Add(new Vector4(worldX, worldY, worldZ, scale));
        }

        // ensure buffers exist
        if (instanceDataBuffer == null || instanceDataBuffer.count < maxInstances) {
            if (instanceDataBuffer != null) instanceDataBuffer.Release();
            instanceDataBuffer = new ComputeBuffer(maxInstances, sizeof(float) * 4);
        }

        if (instances.Count > 0) instanceDataBuffer.SetData(instances.ToArray());
        else {
            // clear first element to safe default
            var zero = new Vector4[1];
            instanceDataBuffer.SetData(zero);
        }

        // bind buffer to material (shader must read StructuredBuffer<float4> appendBuffer)
        instanceMaterial.SetBuffer("appendBuffer", instanceDataBuffer);

        // update indirect args
        uint instanceCount = (uint)instances.Count;
        args[1] = instanceCount;
        indirectArgsBuffer.SetData(args);
        return;

    }

    void CreateBuffers() {
        ReleaseBuffers();

        if (instanceMesh == null) {
            Debug.LogError("Instance mesh not assigned - cannot create buffers.");
            return;
        }

        args[0] = (uint)instanceMesh.GetIndexCount(0);
        args[1] = 0; // instance count filled later
        args[2] = (uint)instanceMesh.GetIndexStart(0);
        args[3] = (uint)instanceMesh.GetBaseVertex(0);
        args[4] = 0;

        indirectArgsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        indirectArgsBuffer.SetData(args);
    }

    void ReleaseBuffers() {
        if (indirectArgsBuffer != null) {
            indirectArgsBuffer.Release();
            indirectArgsBuffer = null;
        }
        if (instanceDataBuffer != null) {
            instanceDataBuffer.Release();
            instanceDataBuffer = null;
        }
    }

    void OnDisable() {
        ReleaseBuffers();
    }

    void Update() {
        if (terrain == null || instanceMesh == null || instanceMaterial == null || indirectArgsBuffer == null) return;

        Vector3 boundsCenter = terrain.transform.position + terrain.terrainData.size * 0.5f;
        Graphics.DrawMeshInstancedIndirect(
            instanceMesh,
            0,
            instanceMaterial,
            new Bounds(boundsCenter, terrain.terrainData.size),
            indirectArgsBuffer
        );
    }
}
