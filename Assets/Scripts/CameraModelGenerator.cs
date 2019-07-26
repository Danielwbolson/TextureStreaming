using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraModelGenerator : MonoBehaviour
{
    int count = 4;
    GameObject[] cameras;
    Matrix4x4[] viewMatrices;

    // Start is called before the first frame update
    void Start() {

        // view mat is:
        // right_column, up_column, forward_column, position_column

        viewMatrices = new Matrix4x4[count];
        cameras = new GameObject[count];
        for (int i = 0; i < count; i++) {
            viewMatrices[i] = new Matrix4x4(
                new Vector4(1, 0, 0, i),
                new Vector4(0, 1, 0, i),
                new Vector4(0, 0, 1, i),
                new Vector4(0, 0, 0, 1));

            cameras[i] = new GameObject("Camera" + i.ToString());
            Camera cam = cameras[i].AddComponent<Camera>();
            cam.worldToCameraMatrix = viewMatrices[i];
        }
    }
}
