using ProceduralVegetation;

public class OakSpeciesContainer : PrefabSpeciesContainerBase {
    protected override TreeSpeciesDescriptor CreateDescriptor() {
        return new OakDescriptor();
    }
}
