using System;

using UnityEngine;

namespace ProceduralVegetation.Editor.Nodes {
    [Serializable]
    [CreateNodeMenu("Visualise")]
    public class BakeParametersNode : EditorNode, ISimulated, IResetable {
        [Input] Descriptor<BakedLandscape> landscape;


        private Terrain terrain;

        public void Simulate() {
            var bakedLandscape = GetInputValue<Descriptor<BakedLandscape>>("landscape").descriptor;

            if (bakedLandscape == null) Debug.LogError("Landscape");

            DrawTerrain(bakedLandscape);
        }

        void DrawTerrain(BakedLandscape landscape) {

        }

        public override void Reset() {
            base.Reset();
        }

        void OnDestroy() {

        }
    }
}
