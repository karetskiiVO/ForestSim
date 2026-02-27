using UnityEngine;

namespace ProceduralVegetation {
    public class OakDescriptor : TreeSpeciesDescriptor {
        public override void Grow(ref FoliageInstance instance, float deltaTime) {
            instance.age += deltaTime;
        }
    }
}