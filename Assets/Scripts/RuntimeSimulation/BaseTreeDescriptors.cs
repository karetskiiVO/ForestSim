using System.Linq;

using Sirenix.Utilities;

using static ProceduralVegetation.Simulation;

namespace ProceduralVegetation {
    public abstract class TreeSpeciesCountDescriptor : TreeSpeciesDescriptor {
        public void ResetCount() { count = 0; }

        public void HandleInstance (ref FoliageInstance instance) {
            if (instance.type == FoliageInstance.FoliageType.Dying) return;

            count++;
        }

        int count = 0;
        public int Count => count;

        public class TreeSpeciesCounterEventGenerator : EventGenerator {
            public override Event[] Generate(float currentTime) {
                return new Event[] {
                    new TreeSpeciesCounterEvent(){ time = 0f },
                };
            }

            public class TreeSpeciesCounterEvent : ProceduralVegetation.Simulation.Event {
            public override void Execute(ref SimulationContext context) {
                for (int i = 0; i < context.points.Count; i++) {
                    var descriptor = context.speciesDescriptors[context.points[i].speciesID];

                    if (descriptor is not TreeSpeciesCountDescriptor) continue;
                    var point = context.points[i];
                    (descriptor as TreeSpeciesCountDescriptor).HandleInstance(ref point.foliageInstance);
                    context.points[i] = point;
                }
            }
        }
        }
    }

    public abstract class RuntimeSpeciesDescriptor : TreeSpeciesCountDescriptor {}
}
