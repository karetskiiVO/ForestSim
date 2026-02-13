using XNode;

using UnityEngine;

using XNodeEditor;

namespace ProceduralVegetation {
    [CreateAssetMenu(menuName = "Procedural forest graph")]
    public class SimulationGraph : NodeGraph {
        // private RootNode rootNode = null;
        // void Reset () {
        //     if (rootNode == null) {
        //         rootNode = AddNode<RootNode>();
        //         rootNode.name = "Root";
        //     }
        // }
        // public override void RemoveNode (Node node) {
        //     // TODO: change type of exception
        //     if (node is RootNode) throw new Exception("Can't remove root node");

        //     base.RemoveNode(node);
        // }

        public void Execute() {
            foreach (var node in nodes) {
                if (node is not ISimulated simNode) continue;

                simNode.Simulate();
            }
        }
    }

    [CustomNodeGraphEditor(typeof(SimulationGraph))]
    public class SimulationGraphEditor : NodeGraphEditor {
        public override void OnGUI() {
            var buttonRect = new Rect(5, 5, 140, 25);

            if (GUI.Button(buttonRect, "Run simulation")) {
                var graph = target as SimulationGraph;

                if (graph != null) graph.Execute();
            }
        }
    }
}
