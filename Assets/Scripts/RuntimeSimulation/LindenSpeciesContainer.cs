using ProceduralVegetation;

public class LindenSpeciesContainer : PrefabSpeciesContainerBase {
    protected override TreeSpeciesDescriptor CreateDescriptor() {
        return new LindenDescriptor();
    }
}
