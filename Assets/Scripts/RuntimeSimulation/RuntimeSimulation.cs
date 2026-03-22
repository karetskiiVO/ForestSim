using System;

using Cysharp.Threading.Tasks;

using ProceduralVegetation;
using ProceduralVegetation.Utilities;

using UnityEngine;

class RuntimeSimulation : MonoBehaviour {
    Simulation simulation;
    BakedLandscape bakedLandscape;

    [SerializeField]
    RuntimeSpeciesContainer[] speciesContainers;

    [SerializeField]
    bool drawMoistureOverlay = true;

    [SerializeField]
    bool normalizeMoistureOverlay = true;

    [SerializeField]
    [Range(0.1f, 4f)]
    float moistureOverlayContrast = 1.1f;

    [SerializeField]
    bool applyMapGeneratorOrientation = false;

    [SerializeField]
    bool logMoistureOverlayStats = true;

    private bool simulationInitialized;

    private void Start() {
        EnsureSimulationInitialized();

        if (simulation == null) {
            Debug.LogError("Failed to initialize simulation in Start().");
            return;
        }

        _ = Run();
    }

    private void EnsureSimulationInitialized() {
        if (simulationInitialized) {
            return;
        }

        var landscape = new AdvancedMountainLandscapeDescriptor() {
            bbox = new Bounds(new Vector3(0, 0, 0), new Vector3(1000, 21, 1000)),
        };
        bakedLandscape = landscape.Bake(new() { resolution = new(512, 512) });

        simulation = new Simulation()
            .SetLandscape(bakedLandscape)
            .GenerateWaterAuto(1000, 0.1f, 1f);

        LogWaterMinMax();

        DrawLandscape();

        if (speciesContainers != null) {
            foreach (var speciesContainer in speciesContainers) {
                if (speciesContainer == null) continue;

                var descriptor = speciesContainer.GetDescriptor();
                var initialPoints = speciesContainer.GetInitialPoints(bakedLandscape.bbox);

                simulation.AddSpecies(descriptor, initialPoints);
            }
        }

        simulationInitialized = true;
    }

    private async UniTaskVoid Run() {
        if (simulation == null) {
            Debug.LogError("Run() called before simulation initialization.");
            return;
        }

        simulation.AddEventGenerator(new BaseEventGenerator());

        while (true) {
            simulation.Run(1);
            DrawTrees();
            await UniTask.Delay(TimeSpan.FromSeconds(0.1));
        }
    }

    private void DrawTrees() {
        if (simulation == null || speciesContainers == null) {
            return;
        }

        foreach (var point in simulation.GetPointsView()) {
            float h = bakedLandscape.Height(point.position);
            if (float.IsNaN(h)) {
                continue;
            }

            var instancePosition = new Vector3(point.position.x, h, point.position.y);

            foreach (var speciesContainer in speciesContainers) {
                if (speciesContainer == null) {
                    continue;
                }

                speciesContainer.HandlePointView(point, instancePosition);
            }
        }

        foreach (var speciesContainer in speciesContainers) {
            if (speciesContainer == null) {
                continue;
            }

            speciesContainer.Flush();
        }
    }

    private const int MAX_TILE_RES = 513;
    private const string TERRAIN_PARENT_NAME = "GeneratedTerrain";

    static int NextValidTerrainResolution(int minSize) {
        int res = 33;
        while (res < minSize && res < 4097) res = (res - 1) * 2 + 1;
        return Mathf.Min(res, 4097);
    }

