using F1AR.ExplodedView;
using UnityEngine;
using UnityEngine.UI;

namespace F1AR.VR
{
    /// <summary>
    /// Dual Mode Controller enabling seamless swapping between PC Desktop CAD Viewer and Quest 3 AR Passthrough.
    /// Supports Orbit Mouse Controls, Keyboard Shortcuts, On-Screen Canvas UI, and AR Passthrough setup.
    /// </summary>
    public class DesktopARModeController : MonoBehaviour
    {
        public enum DisplayMode
        {
            DesktopPC,
            ARPassthrough
        }

        [Header("Mode Configuration")]
        [SerializeField] private DisplayMode currentMode = DisplayMode.DesktopPC;

        [Header("Target References")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private ExplodedViewManager explodedViewManager;
        [SerializeField] private GameObject studioEnvironment; // Optional Desktop Studio Floor/Lighting
        [SerializeField] private Canvas desktopUICanvas;

        [Header("Desktop Controls & Orbit Camera")]
        [SerializeField] private Transform cameraOrbitTarget;
        [SerializeField] private float orbitSensitivity = 3f;
        [SerializeField] private float zoomSensitivity = 2f;
        [SerializeField] private float minDistance = 0.5f;
        [SerializeField] private float maxDistance = 10f;

        private float _currentDistance = 2.5f;
        private Vector2 _orbitAngles = new Vector2(20f, 0f);

        private void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (explodedViewManager == null) explodedViewManager = FindFirstObjectByType<ExplodedViewManager>();

            if (cameraOrbitTarget == null && explodedViewManager != null)
            {
                cameraOrbitTarget = explodedViewManager.transform;
            }

            ApplyMode(currentMode);
        }

        private void Update()
        {
            // Keyboard Shortcuts for quick testing
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleDisplayMode();
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (explodedViewManager != null) explodedViewManager.ToggleExplode();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                if (explodedViewManager != null) explodedViewManager.ToggleSpin();
            }

            // Desktop Orbit Camera Handling
            if (currentMode == DisplayMode.DesktopPC && mainCamera != null && cameraOrbitTarget != null)
            {
                HandleDesktopOrbitCamera();
            }
        }

        public void ToggleDisplayMode()
        {
            currentMode = (currentMode == DisplayMode.DesktopPC) ? DisplayMode.ARPassthrough : DisplayMode.DesktopPC;
            ApplyMode(currentMode);
        }

        public void SetDisplayMode(DisplayMode mode)
        {
            currentMode = mode;
            ApplyMode(currentMode);
        }

        private void ApplyMode(DisplayMode mode)
        {
            if (mainCamera == null) return;

            if (mode == DisplayMode.ARPassthrough)
            {
                // AR Passthrough Settings: Transparent Camera Background
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = new Color(0, 0, 0, 0);

                if (studioEnvironment != null) studioEnvironment.SetActive(false);
                Debug.Log("[DesktopARModeController] Switched to Quest 3 AR Passthrough Mode.");
            }
            else
            {
                // PC Desktop Mode Settings: Skybox / Solid CAD Background
                mainCamera.clearFlags = CameraClearFlags.Skybox;
                if (studioEnvironment != null) studioEnvironment.SetActive(true);
                Debug.Log("[DesktopARModeController] Switched to PC Desktop CAD Viewer Mode.");
            }

            if (desktopUICanvas != null)
            {
                desktopUICanvas.gameObject.SetActive(mode == DisplayMode.DesktopPC);
            }
        }

        private void HandleDesktopOrbitCamera()
        {
            // Orbit with Right Mouse Button or Left Mouse Button drag
            if (Input.GetMouseButton(1) || Input.GetMouseButton(0))
            {
                _orbitAngles.x -= Input.GetAxis("Mouse Y") * orbitSensitivity * 10f;
                _orbitAngles.y += Input.GetAxis("Mouse X") * orbitSensitivity * 10f;
                _orbitAngles.x = Mathf.Clamp(_orbitAngles.x, -85f, 85f);
            }

            // Zoom with Mouse Scroll Wheel
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                _currentDistance -= scroll * zoomSensitivity;
                _currentDistance = Mathf.Clamp(_currentDistance, minDistance, maxDistance);
            }

            // Calculate Orbit Camera Position & Rotation
            Quaternion rotation = Quaternion.Euler(_orbitAngles.x, _orbitAngles.y, 0);
            Vector3 targetPos = cameraOrbitTarget != null ? cameraOrbitTarget.position : Vector3.zero;
            Vector3 position = targetPos - (rotation * Vector3.forward * _currentDistance);

            mainCamera.transform.rotation = rotation;
            mainCamera.transform.position = position;
        }

        private void OnGUI()
        {
            if (currentMode != DisplayMode.DesktopPC) return;

            // Simple On-Screen GUI Overlay for quick PC testing without canvas setup
            GUILayout.BeginArea(new Rect(20, 20, 300, 200), GUI.skin.box);
            
            GUILayout.Label("🏎️ F1 CAD Viewer Controls (PC / Desktop)");
            GUILayout.Space(5);

            if (explodedViewManager != null)
            {
                GUILayout.Label($"Explosion Progress: {(explodedViewManager.Progress * 100f):F0}%");
                float newProgress = GUILayout.HorizontalSlider(explodedViewManager.Progress, 0f, 1f);
                if (Mathf.Abs(newProgress - explodedViewManager.Progress) > 0.001f)
                {
                    explodedViewManager.SetExplosionProgress(newProgress);
                }

                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(explodedViewManager.IsExploded ? "Assemble [Space]" : "Explode [Space]"))
                {
                    explodedViewManager.ToggleExplode();
                }
                if (GUILayout.Button(explodedViewManager.IsSpinning ? "Stop Spin [R]" : "Spin [R]"))
                {
                    explodedViewManager.ToggleSpin();
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            if (GUILayout.Button("🔄 Swap Mode (Tab): AR / PC"))
            {
                ToggleDisplayMode();
            }

            GUILayout.EndArea();
        }
    }
}
