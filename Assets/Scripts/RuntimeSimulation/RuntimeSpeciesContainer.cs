using System;

using ProceduralVegetation;

using UnityEngine;

using static ProceduralVegetation.Simulation;

public abstract class RuntimeSpeciesContainer : MonoBehaviour {
    public abstract TreeSpeciesDescriptor GetDescriptor();
    public abstract Vector2[] GetInitialPoints(Bounds landscapeBounds);
    public abstract void HandlePointView(SimulationPointView point, Vector3 instancePosition);
    public abstract void Flush();
}
