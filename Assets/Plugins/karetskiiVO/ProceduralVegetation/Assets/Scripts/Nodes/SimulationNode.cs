using ProceduralVegetation.Utilities;

using UnityEngine;
using UnityEngine.Assertions;

using XNode;

namespace ProceduralVegetation.Nodes {
    [CreateNodeMenu("Simulation")]
    public class SimulationNode : Node, ISimulated {
        [Header("landscape")]
        [Input]
        public Landscape landscapePort;

        public void Simulate() {
            var landscapePort = (this as Node).GetInputNeighbour<CoreLandscapeNode>();

        }
    }
}
