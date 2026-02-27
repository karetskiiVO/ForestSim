using UnityEngine;

using static ProceduralVegetation.Simulation;

namespace ProceduralVegetation {
    class GrowthEvent : Simulation.Event {
        public float deltaTime = 1f;

        public override void Execute(ref SimulationContext ctx) {
            for (int i = 0; i < ctx.points.Count; i++) {
                var point = ctx.points[i];
                var speciesDescriptorID = point.speciesID;
                var speciesDescriptor = ctx.speciesDescriptors[speciesDescriptorID];

                speciesDescriptor.Grow(ref point.foliageInstance, deltaTime);
                ctx.points[i] = point;
            }
        }
    }
}