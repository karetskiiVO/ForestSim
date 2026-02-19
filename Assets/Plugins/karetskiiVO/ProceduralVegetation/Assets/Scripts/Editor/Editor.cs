using XNode;

using UnityEngine;

using XNodeEditor;

using Sirenix.Utilities;

using ProceduralVegetation.Editor.Nodes;

namespace ProceduralVegetation.Editor {
    [CreateAssetMenu(menuName = "Procedural forest graph")]
    public class SimulationGraph : NodeGraph {
        public void Execute() {
            nodes
                .FilterCast<IResetable>()
                .ForEach(r => r.Reset());

            nodes
                .FilterCast<EditorNode>()
                .ForEach(node => node.Evaluate());

            nodes
                .FilterCast<ISimulated>()
                .ForEach(s => s.Simulate());
        }
    }

    [CustomNodeGraphEditor(typeof(SimulationGraph))]
    public class SimulationGraphEditor : NodeGraphEditor {
        public override void OnGUI() {
            var buttonRect = new Rect(5, 5, 140, 25);

            if (GUI.Button(buttonRect, "Force Run")) {
                var graph = target as SimulationGraph;

                if (graph != null) graph.Execute();
            }
        }
    }
}
