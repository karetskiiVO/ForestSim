using System;
using System.Linq;

using UnityEngine;

namespace ProceduralVegetation.Editor.Nodes {
    public abstract class BaseScatterNode : EditorNode {
        [Input(connectionType = ConnectionType.Override, typeConstraint = TypeConstraint.Strict)]
        public Bounds bbox;
        public long count;
        [Output]
        public Descriptor<Vector2[]> points = new();

        public override void Reset() {
            points = new();
            base.Reset();
        }

        public override void Evaluate() {
            var scatter = GetScatter();
            scatter.bbox = GetInputValue<Bounds>("bbox");
            var hint = scatter.countHint;
            var takeCount = hint.HasValue ? Math.Min(count, hint.Value) : count;

            points.descriptor = scatter.Take((int)takeCount).ToArray();
        }

        public abstract Scatter GetScatter();
    }
}
