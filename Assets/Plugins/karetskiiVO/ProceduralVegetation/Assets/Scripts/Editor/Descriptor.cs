using System;

namespace ProceduralVegetation.Editor {
    [Serializable]
    public struct Descriptor<DescriptorType> {
        [NonSerialized]
        public DescriptorType descriptor;
    }
}
