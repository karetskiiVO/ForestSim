
using System;

using UnityEngine;

namespace ProceduralVegetation.Core {
    class InteractivePowerDiagram {
        // тип int
        public struct ID { }
        // тип float
        public struct Weight { }

        // добавляем точку с весом
        public ID AddPoint(Vector2 point, Weight w) {
            throw new NotImplementedException();
        }

        // обнавляем вес точки
        public void UpdatePoint(ID id, Weight newWeight) {
            throw new NotImplementedException();
        }

        // удаляем точку с весом
        public void RemovePoint(ID id) {
            throw new NotImplementedException();
        }

        // проверяем валидна ли точка, то есть ее зона не пуста
        public bool IsValid(ID id) {
            throw new NotImplementedException();
        }

        // получаем соседей валидной точки
        public ID[] Neighbours(ID id) {
            throw new NotImplementedException();
        }
    }
}
