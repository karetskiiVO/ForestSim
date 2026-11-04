using System;
using System.Collections.Generic;

using ProceduralVegetation;
using ProceduralVegetation.Utilities;

using Unity.VisualScripting;

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

    [SerializeField]
    [FormerlySerializedAs("deadSpruce")]
    [FormerlySerializedAs("deadPine")]
    [FormerlySerializedAs("deadLinden")]
    [FormerlySerializedAs("deadBirch")]
    [FormerlySerializedAs("deadOak")]
    [FormerlySerializedAs("deadBush")]
    private GameObject[] deadPrefabs;

    [SerializeField]
    [FormerlySerializedAs("deadFalledSpruce")]
    [FormerlySerializedAs("deadFalledPine")]
    [FormerlySerializedAs("deadFalledLinden")]
    [FormerlySerializedAs("deadFalledBirch")]
    [FormerlySerializedAs("deadFalledOak")]
    [FormerlySerializedAs("deadFalledBush")]
    private GameObject[] deadFalledPrefabs;

    [Header("Visual Growth")]
    [SerializeField]
    private Vector2 seedScaleRange = new(0.25f, 0.45f);

    [SerializeField]
    private Vector2 saplingScaleRange = new(0.55f, 0.9f);

    [SerializeField]
    private Vector2 matureScaleRange = new(0.95f, 1.35f);

    [SerializeField, Range(0f, 15f)]

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

        var center = Simulation.Random.NextVector2(landscapeBounds);

        var points = new List<Vector2>();

        while (points.Count < initialPopulation) {
            var candidate = Simulation.Random.NextGaussian(35, center);

            if (landscapeBounds.Contains(new(candidate.x, landscapeBounds.center.y, candidate.y))) {
                points.Add(candidate);
            }
        }

        return points.ToArray();
    }

    public override void HandlePointView(Simulation.SimulationPointView point, Vector3 instancePosition) {
        if (!ReferenceEquals(point.descriptor, GetDescriptor())) {
            return;
        }

        var prefab = ResolvePrefab(point.type, point.age, instancePosition);
        if (prefab == null) {
            if (!missingPrefabWarningLogged) {
                missingPrefabWarningLogged = true;
                Debug.LogWarning($"[{name}] Prefab for stage {point.type} is not assigned. Check seed/sapling/mature prefab arrays on this container.");
            }
            return;
        }

        float yaw = GetHash01(point.position, 0x4A7C15) * 2 * Mathf.PI;
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
        if (point.type == FoliageInstance.FoliageType.Dying) {
            return;
        }

        Vector2 range = point.type switch {
            FoliageInstance.FoliageType.Seed => seedScaleRange,
            FoliageInstance.FoliageType.Sapling => saplingScaleRange,
            FoliageInstance.FoliageType.Mature => matureScaleRange,
            _ => matureScaleRange,
        };

        float strengthT = Mathf.Clamp01(point.strength / 3f);
        float ageT = Mathf.Clamp01(point.age / 12f);

        // For seeds use age, for saplings use blend, for mature use max scale (constant)
        float t = point.type switch {
            FoliageInstance.FoliageType.Seed => ageT,
            FoliageInstance.FoliageType.Sapling => Mathf.Clamp01(strengthT * 0.5f + ageT * 0.5f),
            FoliageInstance.FoliageType.Mature => 1f,  // Mature trees always use maximum scale
            _ => 1f,
        };

        float baseScale = Mathf.Lerp(range.x, range.y, t);
        float jitter = Mathf.Lerp(0.92f, 1.08f, GetHash01(point.position, 0x7F4A9D));
        float finalScale = Mathf.Max(0.01f, baseScale * jitter);

        // Add gradual fading effect as tree ages (visual aging)
        var color = instanceTransform.GetComponent<Renderer>()?.material.color ?? Color.white;
        if (point.type == FoliageInstance.FoliageType.Mature) {
            // Fade out gradually based on age to show aging before death
            float maxAge = 400f;  // Reference max age for fading
            float ageFade = Mathf.Clamp01(point.age / maxAge);
            // Between 0.8 and 1.0 alpha for smooth aging
            color.a = Mathf.Lerp(1f, 0.8f, ageFade);
            if (instanceTransform.TryGetComponent<Renderer>(out var renderer)) {
                renderer.material.color = color;
            }
        }

        instanceTransform.localScale = Vector3.one * finalScale;
    }

    HashSet<Vector3> resolvedPositions = new();
    protected virtual GameObject ResolvePrefab(FoliageInstance.FoliageType type, float age, Vector3 position) {
        var hash = position.GetHashCode();
        if (type == FoliageInstance.FoliageType.Dying) {
            if (age < 5f) return null;

            if (!resolvedPositions.Contains(position)) {
                resolvedPositions.Add(position);
                return deadPrefabs.AtCyclic(hash);
            } else {
                return deadFalledPrefabs.AtCyclic(hash);
            }
        }

        return type switch {
            FoliageInstance.FoliageType.Seed => seedPrefabs.AtCyclic(hash),
            FoliageInstance.FoliageType.Sapling => saplingPrefabs.AtCyclic(hash),
            FoliageInstance.FoliageType.Mature => maturePrefabs.AtCyclic(hash),
            _ => null,
        };
    }

    protected abstract TreeSpeciesDescriptor CreateDescriptor();

    private static float GetHash01(Vector2 position, int seed) {
        return (position.GetHashCode() % 10000) * 1.0f / 10000.0f;
    }
}
