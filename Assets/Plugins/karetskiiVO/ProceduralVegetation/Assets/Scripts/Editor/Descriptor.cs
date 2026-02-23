using System;

namespace ProceduralVegetation.Editor {
    [Serializable]
    public class Descriptor<DescriptorType> {
        [NonSerialized]
        public DescriptorType descriptor;
    }
}
