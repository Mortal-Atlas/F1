using F1AR.ExplodedView;
using UnityEngine;

namespace F1AR.VR
{
    /// <summary>
    /// VR Controller Input & Interaction Manager for Meta Quest 3 and Unity Editor.
    /// Supports both New Input System (XRController) and OVRInput / Legacy Input.
    /// Controls:
    /// - Left Controller X Button (Key X): Toggle CAD Exploded View
    /// - Left Controller Y Button (Key Y): Toggle Continuous Model Rotation
    /// - Right Controller A Button (Key A): Toggle Tire & Rim Spin Animation
    /// - Right Controller B Button (Key B): Assemble & Snap floating parts back together into unexploded model!
    /// </summary>
    [ExecuteAlways]
    public class VRControllerInputManager : MonoBehaviour
    {
        [Header("Target Manager References")]
        [SerializeField] private ExplodedViewManager explodedViewManager;
        [SerializeField] private WheelSpinAnimationController wheelSpinController;

        [Header("Live State & Inspector Toggles")]
        [Tooltip("Live Inspector toggle for Exploded CAD View.")]
        [SerializeField] private bool isExplodeActive = false;

        [Tooltip("Live Inspector toggle for Continuous Model Rotation.")]
        [SerializeField] private bool isModelSpinActive = false;

        [Tooltip("Live Inspector toggle for Tire & Rim Spin Animation.")]
        [SerializeField] private bool isWheelSpinActive = false;

        [Header("Keyboard Controls (Editor Fallbacks)")]
        [SerializeField] private KeyCode toggleExplodeKey = KeyCode.X;
        [SerializeField] private KeyCode toggleSpinKey = KeyCode.Y;
        [SerializeField] private KeyCode toggleWheelSpinKey = KeyCode.A;
        [SerializeField] private KeyCode snapBackAssembleKey = KeyCode.B;

        private void Awake()
        {
            AutoAssignReferences();
        }

        private void OnEnable()
        {
            AutoAssignReferences();
        }

        private void Reset()
        {
            AutoAssignReferences();
        }

        private void OnValidate()
        {
            AutoAssignReferences();
        }

        [ContextMenu("Auto Assign Scene References")]
        public void AutoAssignReferences()
        {
            if (explodedViewManager == null)
            {
                explodedViewManager = FindFirstObjectByType<ExplodedViewManager>();
            }

            if (wheelSpinController == null)
            {
                wheelSpinController = FindFirstObjectByType<WheelSpinAnimationController>();
            }
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                if (explodedViewManager != null)
                {
                    isExplodeActive = explodedViewManager.IsExploded;
                    isModelSpinActive = explodedViewManager.IsSpinning;
                }

                if (wheelSpinController != null)
                {
                    isWheelSpinActive = wheelSpinController.IsWheelSpinning;
                }

                CheckVRAndKeyInputs();
            }
        }

        private void CheckVRAndKeyInputs()
        {
            // --- 1. Left Controller X Button (or Key X) -> Toggle Explode ---
            bool xPressed = IsVRButtonOrKeyPressed("x", toggleExplodeKey, OVRInput.Button.One, OVRInput.Controller.LTouch, OVRInput.RawButton.X);
            if (xPressed)
            {
                ToggleExplode();
            }

            // --- 2. Left Controller Y Button (or Key Y) -> Toggle Model Rotation ---
            bool yPressed = IsVRButtonOrKeyPressed("y", toggleSpinKey, OVRInput.Button.Two, OVRInput.Controller.LTouch, OVRInput.RawButton.Y);
            if (yPressed)
            {
                ToggleModelRotation();
            }

            // --- 3. Right Controller A Button (or Key A) -> Toggle Tire & Rim Spin ---
            bool aPressed = IsVRButtonOrKeyPressed("a", toggleWheelSpinKey, OVRInput.Button.One, OVRInput.Controller.RTouch, OVRInput.RawButton.A);
            if (aPressed)
            {
                ToggleWheelSpinAnimation();
            }

            // --- 4. Right Controller B Button (or Key B) -> Snap All Parts Back Together ---
            bool bPressed = IsVRButtonOrKeyPressed("b", snapBackAssembleKey, OVRInput.Button.Two, OVRInput.Controller.RTouch, OVRInput.RawButton.B);
            if (bPressed)
            {
                SnapAllPartsBackToAssembled();
            }
        }

