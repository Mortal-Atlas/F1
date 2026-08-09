using System.Collections.Generic;
using UnityEngine;

namespace F1AR.VR
{
    /// <summary>
    /// Programmatically lerp-rotates tire and rim 3D geometry transforms directly in C# without needing any Unity Animator Controller.
    /// Smoothly spins wheels around their local axle when toggled via A Button (or Key A) / Inspector Toggle.
    /// Supports both New Input System and Legacy Input System without throwing InvalidOperationException.
    /// </summary>
    public class WheelSpinAnimationController : MonoBehaviour
    {
        [Header("Wheel Spin Configuration")]
        [Tooltip("Master Inspector toggle to enable/disable tire & rim geometry rotation.")]
        [SerializeField] private bool isWheelSpinning = false;

        [Tooltip("Target rotation speed in degrees per second (e.g. 1080 deg/sec = 3 full rotations/sec).")]
        [SerializeField] private float targetSpinSpeed = 1080f;

        [Tooltip("Acceleration / Deceleration smooth lerp rate.")]
        [SerializeField] private float speedLerpRate = 5f;

        [Tooltip("Local axle axis around which wheel geometry rotates (default Pitch X-axis).")]
        [SerializeField] private Vector3 spinAxisLocal = Vector3.right;

        [Header("Target Wheel & Rim Geometry Transforms")]
        [Tooltip("List of wheel/rim transforms. Auto-cached if empty.")]
        [SerializeField] private List<Transform> wheelAndRimTransforms = new List<Transform>();

        [Header("Input Controls")]
        [Tooltip("Keyboard shortcut for Editor testing.")]
        [SerializeField] private KeyCode toggleWheelSpinKey = KeyCode.A;

        private float _currentSpinSpeed = 0f;

        public bool IsWheelSpinning
        {
            get => isWheelSpinning;
            set => isWheelSpinning = value;
        }

        private void Start()
        {
            CacheWheelAndRimTransforms();
        }

        private void OnEnable()
        {
            CacheWheelAndRimTransforms();
        }

        private void OnValidate()
        {
            CacheWheelAndRimTransforms();
        }

        [ContextMenu("Find Wheels & Rims Geometry In Scene")]
        public void CacheWheelAndRimTransforms()
        {
            if (wheelAndRimTransforms == null)
            {
                wheelAndRimTransforms = new List<Transform>();
            }

            GameObject carObj = GameObject.Find("Car");
            if (carObj == null) carObj = GameObject.Find("f4goto");

            if (carObj == null) return;

            Transform[] allChildren = carObj.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allChildren)
            {
                string lname = t.name.ToLower();
                if (lname.Contains("wheel") || lname.Contains("jant") || lname.Contains("tire") || lname.Contains("rim"))
                {
                    if (!wheelAndRimTransforms.Contains(t))
                    {
                        wheelAndRimTransforms.Add(t);
                    }
                }
            }
        }

        private void Update()
        {
            HandleInput();

            float desiredSpeed = isWheelSpinning ? targetSpinSpeed : 0f;
            _currentSpinSpeed = Mathf.Lerp(_currentSpinSpeed, desiredSpeed, Time.deltaTime * speedLerpRate);

            if (_currentSpinSpeed > 0.1f && wheelAndRimTransforms != null)
            {
                float deltaRotation = _currentSpinSpeed * Time.deltaTime;
                foreach (Transform wheel in wheelAndRimTransforms)
                {
                    if (wheel != null)
                    {
                        wheel.Rotate(spinAxisLocal, deltaRotation, Space.Self);
                    }
                }
            }
        }

        private void HandleInput()
        {
            bool aButtonPressed = false;

#if ENABLE_INPUT_SYSTEM
            try
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard != null && keyboard.aKey.wasPressedThisFrame)
                {
                    aButtonPressed = true;
                }
            }
            catch { }
#endif

            try
            {
                if (Input.GetKeyDown(toggleWheelSpinKey))
                {
                    aButtonPressed = true;
                }
            }
            catch { }

#if UNITY_ANDROID || UNITY_STANDALONE_WIN
            try
            {
                if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch) ||
                    OVRInput.GetDown(OVRInput.RawButton.A))
                {
                    aButtonPressed = true;
                }
            }
            catch { }
#endif

            if (aButtonPressed)
            {
                ToggleWheelSpin();
            }
        }

        [ContextMenu("Toggle Wheel Geometry Rotation")]
        public void ToggleWheelSpin()
        {
            isWheelSpinning = !isWheelSpinning;
            Debug.Log($"[WheelSpinAnimationController] 🏎️ Wheel & Rim Geometry Spin Toggled: {(isWheelSpinning ? "ON" : "OFF")}");
        }
    }
}
