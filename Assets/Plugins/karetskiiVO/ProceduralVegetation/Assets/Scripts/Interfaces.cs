using UnityEngine;

namespace ProceduralVegetation {
    public interface ILandscapeDescriptor {
        // Границы местности
        public UnityEngine.Bounds bbox { get; }

        public float Height(UnityEngine.Vector2 pos);

        public float NANStrategy(UnityEngine.Vector2 pos) => 0;

        public Utilities.Latitude latitude { get; }
    }

    public interface ISimulated {
        void Simulate();
    }

    public interface IResetable {
        void Reset();
    }
}
