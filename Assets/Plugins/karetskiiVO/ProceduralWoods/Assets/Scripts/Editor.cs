using XNode;
using UnityEngine;
using ProceduralWoods.Nodes;
using System;

namespace ProceduralWoods
{
    [CreateAssetMenu(menuName = "ProceduralWoods")]
    public class Editor : NodeGraph {
        private RootNode rootNode = null;

        void Reset () {
            if (rootNode == null) {
                rootNode = AddNode<RootNode>();
                rootNode.name = "Root";
            }
        }

        public override void RemoveNode (Node node) {
            // TODO: change type of exception
            if (node is RootNode) throw new Exception("Can't remove root node");

            base.RemoveNode(node);
        }
    }
}