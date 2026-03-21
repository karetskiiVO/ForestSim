using ProceduralVegetation;
public class SpruceSpeciesContainer : PrefabSpeciesContainerBase {
    protected override TreeSpeciesDescriptor CreateDescriptor() {
        return new SpruceDescriptor();
    }
}
