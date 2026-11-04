using System;
using System.Collections.Generic;
using System.Linq;

using ProceduralVegetation;

using Sirenix.Utilities;

using UnityEngine;

class EnergyLoggerEvent : ProceduralVegetation.Simulation.Event {
    public override void Execute(ref Simulation.SimulationContext context) {
        var energyByType = new Dictionary<Type, float>();

        context.pointsView.ForEach(point => {
            if (point.energy > 0) {
                var type = point.descriptor.GetType();

                if (!energyByType.ContainsKey(type)) {
                    energyByType[type] = 0;
                }

                energyByType[type] += point.energy;
            }
        });

        var totalEnergy = energyByType.Values.Sum();
        var energyReport = energyByType.OrderBy(kvp => kvp.Key.Name).Aggregate("", (acc, kvp) => $"{acc} {kvp.Key}: {kvp.Value}");
        Debug.Log($"Total: {totalEnergy} Energy report: {energyReport}");
    }
}

class EnergyLoggerEventGenerator : ProceduralVegetation.Simulation.EventGenerator {
    public override Simulation.Event[] Generate(float currentTime) {
        return new Simulation.Event[] {
            new EnergyLoggerEvent(),
        };
    }
}
