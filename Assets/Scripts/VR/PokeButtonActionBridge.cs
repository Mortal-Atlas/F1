using UnityEngine;

namespace F1AR.VR
{
    /// <summary>
    /// Event receiver bridge designed to link Meta XR Interaction SDK PokeInteractable events to ExplodedViewManager functions.
    /// </summary>
    public class PokeButtonActionBridge : MonoBehaviour
    {
        [Header("Target Manager")]
        [SerializeField] private ExplodedView.ExplodedViewManager explodedViewManager;

        public enum PokeActionType
        {
            ToggleExplode,
            Explode,
            Assemble,
            ToggleSpin,
            SetSpinActive,
            SetSpinInactive
        }

        [Header("Action Configuration")]
        [SerializeField] private PokeActionType actionType = PokeActionType.ToggleExplode;

        private void Start()
        {
            if (explodedViewManager == null)
            {
                explodedViewManager = FindFirstObjectByType<ExplodedView.ExplodedViewManager>();
            }
        }

        /// <summary>
        /// Call this method from Meta XR InteractableUnityEventWrapper / PointableUnityEventWrapper WhenSelect() event.
        /// </summary>
        public void OnPokeButtonPressed()
        {
            if (explodedViewManager == null)
            {
                Debug.LogWarning("[PokeButtonActionBridge] ExplodedViewManager reference is missing!", this);
                return;
            }

            switch (actionType)
            {
                case PokeActionType.ToggleExplode:
                    explodedViewManager.ToggleExplode();
                    break;
                case PokeActionType.Explode:
                    explodedViewManager.Explode();
                    break;
                case PokeActionType.Assemble:
                    explodedViewManager.Assemble();
                    break;
                case PokeActionType.ToggleSpin:
                    explodedViewManager.ToggleSpin();
                    break;
                case PokeActionType.SetSpinActive:
                    explodedViewManager.SetSpinning(true);
                    break;
                case PokeActionType.SetSpinInactive:
                    explodedViewManager.SetSpinning(false);
                    break;
            }
        }
    }
}
