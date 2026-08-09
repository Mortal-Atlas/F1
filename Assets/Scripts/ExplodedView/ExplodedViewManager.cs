using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace F1AR.ExplodedView
{
    /// <summary>
    /// Manages CAD Exploded View animations for an arbitrary array of vehicle models (F1 Car, DeLorean, Pod Racer, etc.).
    /// Prevents position drift when dragging the explosion progress slider by locking initial assembled transforms.
    /// </summary>
    public class ExplodedViewManager : MonoBehaviour
    {
        public enum ExplosionAxisMode
        {
            SmartCADRules,       // Strict CAD: Body/Seats UP (+Y), Spoilers -Z, Wheels/Pods ±X
            XAxisProportional,   // Pure lateral explosion along X axis
            Radial3D             // Spherical radial explosion outwards
        }

        [System.Serializable]
        public class VehicleEntry
        {
            [Tooltip("Label or vehicle name (e.g. F1 Car, DeLorean, Pod Racer).")]
            public string vehicleName = "Vehicle";

            [Tooltip("The root transform of the vehicle model (e.g. f4goto, delorean, pod).")]
            public Transform vehicleRoot;

            [Tooltip("The specific center point transform for this individual vehicle model.")]
            public Transform centerPoint;

            public VehicleEntry() { }

            public VehicleEntry(string name, Transform root, Transform center)
            {
                vehicleName = name;
                vehicleRoot = root;
                centerPoint = center;
            }
        }

        [Header("Vehicles & Specific Center Points Array")]
        [Tooltip("Add as many vehicle models and specific center points as you'd like!")]
        [SerializeField] private List<VehicleEntry> vehicleEntries = new List<VehicleEntry>();

        [Header("Explosion Control")]
        [Range(0.0f, 1.0f)]
        [SerializeField] private float explosionProgress = 0.0f;

        [Header("Explosion Settings")]
        [Tooltip("Explosion mode.")]
        [SerializeField] private ExplosionAxisMode axisMode = ExplosionAxisMode.SmartCADRules;

        [Tooltip("Macro distance multiplier (in meters). Controls height and spread of exploded parts.")]
        [Range(0.0f, 10.0f)]
        [SerializeField] private float maxExplosionDistance = 1.5f;

        [Tooltip("Sub-component separation offset along its assigned axis (prevents overlapping).")]
        [Range(0.0f, 3.0f)]
        [SerializeField] private float subComponentSpreadDistance = 0.3f;

        [Tooltip("Duration in seconds to transition between assembled and exploded state.")]
        [SerializeField] private float animationDuration = 1.0f;

        [Tooltip("Easing curve for explosion transition.")]
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Spin / Rotation Setup")]
        [Tooltip("Continuous rotation speed around Y-axis in degrees per second.")]
        [SerializeField] private float spinSpeed = 20f;

        [SerializeField] private bool isSpinning = false;

        [System.Serializable]
        private class PartData
        {
            public Transform transform;
            public Vector3 initialLocalPosition;
            public Vector3 initialLocalScale;
            public Quaternion initialLocalRotation;
            public Vector3 explosionDirectionLocal;
            public float distanceMultiplier;
            public ExplodedPart customPartOverride;
        }

        private readonly List<PartData> _registeredParts = new List<PartData>();
        private Coroutine _transitionCoroutine;
        private bool _isExploded = false;

        public float Progress
        {
            get => explosionProgress;
            set
            {
                explosionProgress = Mathf.Clamp01(value);
                ApplyExplosionProgress(explosionProgress);
            }
        }

        public void SetExplosionProgress(float progress)
        {
            Progress = progress;
        }

        public bool IsExploded => _isExploded;
        public bool IsSpinning => isSpinning;
        public List<VehicleEntry> Vehicles => vehicleEntries;

        private void Awake()
        {
            InitIfNeeded();
        }

        private void OnEnable()
        {
            InitIfNeeded();
        }

        private void OnValidate()
        {
            CacheParts();
            ApplyExplosionProgress(explosionProgress);
        }

        private void InitIfNeeded()
        {
            CacheParts();
            ApplyExplosionProgress(explosionProgress);
        }

        private void Update()
        {
            if (Application.isPlaying && isSpinning)
            {
                List<VehicleEntry> activeVehicles = GetActiveVehicleEntries();
                foreach (VehicleEntry entry in activeVehicles)
                {
                    if (entry == null || entry.vehicleRoot == null) continue;

                    Transform root = entry.vehicleRoot;
                    Vector3 pivotPoint = GetCenterWorldPosition(entry);
                    root.RotateAround(pivotPoint, Vector3.up, spinSpeed * Time.deltaTime);
                }
            }
        }

        private Vector3 GetCenterWorldPosition(VehicleEntry entry)
        {
            if (entry == null || entry.vehicleRoot == null) return transform.position;

            if (entry.centerPoint != null)
            {
                return entry.centerPoint.position;
            }

            Transform foundCenter = entry.vehicleRoot.Find("CenterPoint");
            if (foundCenter == null) foundCenter = entry.vehicleRoot.Find("Center");
            if (foundCenter == null) foundCenter = entry.vehicleRoot.Find("Pivot");

            return foundCenter != null ? foundCenter.position : entry.vehicleRoot.position;
        }

        private List<VehicleEntry> GetActiveVehicleEntries()
        {
            List<VehicleEntry> active = new List<VehicleEntry>();

            if (vehicleEntries != null && vehicleEntries.Count > 0)
            {
                foreach (VehicleEntry entry in vehicleEntries)
                {
                    if (entry != null && entry.vehicleRoot != null && !active.Contains(entry))
                    {
                        active.Add(entry);
                    }
                }
            }

            if (active.Count == 0)
            {
                string[] targets = { "Car", "f4goto", "delorean", "pod", "pod_complet_low", "anakin", "astonmartin" };
                foreach (string name in targets)
                {
                    GameObject go = GameObject.Find(name);
                    if (go != null)
                    {
                        Transform cp = go.transform.Find("CenterPoint");
                        if (cp == null) cp = go.transform.Find("Center");
                        active.Add(new VehicleEntry(go.name, go.transform, cp));
                    }
                }
            }

            return active;
        }

        /// <summary>
        /// Force clears the cached baseline transforms if models were modified while unexploded.
        /// </summary>
        [ContextMenu("Force Reset Baseline Assembled Positions")]
        public void ResetBaselinePositions()
        {
            _registeredParts.Clear();
            explosionProgress = 0.0f;
            CacheParts();
            ApplyExplosionProgress(0.0f);
            Debug.Log("[ExplodedViewManager] 🔄 Baseline assembled positions reset to 0 progress.");
        }

        /// <summary>
        /// Scans and records initial positions, scales, and strict single-axis directions for all components across all vehicle entries.
        /// Preserves original assembled initialLocalPosition to prevent compounding drift when adjusting sliders in Editor.
        /// </summary>
        public void CacheParts()
        {
            Dictionary<Transform, PartData> existingMap = new Dictionary<Transform, PartData>();
            foreach (PartData p in _registeredParts)
            {
                if (p != null && p.transform != null && !existingMap.ContainsKey(p.transform))
                {
                    existingMap[p.transform] = p;
                }
            }

            _registeredParts.Clear();

            List<VehicleEntry> entries = GetActiveVehicleEntries();
            if (entries.Count == 0) return;

            foreach (VehicleEntry entry in entries)
            {
                if (entry == null || entry.vehicleRoot == null) continue;

                Transform root = entry.vehicleRoot;
                Vector3 centerWorld = GetCenterWorldPosition(entry);
                Vector3 centerLocal = root.InverseTransformPoint(centerWorld);

                Renderer[] childRenderers = root.GetComponentsInChildren<Renderer>(true);
                int count = childRenderers.Length;

                for (int i = 0; i < count; i++)
                {
                    Renderer renderer = childRenderers[i];
                    Transform child = renderer.transform;

                    if (child == root || (entry.centerPoint != null && (child == entry.centerPoint || child.IsChildOf(entry.centerPoint))))
                        continue;

                    ExplodedPart customOverride = child.GetComponent<ExplodedPart>();

                    Vector3 meshCenterWorld = renderer.bounds.center;
                    Vector3 meshCenterLocal = root.InverseTransformPoint(meshCenterWorld);

                    float deltaX = meshCenterLocal.x - centerLocal.x;
                    float deltaZ = meshCenterLocal.z - centerLocal.z;

                    CalculateStrictAxisDirectionAndDistance(child, root, meshCenterWorld, centerWorld, deltaX, deltaZ, i, customOverride, out Vector3 dir, out float distMult);

                    Vector3 initialPos = child.localPosition;
                    Vector3 initialScale = child.localScale;
                    Quaternion initialRot = child.localRotation;

                    // DRIFT FIX: If we already have the original unexploded position cached, keep it!
                    if (existingMap.TryGetValue(child, out PartData existingData))
                    {
                        initialPos = existingData.initialLocalPosition;
                        initialScale = existingData.initialLocalScale;
                        initialRot = existingData.initialLocalRotation;
                    }

                    _registeredParts.Add(new PartData
                    {
                        transform = child,
                        initialLocalPosition = initialPos,
                        initialLocalScale = initialScale,
                        initialLocalRotation = initialRot,
                        explosionDirectionLocal = dir,
                        distanceMultiplier = distMult,
                        customPartOverride = customOverride
                    });
                }
            }
        }

        private void CalculateStrictAxisDirectionAndDistance(Transform child, Transform root, Vector3 meshCenterWorld, Vector3 centerWorld, float deltaX, float deltaZ, int index, ExplodedPart customOverride, out Vector3 direction, out float distanceMult)
        {
            distanceMult = maxExplosionDistance;

            if (customOverride != null)
            {
                Vector3 customDir = customOverride.GetDirection(new Vector3(Mathf.Sign(deltaX), 0, 0), (meshCenterWorld - centerWorld).normalized);
                if (customDir != Vector3.zero)
                {
                    direction = customDir;
                    distanceMult *= customOverride.distanceMultiplier;
                    return;
                }
            }

            if (axisMode == ExplosionAxisMode.XAxisProportional)
            {
                float xSign = (deltaX >= 0) ? 1.0f : -1.0f;
                direction = new Vector3(xSign, 0, 0);
                distanceMult *= (0.5f + Mathf.Abs(deltaX));
                return;
            }
            else if (axisMode == ExplosionAxisMode.Radial3D)
            {
                direction = root.InverseTransformDirection((meshCenterWorld - centerWorld).normalized);
                if (direction.sqrMagnitude < 0.001f) direction = Vector3.up;
                distanceMult *= (0.8f + (index % 5) * 0.2f);
                return;
            }

            // --- SmartCADRules Mode: STRICT SINGLE-AXIS MOVEMENT ---
            string nameLower = child.name.ToLower();
            Renderer rnd = child.GetComponent<Renderer>();
            string matNameLower = (rnd != null && rnd.sharedMaterial != null) ? rnd.sharedMaterial.name.ToLower() : "";

            // ==========================================
            // 1. F1 CAR SPECIFIC MESH RULES (HIGHEST PRIORITY)
            // ==========================================
            if (nameLower.Contains("formulachasis") || matNameLower.Contains("formulachasis"))
            {
                direction = Vector3.up;
                distanceMult *= 1.8f;
                return;
            }

            if (nameLower.Contains("koltuk") || matNameLower.Contains("koltuk"))
            {
                direction = Vector3.up;
                distanceMult *= 3.5f;
                return;
            }

            if (nameLower.Contains("direksiyon") || matNameLower.Contains("direksiyon"))
            {
                direction = Vector3.up;
                distanceMult *= 4.2f;
                return;
            }

            if (nameLower.Contains("sapoiel") || nameLower.Contains("spoiel") || nameLower.Contains("spoilertutucu") ||
                nameLower.Contains("spoilerustyarak") || nameLower.Contains("spolieryansahg") || nameLower.Contains("spoielryansol") ||
                matNameLower.Contains("sapoiel") || matNameLower.Contains("spoiel") || matNameLower.Contains("spoiler"))
            {
                direction = Vector3.back;
                if (nameLower.Contains("spoilerustyarak")) distanceMult *= 2.5f;
                else if (nameLower.Contains("sapoielralt7")) distanceMult *= 2.0f;
                else distanceMult *= 1.5f;
                return;
            }

            if (nameLower.Contains("wingalt") || nameLower.Contains("wingust") || matNameLower.Contains("wingalt") || matNameLower.Contains("wingust"))
            {
                direction = Vector3.forward;
                if (nameLower.Contains("wingust")) distanceMult *= 2.2f;
                else distanceMult *= 1.6f;
                return;
            }

            if (nameLower.Contains("front2wheels") || nameLower.Contains("jant") || matNameLower.Contains("jant") || matNameLower.Contains("front2wheels"))
            {
                float xSign = (deltaX >= 0) ? 1.0f : -1.0f;
                direction = new Vector3(xSign, 0, 0);
                distanceMult *= 2.5f;
                return;
            }

            if (nameLower.Contains("suspan") || matNameLower.Contains("suspan"))
            {
                float xSign = (deltaX >= 0) ? 1.0f : -1.0f;
                direction = new Vector3(xSign, 0, 0);
                distanceMult *= 1.8f;
                return;
            }

            // ==========================================
            // 2. POD RACER MESH RULES (anakins-pod-star-wars)
            // ==========================================
            if (nameLower.Contains("siege") || nameLower.Contains("cockpit"))
            {
                direction = Vector3.up;
                distanceMult *= 3.0f;
                return;
            }

            if (nameLower.Contains("tableau") || nameLower.Contains("leviers"))
            {
                direction = Vector3.up;
                distanceMult *= 2.5f;
                return;
            }

            if (nameLower.Contains("cache_cote") || nameLower.Contains("cache_dessus") || nameLower.Contains("elem_dessus"))
            {
                direction = Vector3.up;
                distanceMult *= 1.6f;
                return;
            }

            if (nameLower.Contains("cable") || nameLower.Contains("soutient_cable") || nameLower.Contains("connecteur"))
            {
                direction = Vector3.forward;
                distanceMult *= 1.8f;
                return;
            }

            if (nameLower.Contains("reacteur") || nameLower.Contains("nacelle") || nameLower.Contains("helice") || 
                nameLower.Contains("pale") || nameLower.Contains("tubes") || nameLower.Contains("flapes") || nameLower.Contains("prise_aire"))
            {
                float xSign = (deltaX >= 0) ? 1.0f : -1.0f;
                direction = new Vector3(xSign, 0, 0);
                distanceMult *= 2.6f;
                return;
            }

            // ==========================================
            // 3. DELOREAN MESH RULES (DMC12_BTTF2)
            // ==========================================
            if (nameLower.Contains("door") || nameLower.Contains("gullwing"))
            {
                direction = Vector3.up;
                distanceMult *= 3.0f;
                return;
            }

            if (nameLower.Contains("wheel") || nameLower.Contains("tire") || nameLower.Contains("rim"))
            {
                float xSign = (deltaX >= 0) ? 1.0f : -1.0f;
                direction = new Vector3(xSign, 0, 0);
                distanceMult *= 2.4f;
                return;
            }

            if (nameLower.Contains("hood") || nameLower.Contains("trunk") || nameLower.Contains("bumper"))
            {
                direction = (deltaZ >= 0) ? Vector3.forward : Vector3.back;
                distanceMult *= 1.8f;
                return;
            }

            // ==========================================
            // 4. GENERIC CAD FALLBACKS
            // ==========================================
            if (Mathf.Abs(deltaX) > Mathf.Abs(deltaZ))
            {
                float xSign = (deltaX >= 0) ? 1.0f : -1.0f;
                direction = new Vector3(xSign, 0, 0);
                distanceMult *= (1.0f + Mathf.Abs(deltaX));
            }
            else
            {
                direction = Vector3.up;
                distanceMult *= 1.5f;
            }
        }

        public void Explode()
        {
            AnimateToProgress(1.0f);
            _isExploded = true;
        }

        public void Assemble()
        {
            AnimateToProgress(0.0f);
            _isExploded = false;
        }

        public void ToggleExplode()
        {
            if (_isExploded) Assemble();
            else Explode();
        }

        public void ToggleSpin()
        {
            SetSpinning(!isSpinning);
        }

        public void SetSpinning(bool spin)
        {
            isSpinning = spin;
        }

        public void AnimateToProgress(float targetProgress)
        {
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
            }
            _transitionCoroutine = StartCoroutine(AnimateProgressRoutine(targetProgress));
        }

        private IEnumerator AnimateProgressRoutine(float targetProgress)
        {
            float startProgress = explosionProgress;
            float elapsed = 0.0f;

            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);
                float evaluatedT = transitionCurve != null ? transitionCurve.Evaluate(t) : t;

                explosionProgress = Mathf.Lerp(startProgress, targetProgress, evaluatedT);
                ApplyExplosionProgress(explosionProgress);

                yield return null;
            }

            explosionProgress = targetProgress;
            ApplyExplosionProgress(explosionProgress);
            _transitionCoroutine = null;
        }

        private void ApplyExplosionProgress(float progress)
        {
            if (_registeredParts == null) return;

            foreach (PartData part in _registeredParts)
            {
                if (part == null || part.transform == null) continue;

                Vector3 offset = part.explosionDirectionLocal * (part.distanceMultiplier * (1.0f + subComponentSpreadDistance * 0.5f) * progress);

                part.transform.localPosition = part.initialLocalPosition + offset;
                part.transform.localScale = part.initialLocalScale;
                part.transform.localRotation = part.initialLocalRotation;
            }
        }
    }
}
