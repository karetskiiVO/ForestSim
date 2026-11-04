using System;

using UnityEngine;

namespace ProceduralVegetation.Editor.Nodes {
    [Serializable]
    [CreateNodeMenu("Debug/Visualise")]
    public class VisualisationNode : EditorNode, ISimulated, IResetable {
        [Input(connectionType = ConnectionType.Override, typeConstraint = TypeConstraint.Strict)]
        public Descriptor<BakedLandscape> landscape;
        public GameObject parent = null;
        public Material terrainMaterial = null;

        private const int MAX_TILE_RES = 513;
        private const string TERRAIN_PARENT_NAME = "GeneratedTerrain";

        public void Simulate() {
            var bakedLandscape = GetInputValue<Descriptor<BakedLandscape>>("landscape")?.descriptor;
            DrawTerrain(bakedLandscape);
        }

        void DrawTerrain(BakedLandscape landscape) {
            var heightmap = landscape.heightmap;
            int texW = heightmap.width;
            int texH = heightmap.height;
            var rawData = heightmap.GetRawTextureData<float>();

            float worldSizeX = landscape.texelSize.x * texW;
            float worldSizeZ = landscape.texelSize.y * texH;
            float worldHeight = Mathf.Max(landscape.maxHeight - landscape.minHeight, 0.01f);

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

                    float tileWorldW = (tilePixW - 1) * landscape.texelSize.x;
                    float tileWorldH = (tilePixH - 1) * landscape.texelSize.y;

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

                    float originX = landscape.bbox.center.x - worldSizeX * 0.5f + pixX0 * landscape.texelSize.x;
                    float originZ = landscape.bbox.center.z - worldSizeZ * 0.5f + pixZ0 * landscape.texelSize.y;
                    go.transform.position = new Vector3(originX, landscape.minHeight, originZ);
                }
            }
        }

        static int NextValidTerrainResolution(int minSize) {
            int res = 33;
            while (res < minSize && res < 4097)
                res = (res - 1) * 2 + 1;
            return Mathf.Min(res, 4097);
        }

        public override void Reset() {
            base.Reset();
        }

        void OnDestroy() {

        }
    }

    [Serializable]
    [CreateNodeMenu("Debug/Visualise scatter")]
    public class ScatterVisualisationNode : EditorNode, ISimulated, IResetable {
        [Input(connectionType = ConnectionType.Override, typeConstraint = TypeConstraint.Strict)]
        public Descriptor<BakedLandscape> landscape;
        [Input(connectionType = ConnectionType.Override, typeConstraint = TypeConstraint.Strict)]
        public Descriptor<Vector2[]> points;

        public Color color;
        public GameObject parent;
        public GameObject drawable;

        private const string SCATTER_CONTAINER_NAME = "ScatterPoints";
        private GameObject container;

        public void Simulate() {
            var landscape = GetInputValue<Descriptor<BakedLandscape>>("landscape")?.descriptor;
            var points = GetInputValue<Descriptor<Vector2[]>>("points")?.descriptor;

            DrawPoints(landscape, points);
        }

        void ClearPoints() {
            if (container != null) {
                GameObject.DestroyImmediate(container);
                container = null;
            }
        }

        void DrawPoints(BakedLandscape landscape, Vector2[] points) {
            if (landscape == null || points == null) return;

            ClearPoints();

            container = new GameObject(SCATTER_CONTAINER_NAME);
            if (parent != null)
                container.transform.SetParent(parent.transform);

            foreach (var pt in points) {
                float h = landscape.Height(pt);
                if (float.IsNaN(h)) continue;

                GameObject go;
                if (drawable != null) {
                    go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(drawable, container.transform);
                    if (go == null)
                        go = GameObject.Instantiate(drawable, container.transform);
                } else {
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.transform.SetParent(container.transform);
                    go.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = color };
                    GameObject.DestroyImmediate(go.GetComponent<SphereCollider>());
                }

                go.transform.position = new Vector3(pt.x, h, pt.y);
            }
        }
    }
}
