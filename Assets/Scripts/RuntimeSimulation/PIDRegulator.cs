using UnityEngine;

namespace ProceduralVegetation {
    public class PIDRegulator {
        public float Kp { get; set; }
        public float Ki { get; set; }
        public float Kd { get; set; }

        public float OutputMin { get; set; }
        public float OutputMax { get; set; }

        private float integral;
        private float lastError;
        private bool hasLastError;

        public PIDRegulator(float kp, float ki, float kd, float outputMin = float.NegativeInfinity, float outputMax = float.PositiveInfinity) {
            Kp = kp;
            Ki = ki;
            Kd = kd;
            OutputMin = outputMin;
            OutputMax = outputMax;
            Reset();
        }

        public void Reset() {
            integral = 0f;
            lastError = 0f;
            hasLastError = false;
        }

        public float Update(float setPoint, float measuredValue, float deltaTime = 1f) {
            float error = setPoint - measuredValue;
            integral += error * deltaTime;

            float derivative = 0f;
            if (hasLastError && deltaTime > 0f) {
                derivative = (error - lastError) / deltaTime;
            }

            lastError = error;
            hasLastError = true;

            float output = Kp * error + Ki * integral + Kd * derivative;
            return Mathf.Clamp(output, OutputMin, OutputMax);
        }
    }
}