    private void DrawLandscape() {
        GameObject parent = null;

        var heightmap = simulation.simulationContext.landscape.heightmap;
        int texW = heightmap.width;
        int texH = heightmap.height;
        var rawData = heightmap.GetRawTextureData<float>();

        float worldSizeX = bakedLandscape.texelSize.x * texW;
        float worldSizeZ = bakedLandscape.texelSize.y * texH;
        float worldHeight = Mathf.Max(bakedLandscape.maxHeight - bakedLandscape.minHeight, 0.01f);

        int stride = MAX_TILE_RES - 1;
        int tilesX = Mathf.CeilToInt((float)texW / stride);
        int tilesZ = Mathf.CeilToInt((float)texH / stride);

        int tileRes = NextValidTerrainResolution(MAX_TILE_RES);

        Texture2D moistureDebugTexture = null;
        Color[] moistureDebugPixels = null;
        int moistureDebugWidth = 0;
        int moistureDebugHeight = 0;

        if (drawMoistureOverlay) {
            moistureDebugTexture = BuildMoistureDebugTexture();
            if (moistureDebugTexture != null) {
                moistureDebugPixels = moistureDebugTexture.GetPixels();
                moistureDebugWidth = moistureDebugTexture.width;
                moistureDebugHeight = moistureDebugTexture.height;
            }
        }

        GameObject terrainParent;
        if (parent != null) {
            terrainParent = parent;
        } else {
            var existingParent = GameObject.Find(TERRAIN_PARENT_NAME);
            if (existingParent != null) {
                terrainParent = existingParent;
            } else {
                terrainParent = new GameObject(TERRAIN_PARENT_NAME);
            }
        }

        for (int i = terrainParent.transform.childCount - 1; i >= 0; i--) {
            DestroyImmediate(terrainParent.transform.GetChild(i).gameObject);
        }

        for (int tz = 0; tz < tilesZ; tz++) {
            for (int tx = 0; tx < tilesX; tx++) {
                int pixX0 = tx * stride;
                int pixZ0 = tz * stride;
                int tilePixW = Mathf.Min(MAX_TILE_RES, texW - pixX0);
                int tilePixH = Mathf.Min(MAX_TILE_RES, texH - pixZ0);

                float tileWorldW = (tilePixW - 1) * bakedLandscape.texelSize.x;
                float tileWorldH = (tilePixH - 1) * bakedLandscape.texelSize.y;

                float[,] heights = new float[tileRes, tileRes];
                for (int ly = 0; ly < tileRes; ly++) {
                    for (int lx = 0; lx < tileRes; lx++) {
                        float fu = tileRes > 1 ? (float)lx / (tileRes - 1) * (tilePixW - 1) : 0f;
                        float fv = tileRes > 1 ? (float)ly / (tileRes - 1) * (tilePixH - 1) : 0f;

                        int x0 = Mathf.Clamp(pixX0 + (int)fu, 0, texW - 1);
                        int z0 = Mathf.Clamp(pixZ0 + (int)fv, 0, texH - 1);
                        int x1 = Mathf.Clamp(x0 + 1, 0, texW - 1);
                        int z1 = Mathf.Clamp(z0 + 1, 0, texH - 1);

                        float tx0 = fu - Mathf.Floor(fu);
                        float tz0 = fv - Mathf.Floor(fv);

                        float h00 = rawData[z0 * texW + x0];
                        float h10 = rawData[z0 * texW + x1];
                        float h01 = rawData[z1 * texW + x0];
                        float h11 = rawData[z1 * texW + x1];

                        heights[ly, lx] = Mathf.Lerp(
                            Mathf.Lerp(h00, h10, tx0),
                            Mathf.Lerp(h01, h11, tx0),
                            tz0
                        );
                    }
                }

                var terrainData = new TerrainData {
                    heightmapResolution = tileRes,
                    size = new Vector3(tileWorldW, worldHeight, tileWorldH),
                };
                terrainData.SetHeights(0, 0, heights);

                if (moistureDebugPixels != null) {
                    ApplyMoistureDebugOverlay(
                        terrainData,
                        pixX0,
                        pixZ0,
                        tilePixW,
                        tilePixH,
                        moistureDebugPixels,
                        moistureDebugWidth,
                        moistureDebugHeight
                    );
                }

                var go = Terrain.CreateTerrainGameObject(terrainData);
                go.name = $"TerrainTile_{tx}_{tz}";
                go.transform.SetParent(terrainParent.transform);

                float originX = bakedLandscape.bbox.center.x - worldSizeX * 0.5f + pixX0 * bakedLandscape.texelSize.x;
                float originZ = bakedLandscape.bbox.center.z - worldSizeZ * 0.5f + pixZ0 * bakedLandscape.texelSize.y;
                go.transform.position = new Vector3(originX, bakedLandscape.minHeight, originZ);
            }
        }

        if (moistureDebugTexture != null) {
            Destroy(moistureDebugTexture);
        }
    }

