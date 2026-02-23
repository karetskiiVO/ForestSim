using ProceduralVegetation.Utilities;

using UnityEngine;
using UnityEngine.Assertions;

using XNode;

namespace ProceduralVegetation.Editor.Nodes {
    [CreateNodeMenu("Simulation")]
    public class SimulationNode : EditorNode, ISimulated {
        [Input(connectionType = ConnectionType.Override, typeConstraint = TypeConstraint.Strict)]
        public Descriptor<BakedLandscape> landscape;

        public override void Evaluate() { }

        public void Simulate() {

        }
    }
}
