using System;
using F1AR.ExplodedView;
using UnityEngine;

namespace F1AR.Telemetry
{
    /// <summary>
    /// Replicates 6DOF motion (Sway, Surge, Heave, Pitch, Roll, Yaw) onto a 3D model (e.g. F1 Car)
    /// Replicates 6DOF motion (Sway, Surge, Heave, Pitch, Roll, Yaw) onto a 3D model (e.g. F1 Car)
    /// relative to an Anchor Transform (e.g. Stewart Platform origin).
    /// Supports Spin Yaw Override: Continuous spinning takes priority over telemetry Yaw while all other 5 DOFs broadcast live!
    /// </summary>
    [ExecuteAlways]
    public class StewartPlatform6DOFReplicator : MonoBehaviour
    {
        [Header("Target & Anchor Configuration")]
        [Tooltip("The 3D Model transform to simulate 6DOF motion onto (e.g. F1 Car).")]
        [SerializeField] private Transform targetModel;

        [Tooltip("Anchor origin transform (e.g. CenterPoint or Stewart Platform origin).")]
        [SerializeField] private Transform platformAnchor;

        [Header("Data Source Configuration")]
        [Tooltip("Optional SimTools UDP Receiver. Auto-assigned if empty.")]
        [SerializeField] private SimToolsUDPReceiver udpReceiver;

        [Tooltip("Optional reference to PlatformController. Auto-assigned if empty.")]
        [SerializeField] private PlatformController platformController;

        [Tooltip("Optional reference to ExplodedViewManager to sync spin state.")]
        [SerializeField] private ExplodedViewManager explodedViewManager;

        [Header("Spin vs Yaw Telemetry Override")]
        [Tooltip("When continuous spin is active, spin rotation takes priority over telemetry Yaw. All other 5 DOFs (Sway, Surge, Heave, Pitch, Roll) continue broadcasting live!")]
        [SerializeField] private bool overrideYawWithSpin = true;

        [Tooltip("Continuous spin speed (degrees per second) used when overriding Yaw.")]
        [SerializeField] private float spinSpeed = 20f;

        [Tooltip("Master toggle for continuous spin.")]
        [SerializeField] private bool isSpinning = false;

        [Header("Manual 6DOF Control Sliders")]
        [SerializeField] private bool useManualSliders = false;
        [Range(-10f, 10f)] [SerializeField] private float manualSway = 0f;    // X translation (meters/units)
        [Range(-10f, 10f)] [SerializeField] private float manualSurge = 0f;   // Z translation
        [Range(-10f, 10f)] [SerializeField] private float manualHeave = 0f;   // Y translation
        [Range(-45f, 45f)] [SerializeField] private float manualPitch = 0f;   // X rotation (degrees)
        [Range(-45f, 45f)] [SerializeField] private float manualRoll = 0f;    // Z rotation
        [Range(-45f, 45f)] [SerializeField] private float manualYaw = 0f;     // Y rotation

        [Header("Gain & Scale Multipliers")]
        [Tooltip("Multiplier for Sway (X Translation).")]
        [SerializeField] private float swayGain = 0.01f;

        [Tooltip("Multiplier for Surge (Z Translation).")]
        [SerializeField] private float surgeGain = 0.01f;

        [Tooltip("Multiplier for Heave (Y Translation).")]
        [SerializeField] private float heaveGain = 0.01f;

        [Tooltip("Multiplier for Pitch (X Rotation).")]
        [SerializeField] private float pitchGain = 1.0f;

        [Tooltip("Multiplier for Roll (Z Rotation).")]
        [SerializeField] private float rollGain = 1.0f;

        [Tooltip("Multiplier for Yaw (Y Rotation).")]
        [SerializeField] private float yawGain = 1.0f;

        [Header("Motion Smoothing")]
        [Tooltip("Smooth lerp speed for motion replication. 0 = instant, 15 = smooth.")]
        [Range(1f, 30f)]
        [SerializeField] private float smoothSpeed = 15f;

