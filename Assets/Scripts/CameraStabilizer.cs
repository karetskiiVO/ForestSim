using UnityEngine;

class CameraStabilizer : MonoBehaviour {
    Camera cam;

    void Start() {
        cam = GetComponent<Camera>();
    }

    void Update() {
        var left = Vector3.Cross(cam.transform.forward, Vector3.up);
        cam.transform.right = -left;
    }
}
