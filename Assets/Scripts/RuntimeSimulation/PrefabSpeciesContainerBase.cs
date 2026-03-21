using System;
using System.Collections.Generic;

using ProceduralVegetation;

using UnityEngine;
using UnityEngine.Serialization;

public abstract class PrefabSpeciesContainerBase : RuntimeSpeciesContainer {
    [SerializeField, Min(1)]
    private int initialPopulation = 6;

    [SerializeField]
    [FormerlySerializedAs("seedSpruce")]
    [FormerlySerializedAs("seedPine")]
    [FormerlySerializedAs("seedLinden")]
    [FormerlySerializedAs("seedBirch")]
    [FormerlySerializedAs("seedOak")]
    [FormerlySerializedAs("seedBush")]
    private GameObject[] seedPrefabs;

    [SerializeField]
    [FormerlySerializedAs("saplingSpruce")]
    [FormerlySerializedAs("saplingPine")]
    [FormerlySerializedAs("saplingLinden")]
    [FormerlySerializedAs("saplingBirch")]
    [FormerlySerializedAs("saplingOak")]
    [FormerlySerializedAs("saplingBush")]
    private GameObject[] saplingPrefabs;

    [SerializeField]
    [FormerlySerializedAs("matureSpruce")]
    [FormerlySerializedAs("maturePine")]
    [FormerlySerializedAs("matureLinden")]
    [FormerlySerializedAs("matureBirch")]
    [FormerlySerializedAs("matureOak")]
    [FormerlySerializedAs("matureBush")]
    private GameObject[] maturePrefabs;

    [Header("Visual Growth")]
    [SerializeField]
    private Vector2 seedScaleRange = new(0.25f, 0.45f);

    [SerializeField]
    private Vector2 saplingScaleRange = new(0.55f, 0.9f);

    [SerializeField]
    private Vector2 matureScaleRange = new(0.95f, 1.35f);

    [SerializeField, Range(0f, 15f)]
    private float maxRandomYaw = 8f;

    private readonly List<GameObject> instances = new();
    private readonly List<GameObject> futureInstances = new();

    private TreeSpeciesDescriptor descriptor;
    private bool missingPrefabWarningLogged;

    public override TreeSpeciesDescriptor GetDescriptor() {
        descriptor ??= CreateDescriptor();
        return descriptor;
    }

    public override Vector2[] GetInitialPoints(Bounds landscapeBounds) {
        if (initialPopulation <= 0) {
            return Array.Empty<Vector2>();
        }

        var points = new Vector2[initialPopulation];
        int grid = Mathf.CeilToInt(Mathf.Sqrt(initialPopulation));
        float stepX = landscapeBounds.size.x / (grid + 1f);
        float stepY = landscapeBounds.size.z / (grid + 1f);
        Vector2 min = new Vector2(landscapeBounds.min.x, landscapeBounds.min.z);

        for (int i = 0; i < points.Length; i++) {
            int x = i % grid;
            int y = i / grid;
            points[i] = new Vector2(
                min.x + (x + 1) * stepX,
                min.y + (y + 1) * stepY
            );
        }

        return points;
    }

    public override void HandlePointView(Simulation.SimulationPointView point, Vector3 instancePosition) {
        if (!ReferenceEquals(point.descriptor, GetDescriptor())) {
            return;
        }

        var prefab = ResolvePrefab(point.type, 0);
        if (prefab == null) {
            if (!missingPrefabWarningLogged) {
                missingPrefabWarningLogged = true;
                Debug.LogWarning($"[{name}] Prefab for stage {point.type} is not assigned. Check seed/sapling/mature prefab arrays on this container.");
            }
            return;
        }

        float yaw = maxRandomYaw > 0f ? GetHash01(point.position, 0x4A7C15) * maxRandomYaw : 0f;
        var instance = Instantiate(prefab, instancePosition, Quaternion.Euler(0f, yaw, 0f), transform);
        ApplyTransform(point, 0, instance.transform);
        futureInstances.Add(instance);
        instance.SetActive(false);
    }

    public override void Flush() {
        foreach (var instance in instances) {
#if UNITY_EDITOR
            DestroyImmediate(instance);
#else
            Destroy(instance);
#endif
        }

        instances.Clear();

        foreach (var instance in futureInstances) {
            if (instance == null) {
                continue;
            }

            instance.SetActive(true);
            instances.Add(instance);
        }

        futureInstances.Clear();
    }

    protected virtual void ApplyTransform(Simulation.SimulationPointView point, int pointHash, Transform instanceTransform) {
        Vector2 range = point.type switch {
            FoliageInstance.FoliageType.Seed => seedScaleRange,
            FoliageInstance.FoliageType.Sapling => saplingScaleRange,
            FoliageInstance.FoliageType.Mature => matureScaleRange,
            _ => matureScaleRange,
        };

        float strengthT = Mathf.Clamp01(point.strength / 3f);
        float ageT = Mathf.Clamp01(point.age / 12f);

        // For seeds use age, for saplings use blend, for mature use strength
        float t = point.type switch {
            FoliageInstance.FoliageType.Seed => ageT,
            FoliageInstance.FoliageType.Sapling => Mathf.Clamp01(strengthT * 0.5f + ageT * 0.5f),
            FoliageInstance.FoliageType.Mature => strengthT,
            _ => strengthT,
        };

        float baseScale = Mathf.Lerp(range.x, range.y, t);
        float jitter = Mathf.Lerp(0.92f, 1.08f, GetHash01(point.position, 0x7F4A9D));
        float finalScale = Mathf.Max(0.01f, baseScale * jitter);

        instanceTransform.localScale = Vector3.one * finalScale;
    }

    protected virtual GameObject ResolvePrefab(FoliageInstance.FoliageType type, int hash) {
        static GameObject FirstAssigned(GameObject[] prefabs) {
            if (prefabs == null) {
                return null;
            }

            for (int i = 0; i < prefabs.Length; i++) {
                if (prefabs[i] != null) {
                    return prefabs[i];
                }
            }

            return null;
        }

        return type switch {
            FoliageInstance.FoliageType.Seed => FirstAssigned(seedPrefabs),
            FoliageInstance.FoliageType.Sapling => FirstAssigned(saplingPrefabs),
            FoliageInstance.FoliageType.Mature => FirstAssigned(maturePrefabs),
            _ => null,
        };
    }

    protected abstract TreeSpeciesDescriptor CreateDescriptor();

    private static float GetHash01(Vector2 position, int seed) {
        unchecked {
            int x = Mathf.RoundToInt(position.x * 100f);
            int y = Mathf.RoundToInt(position.y * 100f);
            int hash = seed;
            hash = (hash * 397) ^ x;
            hash = (hash * 397) ^ y;
            uint u = (uint)hash;
            return (u & 0x00FFFFFF) / 16777215f;
        }
    }
}