        private Vector3 _initialLocalPos;
        private Quaternion _initialLocalRot;
        private float _continuousSpinYaw = 0f;

        public float CurrentSway { get; private set; }
        public float CurrentSurge { get; private set; }
        public float CurrentHeave { get; private set; }
        public float CurrentPitch { get; private set; }
        public float CurrentRoll { get; private set; }
        public float CurrentYaw { get; private set; }

        public bool OverrideYawWithSpin
        {
            get => overrideYawWithSpin;
            set => overrideYawWithSpin = value;
        }

        public bool IsSpinning
        {
            get => (explodedViewManager != null) ? explodedViewManager.IsSpinning : isSpinning;
            set
            {
                isSpinning = value;
                if (explodedViewManager != null) explodedViewManager.SetSpinning(value);
            }
        }

        private void Awake()
        {
            AutoAssignReferences();
            CacheInitialTransforms();
        }

        private void OnEnable()
        {
            AutoAssignReferences();
            CacheInitialTransforms();
        }

        private void Reset()
        {
            AutoAssignReferences();
        }

        private void OnValidate()
        {
            AutoAssignReferences();
        }

        private void CacheInitialTransforms()
        {
            if (targetModel != null)
            {
                _initialLocalPos = targetModel.localPosition;
                _initialLocalRot = targetModel.localRotation;
            }
        }

        [ContextMenu("Auto Assign Scene References")]
        public void AutoAssignReferences()
        {
            if (targetModel == null)
            {
                Transform foundModel = transform.Find("f4goto");
                if (foundModel == null) foundModel = transform.Find("Car");
                if (foundModel == null) foundModel = transform;
                targetModel = foundModel;
            }

            if (platformAnchor == null)
            {
                Transform foundAnchor = transform.Find("CenterPoint");
                if (foundAnchor != null) platformAnchor = foundAnchor;
            }

            if (udpReceiver == null)
            {
                udpReceiver = FindFirstObjectByType<SimToolsUDPReceiver>();
            }

            if (platformController == null)
            {
                platformController = FindFirstObjectByType<PlatformController>();
            }

            if (explodedViewManager == null)
            {
                explodedViewManager = FindFirstObjectByType<ExplodedViewManager>();
            }
        }

        private void Update()
        {
            FetchTelemetryValues();

            if (Application.isPlaying)
            {
                Apply6DOFMotion();
            }
        }

        private void FetchTelemetryValues()
        {
            if (useManualSliders)
            {
                CurrentSway = manualSway;
                CurrentSurge = manualSurge;
                CurrentHeave = manualHeave;
                CurrentPitch = manualPitch;
                CurrentRoll = manualRoll;
                CurrentYaw = manualYaw;
            }
            else if (udpReceiver != null)
            {
                CurrentSway = udpReceiver.Sway;
                CurrentSurge = udpReceiver.Surge;
                CurrentHeave = udpReceiver.Heave;
                CurrentPitch = udpReceiver.Pitch;
                CurrentRoll = udpReceiver.Roll;
                CurrentYaw = udpReceiver.Yaw;
            }
            else if (platformController != null)
            {
                CurrentSway = platformController.Sway;
                CurrentSurge = platformController.Surge;
                CurrentHeave = platformController.Heave;
                CurrentPitch = platformController.Pitch;
                CurrentRoll = platformController.Roll;
                CurrentYaw = platformController.Yaw;
            }
        }

