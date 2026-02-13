using UnityEngine;
using UnityEngine.Assertions;

using XNode;

namespace ProceduralVegetation.Nodes {
    [CreateNodeMenu("Simulation")]
    public class SimulationNode : Node, ISimulated {
        [Input] public DescriptorGetter<ILandscapeDescriptor> landscape = null;

        public void Simulate() {
            landscape.GetDescriptor?.Invoke();
        }
    }
}
