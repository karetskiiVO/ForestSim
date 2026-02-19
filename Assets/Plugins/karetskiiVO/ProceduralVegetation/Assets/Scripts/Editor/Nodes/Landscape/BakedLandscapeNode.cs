using System;
using System.Collections.Generic;
using System.Reflection;

using UnityEditor;

using UnityEngine;

using XNodeEditor;

using static XNode.Node;

namespace ProceduralVegetation.Editor.Nodes {
    [Serializable]
    [CreateNodeMenu("landscape/baked")]
    class BakedLandscapeNode : CoreLandscapeNode {
        // TODO: Remove input port
        public BakedLandscape descriptor;

        public override ILandscapeDescriptor GetLandscapeDescriptor() {
            return descriptor;
        }
    }
}
