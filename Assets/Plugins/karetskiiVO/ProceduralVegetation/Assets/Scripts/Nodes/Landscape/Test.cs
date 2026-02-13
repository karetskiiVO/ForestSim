using ProceduralVegetation.Nodes;

using UnityEngine;
using UnityEngine.Assertions;

namespace ProceduralVegetation {
    [CreateNodeMenu("landscape/test1")]
    class Test1 : CoreLandscapeNode {
        public override ILandscapeDescriptor GetLandscapeDescriptor() {
            Debug.Log("test1");
            return null;
        }
    }

    [CreateNodeMenu("landscape/test2")]
    class Test2 : CoreLandscapeNode {
        public override ILandscapeDescriptor GetLandscapeDescriptor() {
            Debug.Log("test2");
            return null;
        }
    }
}
