using System;
using System.Collections.Generic;

using ProceduralVegetation;
using ProceduralVegetation.Utilities;

using Sirenix.OdinInspector;

using UnityEngine;
using UnityEngine.Serialization;

public abstract class PrefabSpeciesContainerBase : RuntimeSpeciesContainer {
    [SerializeField]
    [OnValueChanged(nameof(SetupSeed))]
    private string seed = "";

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

    private readonly List<GameObject> instances = new();
    private readonly List<GameObject> futureInstances = new();

    private System.Random random = new();
    private TreeSpeciesDescriptor descriptor;
    private bool missingPrefabWarningLogged;

    private void Awake() {
        SetupSeed();
    }

    private void SetupSeed() {
        random = string.IsNullOrEmpty(seed)
            ? new System.Random()
            : new System.Random(StableHash(seed));
    }

    public override TreeSpeciesDescriptor GetDescriptor() {
        descriptor ??= CreateDescriptor();
        return descriptor;
    }

    public override Vector2[] GetInitialPoints(Bounds landscapeBounds) {
        var points = new Vector2[initialPopulation];
        for (int i = 0; i < points.Length; i++) {
            points[i] = random.NextVector2(landscapeBounds);
        }

        return points;
    }

    public override void HandlePointView(Simulation.SimulationPointView point, Vector3 instancePosition) {
        if (!ReferenceEquals(point.descriptor, GetDescriptor())) {
            return;
        }

        int pointHash = PointHash(point);
        int rotationY = ((pointHash % 360) + 360) % 360;

        var prefab = ResolvePrefab(point.type, pointHash);
        if (prefab == null) {
            if (!missingPrefabWarningLogged) {
                missingPrefabWarningLogged = true;
                Debug.LogWarning($"[{name}] Prefab for stage {point.type} is not assigned. Check seed/sapling/mature prefab arrays on this container.");
            }
            return;
        }

        var instance = Instantiate(prefab, instancePosition, Quaternion.Euler(0f, rotationY, 0f), transform);
        ApplyTransform(point, pointHash, instance.transform);
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

    protected virtual void ApplyTransform(Simulation.SimulationPointView point, int pointHash, Transform instanceTransform) { }

    protected virtual GameObject ResolvePrefab(FoliageInstance.FoliageType type, int hash) {
        return type switch {
            FoliageInstance.FoliageType.Seed => seedPrefabs.AtCyclic(hash),
            FoliageInstance.FoliageType.Sapling => saplingPrefabs.AtCyclic(hash),
            FoliageInstance.FoliageType.Mature => maturePrefabs.AtCyclic(hash),
            _ => null,
        };
    }

    protected abstract TreeSpeciesDescriptor CreateDescriptor();

    protected static float HashTo01(int hash) {
        uint normalized = unchecked((uint)hash);
        return normalized / (float)uint.MaxValue;
    }

    private static int PointHash(Simulation.SimulationPointView point) {
        int px = Mathf.RoundToInt(point.position.x * 100f);
        int py = Mathf.RoundToInt(point.position.y * 100f);

        unchecked {
            int hash = 17;
            hash = hash * 31 + px;
            hash = hash * 31 + py;
            hash = hash * 31 + (int)point.type;
            return hash;
        }
    }

    private static int StableHash(string value) {
        unchecked {
            int hash = 23;
            for (int i = 0; i < value.Length; i++) {
                hash = hash * 31 + value[i];
            }

            return hash;
        }
    }
}
