using System;
using System.Collections.Generic;
using System.Linq;

using Cysharp.Threading.Tasks;

using ProceduralVegetation;
using ProceduralVegetation.Utilities;

using Unity.VisualScripting;

using UnityEngine;

class RuntimeSimulation : MonoBehaviour {
    Simulation simulation;
    BakedLandscape bakedLandscape;

    [SerializeField]
    RuntimeSpeciesContainer[] speciesContainers;

    [SerializeField]
    [Range(1, 8)]
    int fruitfulnessNoiseOctaves = 4;

    [SerializeField]
    [Range(0.1f, 0.95f)]
    float fruitfulnessNoisePersistence = 0.5f;

    [SerializeField]
    float fruitfulnessNoiseScale = 0.02f;

    [SerializeField]
    int fruitfulnessNoiseSeed = 1337;

    List<TreeSpeciesCountDescriptor> descriptors;

    private void Start() {
        descriptors ??= new();

        var landscape = new AdvancedMountainLandscapeDescriptor() {
            bbox = new Bounds(new Vector3(0, 0, 0), new Vector3(500, 21, 500)),
        };
        bakedLandscape = landscape.Bake(new() { resolution = new(512, 512) });

        var fruitfulnessMap = CreateFruitfulnessNoiseMap(
            bakedLandscape.heightmap.width,
            bakedLandscape.heightmap.height
        );

        simulation = new Simulation()
            .SetLandscape(bakedLandscape)
            .SetFruitfulness(new LanscapeFruitfillness() {
                fruitfulnessMap = fruitfulnessMap,
                fruitfulnessScale = 1f,
            })
            .GenerateWaterAuto(1000, 0.1f, 1f, 0.002f)
            .AddEventGenerator(new TreeSpeciesCountDescriptor.TreeSpeciesCounterEventGenerator())
            .AddEventGenerator(new EnergyLoggerEventGenerator());

        DrawLandscape();

        if (speciesContainers != null) {
            foreach (var speciesContainer in speciesContainers) {
                if (speciesContainer == null) continue;

                var descriptor = speciesContainer.GetDescriptor();
                var initialPoints = speciesContainer.GetInitialPoints(bakedLandscape.bbox);

                simulation.AddSpecies(descriptor, initialPoints);
                descriptors.Add(descriptor as TreeSpeciesCountDescriptor);
            }
        }

        if (simulation == null) {
            Debug.LogError("Failed to initialize simulation in Start().");
            return;
        }

        _ = Run();
    }

    private Texture2D CreateFruitfulnessNoiseMap(int width, int height) {
        var map = new Texture2D(width, height, TextureFormat.RFloat, false) {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        var data = new float[width * height];
        var random = new System.Random(fruitfulnessNoiseSeed);
        float offsetX = random.Next(-100000, 100000);
        float offsetY = random.Next(-100000, 100000);

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                float total = 0f;
                float amplitude = 1f;
                float frequency = 1f;
                float maxValue = 0f;

                for (int octave = 0; octave < fruitfulnessNoiseOctaves; octave++) {
                    float sampleX = (x + offsetX) * fruitfulnessNoiseScale * frequency;
                    float sampleY = (y + offsetY) * fruitfulnessNoiseScale * frequency;

                    total += Mathf.PerlinNoise(sampleX, sampleY) * amplitude;
                    maxValue += amplitude;
                    amplitude *= fruitfulnessNoisePersistence;
                    frequency *= 2f;
                }

                data[y * width + x] = maxValue > 0f
                    ? Mathf.Clamp01(total / maxValue)
                    : 0f;
            }
        }

        map.SetPixelData(data, 0);
        map.Apply(false, false);
        return map;
    }

    private async UniTaskVoid Run() {
        if (simulation == null) {
            Debug.LogError("Run() called before simulation initialization.");
            return;
        }

        simulation.AddEventGenerator(new BaseEventGenerator());

        float accumulatedTime = 0f;
        while (true) {
            simulation.Run(0.1f);
            accumulatedTime += 1f;

            Debug.Log(
                $"{accumulatedTime:F0} years; {simulation.simulationContext.points.Count}; " +
                string.Join(" ", descriptors.Select(descr => $"({descr.GetType()}:{descr.Count})"))
            );

            DrawTrees();
            await UniTask.Delay(TimeSpan.FromSeconds(1));
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


