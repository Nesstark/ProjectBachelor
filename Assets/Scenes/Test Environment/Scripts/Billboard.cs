using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Camera mainCam;

    private void Awake()
    {
        mainCam = Camera.main;
    }

    private void LateUpdate()
    {
        // Fully face the camera by copying its exact rotation
        transform.rotation = mainCam.transform.rotation;
    }
}
