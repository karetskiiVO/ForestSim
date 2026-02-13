using UnityEngine;

namespace ProceduralWoods {
    public interface ILandsacapeDescriptor {
        // Границы местности
        public UnityEngine.Bounds bbox { get; }

        public float Height(UnityEngine.Vector2 pos);

        public float NANStrategy(UnityEngine.Vector2 pos) => 0;

        public Utilities.Latitude latitude { get; }
    }
}