    private Texture2D BuildMoistureDebugTexture() {
        var waterMap = simulation?.simulationContext.water?.waterMap;
        if (waterMap == null) {
            return null;
        }

        var sourceData = waterMap.GetPixelData<float>(0);
        if (sourceData.Length == 0) {
            return null;
        }

        float minValue = float.PositiveInfinity;
        float maxValue = float.NegativeInfinity;
        int nanCount = 0;
        for (int i = 0; i < sourceData.Length; i++) {
            float value = sourceData[i];
            if (float.IsNaN(value)) {
                nanCount++;
                continue;
            }

            if (value < minValue) minValue = value;
            if (value > maxValue) maxValue = value;
        }

        if (float.IsInfinity(minValue) || float.IsInfinity(maxValue)) {
            return null;
        }

        if (logMoistureOverlayStats) {
            Debug.Log($"Moisture map stats: min={minValue:F4}, max={maxValue:F4}, range={(maxValue - minValue):F4}, nan={nanCount}");
        }

        var wetGradient = BuildWetGradient();
        var colors = new Color[sourceData.Length];
        float range = Mathf.Max(maxValue - minValue, 1e-6f);

        for (int i = 0; i < sourceData.Length; i++) {
            float value = sourceData[i];
            if (float.IsNaN(value)) {
                value = minValue;
            }

            float normalized = normalizeMoistureOverlay
                ? Mathf.Clamp01((value - minValue) / range)
                : Mathf.Clamp01(value);

            float contrasted = Mathf.Clamp01((normalized - 0.5f) * moistureOverlayContrast + 0.5f);
            colors[i] = wetGradient.Evaluate(contrasted);
        }

        var texture = new Texture2D(waterMap.width, waterMap.height, TextureFormat.RGBA32, false) {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        texture.SetPixels(colors);
        texture.Apply(true, false);

        if (applyMapGeneratorOrientation) {
            texture = MapGenerator.TextureFlip.FlipHorizontal(texture);
            texture = MapGenerator.TextureFlip.RotateTexture90CounterClockwise(texture);
        }

        return texture;
    }

    private void LogWaterMinMax() {
        var waterMap = simulation?.simulationContext.water?.waterMap;
        if (waterMap == null) {
            Debug.LogWarning("Water map is not available, min/max moisture values were not computed.");
            return;
        }

        var data = waterMap.GetPixelData<float>(0);
        if (data.Length == 0) {
            Debug.LogWarning("Water map is empty, min/max moisture values were not computed.");
            return;
        }

        float minValue = float.PositiveInfinity;
        float maxValue = float.NegativeInfinity;
        for (int i = 0; i < data.Length; i++) {
            float value = data[i];
            if (float.IsNaN(value)) {
                continue;
            }

            if (value < minValue) minValue = value;
            if (value > maxValue) maxValue = value;
        }

        if (float.IsInfinity(minValue) || float.IsInfinity(maxValue)) {
            Debug.LogWarning("Water map contains only NaN values, min/max moisture values were not computed.");
            return;
        }

        Debug.Log($"Moisture min={minValue:F6}, max={maxValue:F6}");
    }

    private static Gradient BuildWetGradient() {
        var wetGradient = new Gradient();

        var colorKeys = new GradientColorKey[3];
        colorKeys[0] = new GradientColorKey(new Color(0.6f, 0.4f, 0.2f), 0.0f);
        colorKeys[1] = new GradientColorKey(new Color(0.3f, 0.6f, 0.2f), 0.5f);
        colorKeys[2] = new GradientColorKey(new Color(0.1f, 0.2f, 0.8f), 1.0f);

        var alphaKeys = new GradientAlphaKey[2];
        alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
        alphaKeys[1] = new GradientAlphaKey(1.0f, 1.0f);

        wetGradient.SetKeys(colorKeys, alphaKeys);
        return wetGradient;
    }

    private static void ApplyMoistureDebugOverlay(
        TerrainData terrainData,
        int pixX0,
        int pixZ0,
        int tilePixW,
        int tilePixH,
        Color[] moistureDebugPixels,
        int moistureDebugWidth,
        int moistureDebugHeight
    ) {
        var tileTexture = new Texture2D(tilePixW, tilePixH, TextureFormat.RGBA32, false) {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var tileColors = new Color[tilePixW * tilePixH];
        for (int y = 0; y < tilePixH; y++) {
            int srcY = Mathf.Clamp(pixZ0 + y, 0, moistureDebugHeight - 1);
            int srcRow = srcY * moistureDebugWidth;
            int dstRow = y * tilePixW;

            for (int x = 0; x < tilePixW; x++) {
                int srcX = Mathf.Clamp(pixX0 + x, 0, moistureDebugWidth - 1);
                tileColors[dstRow + x] = moistureDebugPixels[srcRow + srcX];
            }
        }

        tileTexture.SetPixels(tileColors);
        tileTexture.Apply(false, false);

        var textureLayer = new TerrainLayer {
            diffuseTexture = tileTexture,
            normalMapTexture = null,
            tileSize = new Vector2(terrainData.size.x, terrainData.size.z),
        };

        terrainData.terrainLayers = new TerrainLayer[] { textureLayer };

        float[,,] alphamap = new float[terrainData.alphamapWidth, terrainData.alphamapHeight, 1];
        for (int x = 0; x < terrainData.alphamapWidth; x++) {
            for (int y = 0; y < terrainData.alphamapHeight; y++) {
                alphamap[x, y, 0] = 1f;
            }
        }

        terrainData.SetAlphamaps(0, 0, alphamap);
    }
}


