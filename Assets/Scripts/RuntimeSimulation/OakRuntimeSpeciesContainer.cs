using System.Collections.Generic;
using System.Linq;

using ProceduralVegetation;
using ProceduralVegetation.Utilities;

using UnityEngine;

using static ProceduralVegetation.Simulation;

public class OakRuntimeSpeciesContainer : RuntimeSpeciesContainer {
    [SerializeField]
    GameObject[] seedOak;
    [SerializeField]
    GameObject[] smallOak;
    [SerializeField]
    GameObject[] matureOak;
    [SerializeField]
    GameObject[] deadOak;
    [SerializeField, Min(0)]
    int initialPointsCount = 5;

    readonly Dictionary<FoliageInstance.FoliageType, List<(Vector3 position, float rotationY)>> bufferedPositions = new();

    (Mesh mesh, Material material)[] seedOakInstanceInfos => CollectMeshesFromChildren(seedOak);
    (Mesh mesh, Material material)[] smallOakInstanceInfos => CollectMeshesFromChildren(smallOak);
    (Mesh mesh, Material material)[] matureOakInstanceInfos => CollectMeshesFromChildren(matureOak);
    (Mesh mesh, Material material)[] deadOakInstanceInfos => CollectMeshesFromChildren(deadOak);

    public override TreeSpeciesDescriptor GetDescriptor() {
        return new OakDescriptor();
    }

    public override Vector2[] GetInitialPoints(BakedLandscape bakedLandscape) {
        return Enumerable.Range(0, initialPointsCount)
            .Select(_ => Simulation.Random.NextVector2(bakedLandscape.bbox))
            .ToArray();
    }

    public override void HandlePointView(SimulationPointView point, Vector3 instancePosition) {
        if (point.descriptor is not OakDescriptor) {
            return;
        }

        if (!bufferedPositions.TryGetValue(point.type, out var positions)) {
            positions = new List<(Vector3 position, float rotationY)>();
            bufferedPositions[point.type] = positions;
        }

        positions.Add((instancePosition, UnityEngine.Random.Range(0f, 360f)));
    }

    public override void Flush() {
        foreach (var (treeType, positions) in bufferedPositions) {
            var instanceInfos = treeType switch {
                FoliageInstance.FoliageType.Seed => seedOakInstanceInfos,
                FoliageInstance.FoliageType.Sapling => smallOakInstanceInfos,
                FoliageInstance.FoliageType.Mature => matureOakInstanceInfos,
                FoliageInstance.FoliageType.Dying => deadOakInstanceInfos,
                _ => new (Mesh mesh, Material material)[0]
            };

            if (instanceInfos.Length == 0 || positions.Count == 0) {
                continue;
            }

            var matrices = new Matrix4x4[positions.Count];
            for (int i = 0; i < positions.Count; i++) {
                var (position, bufferedRotationY) = positions[i];
                matrices[i] = Matrix4x4.TRS(
                    position,
                    Quaternion.Euler(0, bufferedRotationY, 0),
                    Vector3.one
                );
            }

            foreach (var (mesh, material) in instanceInfos) {
                if (mesh != null && material != null) {
                    Graphics.DrawMeshInstanced(mesh, 0, material, matrices);
                }
            }
        }

        bufferedPositions.Clear();
    }

    private (Mesh mesh, Material material)[] CollectMeshesFromChildren(GameObject[] parents) {
        var meshes = new List<(Mesh mesh, Material material)>();

        foreach (var parent in parents) {
            var meshFilters = parent.GetComponentsInChildren<MeshFilter>();
            var meshRenderers = parent.GetComponentsInChildren<MeshRenderer>();

            for (int i = 0; i < meshFilters.Length && i < meshRenderers.Length; i++) {
                var mesh = meshFilters[i].sharedMesh;
                var material = meshRenderers[i].sharedMaterial;

                if (mesh != null && material != null) {
                    meshes.Add((mesh, material));
                }
            }
        }

        return meshes.ToArray();
    }
}
