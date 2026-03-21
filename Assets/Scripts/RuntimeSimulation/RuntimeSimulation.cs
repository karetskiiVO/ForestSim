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

        var landscape = new RidgedNoiseLandscapeDescriptor() {
            bbox = new Bounds(new Vector3(0, 0, 0), new Vector3(1000, 21, 1000)),
            offset = new Vector2(0, 0),
            octaves = 4,
            lacunarity = 2f,
            persistence = 0.74f,
            sharpness = 0.5f
        };
        bakedLandscape = landscape.Bake(new() { resolution = new(512, 512) });
        DrawLandscape();

        simulation = new Simulation()
            .SetLandscape(bakedLandscape);

        if (speciesContainers != null) {
            foreach (var speciesContainer in speciesContainers) {
                if (speciesContainer == null) {
                    continue;
                }

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

        var heightmap = bakedLandscape.heightmap;
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

                var go = Terrain.CreateTerrainGameObject(terrainData);
                go.name = $"TerrainTile_{tx}_{tz}";
                go.transform.SetParent(terrainParent.transform);

                float originX = bakedLandscape.bbox.center.x - worldSizeX * 0.5f + pixX0 * bakedLandscape.texelSize.x;
                float originZ = bakedLandscape.bbox.center.z - worldSizeZ * 0.5f + pixZ0 * bakedLandscape.texelSize.y;
                go.transform.position = new Vector3(originX, bakedLandscape.minHeight, originZ);
            }
        }
    }
}


