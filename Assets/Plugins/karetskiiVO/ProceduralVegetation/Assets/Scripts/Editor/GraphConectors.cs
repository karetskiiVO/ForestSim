using System;

namespace ProceduralVegetation.Editor {
    [Serializable]
    public struct Landscape {
        [NonSerialized]
        public ILandscapeDescriptor landscapeDescriptor;
    }

}
