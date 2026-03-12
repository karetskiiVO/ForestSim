
using System.Linq;

using ProceduralVegetation.Utilities;

using UnityEngine;
using UnityEngine.Assertions;

using XNode;

namespace ProceduralVegetation.Editor.Nodes {
    [CreateNodeMenu("Simulation")]
    public class SimulationNode : EditorNode, ISimulated {
        [Input(connectionType = ConnectionType.Override, typeConstraint = TypeConstraint.Strict)]
        public Descriptor<BakedLandscape> inLandscape;
        [Input(connectionType = ConnectionType.Override, typeConstraint = TypeConstraint.Strict)]
        public Descriptor<Vector2[]> inPoints;

        [Output]
        public Descriptor<BakedLandscape> outLandscape;
        [Output]
        public Descriptor<Vector2[]> outPoints;

        public override void Evaluate() { }

        /*
        ** This is temporary part for testing the simulation logic. The final version will likely involve more nodes and a more complex data flow, but for now this serves as a sandbox for running the simulation and visualizing results.
        */
        GameObject parent;
        GameObject[] seedOak;
        GameObject[] smallOak;
        GameObject[] matureOak;
        GameObject[] deadOak;

        /*
        **
        */

        public float simulationTime = 100f;

        public void Simulate() {
            var landscape = GetInputValue<Descriptor<BakedLandscape>>(nameof(inLandscape))?.descriptor;
            Assert.IsNotNull(landscape);

            var oakPoints = Enumerable.Range(0, 5)
                .Select(_ => Simulation.Random.NextVector2(landscape.bbox))
                .ToArray();

            var simulation = new Simulation()
                .SetLandscape(landscape)
                .AddEventGenerator(new BaseEventGenerator())
                .AddSpecies(new OakDescriptor(), oakPoints);

            simulation.Run(simulationTime);

            /// Debug

            if (parent == null) {
                var existingParent = GameObject.Find("SimulationResult");
                if (existingParent != null) {
                    parent = existingParent;
                } else {
                    parent = new GameObject("SimulationResult");
                }
            }

            parent.RemoveChildren();

            foreach (var point in simulation.GetPointsView()) {
                var prefabArr = point.type switch {
                    FoliageInstance.FoliageType.Seed => seedOak,
                    FoliageInstance.FoliageType.Sapling => smallOak,
                    FoliageInstance.FoliageType.Mature => matureOak,
                    FoliageInstance.FoliageType.Dying => deadOak,
                    _ => null
                };

                float h = landscape.Height(point.position);
                if (float.IsNaN(h)) continue;

                if (prefabArr != null && prefabArr.Length > 0) {
                    var instance = Instantiate(Simulation.Random.RandomElement(prefabArr), parent.transform);
                    instance.transform.position = new Vector3(
                        point.position.x,
                        h,
                        point.position.y
                    );

                    instance.transform.rotation = Quaternion.Euler(0, Simulation.Random.Next(0, 360), 0);
                }
            }
        }
    }
}
