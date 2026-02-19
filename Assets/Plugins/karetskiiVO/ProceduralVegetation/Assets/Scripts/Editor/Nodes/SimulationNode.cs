using ProceduralVegetation.Utilities;

using UnityEngine;
using UnityEngine.Assertions;

using XNode;

namespace ProceduralVegetation.Editor.Nodes {
    [CreateNodeMenu("Simulation")]
    public class SimulationNode : EditorNode, ISimulated {
        [Input]
        public Descriptor<ILandscapeDescriptor> landscape;

        public override void Evaluate() { }

        public void Simulate() {
            Debug.Log(GetInputValue<Descriptor<ILandscapeDescriptor>>("landscape").descriptor);
        }
    }
}
