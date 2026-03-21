using ProceduralVegetation;

public class PineSpeciesContainer : PrefabSpeciesContainerBase {
    protected override TreeSpeciesDescriptor CreateDescriptor() {
        return new PineDescriptor();
    }
}
