using ProceduralVegetation;

using UnityEngine;

public class BushSpeciesContainer : PrefabSpeciesContainerBase {
    [SerializeField]
    private Vector2 bushSeedScaleRange = new(0.35f, 0.55f);

    [SerializeField]
    private Vector2 bushSaplingScaleRange = new(0.65f, 0.95f);

    [SerializeField]
    private Vector2 bushMatureScaleRange = new(1.05f, 1.45f);

    protected override TreeSpeciesDescriptor CreateDescriptor() {
        return new BushDescriptor();
    }

    protected override void ApplyTransform(Simulation.SimulationPointView point, int pointHash, Transform instanceTransform) {
        Vector2 range = point.type switch {
            FoliageInstance.FoliageType.Seed => bushSeedScaleRange,
            FoliageInstance.FoliageType.Sapling => bushSaplingScaleRange,
            FoliageInstance.FoliageType.Mature => bushMatureScaleRange,
            _ => bushMatureScaleRange,
        };

        float strengthT = Mathf.Clamp01(point.strength / 3f);
        float ageT = Mathf.Clamp01(point.age / 12f);

        float t = point.type switch {
            FoliageInstance.FoliageType.Seed => ageT,
            FoliageInstance.FoliageType.Sapling => Mathf.Clamp01(strengthT * 0.5f + ageT * 0.5f),
            FoliageInstance.FoliageType.Mature => 1f,
            _ => 1f,
        };

        float baseScale = Mathf.Lerp(range.x, range.y, t);
        float finalScale = Mathf.Max(0.01f, baseScale);
        instanceTransform.localScale = Vector3.one * finalScale;
    }
}
