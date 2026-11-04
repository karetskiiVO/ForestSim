using ProceduralVegetation;

public class BirchSpeciesContainer : PrefabSpeciesContainerBase {
    protected override TreeSpeciesDescriptor CreateDescriptor() {
        return new BirchDescriptor();
    }
}
