using System;
using System.Linq;
using System.Reflection;

using ProceduralVegetation.Utilities;

using Sirenix.Utilities;

using UnityEngine;

using XNode;

namespace ProceduralVegetation.Editor.Nodes {
    public abstract class EditorNode : Node, IResetable {
        enum State {
            NotCalculated,
            InProgress,
            Done,
        }

        private State state = State.NotCalculated;

        public virtual void Reset() {
            state = State.NotCalculated;
        }

        public override object GetValue(NodePort port) {
            var fieldInfo = GetType()
                .GetField(port.fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfo == null) return null;
            return fieldInfo.GetValue(this);
        }

        // Reduce boilerplate do not
        public void GraphCalc() {
            Debug.Log($"{GetType()} {Inputs.Select(port => port.node).FilterCast<Node, EditorNode>().Count()}");

            if (state == State.InProgress) {    // цикл
                throw new OverflowException("cyclic graph");
            }
            if (state == State.Done) return;    // уже посчитано

            state = State.InProgress;


            Inputs
                .Select(port => port.node)
                .FilterCast<Node, EditorNode>()
                .ForEach(node => node.GraphCalc());

            Evaluate();

            state = State.Done;
        }

        public abstract void Evaluate();
    }
}