        private bool IsVRButtonOrKeyPressed(string actionName, KeyCode legacyKey, OVRInput.Button ovrButton, OVRInput.Controller ovrController, OVRInput.RawButton rawButton)
        {
            // A. Check OVRInput (Oculus SDK)
#if UNITY_ANDROID || UNITY_STANDALONE_WIN
            try
            {
                if (OVRInput.GetDown(ovrButton, ovrController) ||
                    OVRInput.GetDown(rawButton) ||
                    OVRInput.GetDown(ovrButton, OVRInput.Controller.Active) ||
                    OVRInput.GetDown(ovrButton, OVRInput.Controller.Touch))
                {
                    return true;
                }
            }
            catch { }
#endif

            // B. Check Unity New Input System (XRController & Keyboard)
#if ENABLE_INPUT_SYSTEM
            try
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard != null)
                {
                    switch (actionName.ToLower())
                    {
                        case "x": if (keyboard.xKey.wasPressedThisFrame) return true; break;
                        case "y": if (keyboard.yKey.wasPressedThisFrame) return true; break;
                        case "a": if (keyboard.aKey.wasPressedThisFrame) return true; break;
                        case "b": if (keyboard.bKey.wasPressedThisFrame) return true; break;
                    }
                }
            }
            catch { }
#endif

            // C. Check Legacy Input System
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                if (Input.GetKeyDown(legacyKey)) return true;
            }
            catch { }
#endif

            return false;
        }

        [ContextMenu("Toggle Explode View (Left X)")]
        public void ToggleExplode()
        {
            if (explodedViewManager != null)
            {
                explodedViewManager.ToggleExplode();
                isExplodeActive = explodedViewManager.IsExploded;
                Debug.Log($"[VRControllerInputManager] 💥 Left Controller X Button pressed -> Explode: {(isExplodeActive ? "ON" : "OFF")}");
            }
        }

        [ContextMenu("Toggle Model Rotation (Left Y)")]
        public void ToggleModelRotation()
        {
            if (explodedViewManager != null)
            {
                explodedViewManager.ToggleSpin();
                isModelSpinActive = explodedViewManager.IsSpinning;
                Debug.Log($"[VRControllerInputManager] 🔄 Left Controller Y Button pressed -> Model Rotation: {(isModelSpinActive ? "ON" : "OFF")}");
            }
        }

        [ContextMenu("Toggle Wheel Spin Animation (Right A)")]
        public void ToggleWheelSpinAnimation()
        {
            if (wheelSpinController != null)
            {
                wheelSpinController.ToggleWheelSpin();
                isWheelSpinActive = wheelSpinController.IsWheelSpinning;
            }
            else
            {
                WheelSpinAnimationController ws = FindFirstObjectByType<WheelSpinAnimationController>();
                if (ws != null)
                {
                    ws.ToggleWheelSpin();
                    isWheelSpinActive = ws.IsWheelSpinning;
                }
            }
            Debug.Log($"[VRControllerInputManager] 🏎️ Right Controller A Button pressed -> Wheel Spin Animation: {(isWheelSpinActive ? "ON" : "OFF")}");
        }

        [ContextMenu("Snap All Floating Parts Back Together (Right B)")]
        public void SnapAllPartsBackToAssembled()
        {
            if (explodedViewManager != null)
            {
                explodedViewManager.Assemble();
                isExplodeActive = false;
            }

            FloatingGrabbablePart[] grabbableParts = FindObjectsByType<FloatingGrabbablePart>(FindObjectsSortMode.None);
            foreach (FloatingGrabbablePart part in grabbableParts)
            {
                part.SnapBackToAssembled(0.8f);
            }

            Debug.Log($"[VRControllerInputManager] 🧩 Right Controller B Button pressed -> Snapped {grabbableParts.Length} floating parts back into unexploded model!");
        }
    }
}
