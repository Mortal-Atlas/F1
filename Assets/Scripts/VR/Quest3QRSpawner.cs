using System.Collections.Generic;
using UnityEngine;
using Meta.XR.MRUtilityKit;

[RequireComponent(typeof(MRUK))]
public class Quest3QRFloatingModelSpawner : MonoBehaviour
{
    [Header("3D Model Settings")]
    [Tooltip("The 3D model prefab to instantiate over the QR code.")]
    public GameObject modelPrefab;

    [Tooltip("Optional: Only spawn the model if the QR code contains this exact text. Leave empty to spawn on ANY QR code.")]
    public string targetQRCodeData = "F1ARJacobZanderGino";

    [Header("Alignment Settings (For Tables)")]
    [Tooltip("Offset relative to the QR code. If flat on a table, the Z-axis (Forward) usually points UP towards the ceiling. Adjust X, Y, Z to center your car.")]
    public Vector3 localPositionOffset = new Vector3(0f, 0f, 0.15f);

    [Tooltip("Adjust the default rotation of the car so it faces forward on the platform.")]
    public Vector3 localRotationOffset = Vector3.zero;

    [Tooltip("Should the model continuously update its position and orientation every frame? Recommended for a moving Stewart platform.")]
    public bool trackMovement = true;

    [Header("Stewart Platform Smoothing")]
    [Tooltip("Enable interpolation to smooth out tiny high-frequency tracking jitter from the moving platform.")]
    public bool useSmoothing = true;

    [Tooltip("How fast the model snaps to the QR code's position (higher = faster, lower = smoother).")]
    public float positionSmoothSpeed = 25f;

    [Tooltip("How fast the model snaps to the QR code's rotation.")]
    public float rotationSmoothSpeed = 25f;

    // Internal tracking data structures
    private Dictionary<MRUKTrackable, GameObject> spawnedModels = new Dictionary<MRUKTrackable, GameObject>();
    private MRUK mrukInstance;

    private void OnEnable()
    {
        mrukInstance = GetComponent<MRUK>() ?? MRUK.Instance;
        if (mrukInstance != null && mrukInstance.SceneSettings != null)
        {
            var config = mrukInstance.SceneSettings.TrackerConfiguration;
            if (!config.QRCodeTrackingEnabled)
            {
                config.QRCodeTrackingEnabled = true;
                mrukInstance.SceneSettings.TrackerConfiguration = config;
            }

            mrukInstance.SceneSettings.TrackableAdded.AddListener(OnTrackableAdded);
            mrukInstance.SceneSettings.TrackableRemoved.AddListener(OnTrackableRemoved);
        }
    }

    private void OnDisable()
    {
        if (mrukInstance != null && mrukInstance.SceneSettings != null)
        {
            mrukInstance.SceneSettings.TrackableAdded.RemoveListener(OnTrackableAdded);
            mrukInstance.SceneSettings.TrackableRemoved.RemoveListener(OnTrackableRemoved);
        }
    }

    private void Update()
    {
        // If continuous tracking is enabled, update all spawned model positions every frame.
        // This is necessary because a Stewart platform moves rapidly and minor changes 
        // need to be tracked instantly rather than waiting for discrete event triggers.
        if (!trackMovement) return;

        foreach (var kvp in spawnedModels)
        {
            MRUKTrackable trackable = kvp.Key;
            GameObject model = kvp.Value;

            if (trackable != null && model != null)
            {
                // Calculate target position based on the QR code's local axes.
                // On a table, trackable.transform.forward (+Z) usually points straight up into the air.
                Vector3 targetPosition = trackable.transform.TransformPoint(localPositionOffset);
                Quaternion targetRotation = trackable.transform.rotation * Quaternion.Euler(localRotationOffset);

                if (useSmoothing)
                {
                    // Smoothly interpolate position and rotation to filter out camera noise / micro-jitter
                    model.transform.position = Vector3.Lerp(model.transform.position, targetPosition, Time.deltaTime * positionSmoothSpeed);
                    model.transform.rotation = Quaternion.Slerp(model.transform.rotation, targetRotation, Time.deltaTime * rotationSmoothSpeed);
                }
                else
                {
                    // Instant lock (direct mapping)
                    model.transform.position = targetPosition;
                    model.transform.rotation = targetRotation;
                }
            }
        }
    }

    private void OnTrackableAdded(MRUKTrackable trackable)
    {
        // Verify trackable type is indeed a QR code
        if (trackable.TrackableType == OVRAnchor.TrackableType.QRCode) 
        {
            string scannedData = trackable.MarkerPayloadString;

            // Check if string matches target (if specified)
            if (!string.IsNullOrEmpty(targetQRCodeData) && scannedData != targetQRCodeData)
            {
                Debug.Log($"[QR Spawner] Ignored QR code. Expected '{targetQRCodeData}', but scanned '{scannedData}'.");
                return;
            }

            // Ensure we don't spawn multiple instances for the same trackable
            if (!spawnedModels.ContainsKey(trackable))
            {
                // Calculate initial world position using the local offset
                Vector3 spawnPosition = trackable.transform.TransformPoint(localPositionOffset);
                Quaternion spawnRotation = trackable.transform.rotation * Quaternion.Euler(localRotationOffset);

                // Instantiate independently in world space so we can manage smooth updates in Update()
                GameObject spawnedModel = Instantiate(modelPrefab, spawnPosition, spawnRotation);
                
                spawnedModels.Add(trackable, spawnedModel);

                Debug.Log($"[QR Spawner] Spawned model flat on surface. Decoded Data: {scannedData}");
            }
        }
    }

    private void OnTrackableRemoved(MRUKTrackable trackable)
    {
        // Clean up when marker goes out of view or tracking is lost
        if (spawnedModels.TryGetValue(trackable, out GameObject model))
        {
            if (model != null)
            {
                Destroy(model);
            }
            
            spawnedModels.Remove(trackable);
            Debug.Log("[QR Spawner] Removed floating model because QR Code tracking was lost.");
        }
    }
}