using ProceduralVegetation;

using UnityEngine;

public class BushSpeciesContainer : PrefabSpeciesContainerBase {
    [SerializeField]
    private Vector2 seedScaleRange = new(0.35f, 0.55f);

    [SerializeField]
    private Vector2 saplingScaleRange = new(0.65f, 0.95f);

    [SerializeField]
    private Vector2 matureScaleRange = new(1.05f, 1.45f);

    [SerializeField]
    private float maxTilt = 4f;

    protected override TreeSpeciesDescriptor CreateDescriptor() {
        return new BushDescriptor();
    }

    protected override GameObject ResolvePrefab(FoliageInstance.FoliageType type, int hash) {
        return base.ResolvePrefab(FoliageInstance.FoliageType.Mature, hash);
    }

}