        private void Apply6DOFMotion()
        {
            if (targetModel == null) return;

            // 1. Live Telemetry Translation (Sway=X, Heave=Y, Surge=Z)
            Vector3 targetTranslation = new Vector3(
                CurrentSway * swayGain,
                CurrentHeave * heaveGain,
                CurrentSurge * surgeGain
            );

            // 2. Check if Continuous Spin takes priority over Telemetry Yaw
            bool activeSpin = IsSpinning;
            float effectiveYaw = CurrentYaw * yawGain;

            if (activeSpin && overrideYawWithSpin)
            {
                // Accumulate spin rotation around Y axis
                _continuousSpinYaw += spinSpeed * Time.deltaTime;
                _continuousSpinYaw %= 360f;
                effectiveYaw = _continuousSpinYaw;
            }

            // 3. 6DOF Rotation Offset (Pitch=X from telemetry, Yaw=Y from spin/telemetry, Roll=Z from telemetry)
            Quaternion targetRotationOffset = Quaternion.Euler(
                CurrentPitch * pitchGain,
                effectiveYaw,
                CurrentRoll * rollGain
            );

            if (platformAnchor != null)
            {
                Vector3 desiredWorldPos = platformAnchor.TransformPoint(_initialLocalPos + targetTranslation);
                Quaternion desiredWorldRot = platformAnchor.rotation * _initialLocalRot * targetRotationOffset;

                targetModel.position = Vector3.Lerp(targetModel.position, desiredWorldPos, Time.deltaTime * smoothSpeed);
                targetModel.rotation = Quaternion.Slerp(targetModel.rotation, desiredWorldRot, Time.deltaTime * smoothSpeed);
            }
            else
            {
                Vector3 desiredLocalPos = _initialLocalPos + targetTranslation;
                Quaternion desiredLocalRot = _initialLocalRot * targetRotationOffset;

                targetModel.localPosition = Vector3.Lerp(targetModel.localPosition, desiredLocalPos, Time.deltaTime * smoothSpeed);
                targetModel.localRotation = Quaternion.Slerp(targetModel.localRotation, desiredLocalRot, Time.deltaTime * smoothSpeed);
            }
        }

        [ContextMenu("Toggle Override Yaw With Spin")]
        public void ToggleOverrideYawWithSpin()
        {
            overrideYawWithSpin = !overrideYawWithSpin;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying) return;

            GUILayout.BeginArea(new Rect(Screen.width - 320, 20, 300, 300), GUI.skin.box);
            GUILayout.Label("🎮 6DOF SimTools Telemetry Replicator");
            GUILayout.Space(5);

            overrideYawWithSpin = GUILayout.Toggle(overrideYawWithSpin, " Override Yaw With Spin");
            useManualSliders = GUILayout.Toggle(useManualSliders, " Use Manual Sliders");

            if (useManualSliders)
            {
                GUILayout.Label($"Sway (X): {manualSway:F2}");
                manualSway = GUILayout.HorizontalSlider(manualSway, -10f, 10f);

                GUILayout.Label($"Surge (Z): {manualSurge:F2}");
                manualSurge = GUILayout.HorizontalSlider(manualSurge, -10f, 10f);

                GUILayout.Label($"Heave (Y): {manualHeave:F2}");
                manualHeave = GUILayout.HorizontalSlider(manualHeave, -10f, 10f);

                GUILayout.Label($"Pitch (X-Rot): {manualPitch:F1}°");
                manualPitch = GUILayout.HorizontalSlider(manualPitch, -45f, 45f);

                GUILayout.Label($"Roll (Z-Rot): {manualRoll:F1}°");
                manualRoll = GUILayout.HorizontalSlider(manualRoll, -45f, 45f);
            }
            else
            {
                GUILayout.Label($"Sway (X):  {CurrentSway:F2}");
                GUILayout.Label($"Surge (Z): {CurrentSurge:F2}");
                GUILayout.Label($"Heave (Y): {CurrentHeave:F2}");
                GUILayout.Label($"Pitch (X): {CurrentPitch:F1}°");
                GUILayout.Label($"Roll (Z):  {CurrentRoll:F1}°");
                GUILayout.Label($"Yaw (Y):   {(overrideYawWithSpin && IsSpinning ? _continuousSpinYaw : CurrentYaw):F1}° {(overrideYawWithSpin && IsSpinning ? "[SPINNING]" : "")}");
            }

            GUILayout.EndArea();
        }
    }
}
