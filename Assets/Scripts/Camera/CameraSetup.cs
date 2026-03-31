using UnityEngine;
using Unity.Cinemachine;
using Unity.Netcode;

public class CameraSetup : NetworkBehaviour
{
    private void Start()
    {
        if (!IsLocalPlayer) return;

        CinemachineCamera vcam = Object.FindAnyObjectByType<CinemachineCamera>();
        if (vcam != null)
        {
            vcam.Follow = transform;
            vcam.LookAt = transform;
        }
        else
        {
            Debug.LogWarning("CameraSetup: No CinemachineCamera found in the scene!");
        }
    }
}