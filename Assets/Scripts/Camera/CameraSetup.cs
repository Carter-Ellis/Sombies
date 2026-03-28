using UnityEngine;
using Unity.Cinemachine;

public class CameraSetup : MonoBehaviour
{
    private void Start()
    {
        CinemachineCamera vcam = Object.FindAnyObjectByType<CinemachineCamera>();
        if (vcam != null)
        {
            vcam.Follow = transform;

        }
    }
}