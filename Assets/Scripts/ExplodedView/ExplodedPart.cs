using UnityEngine;

namespace F1AR.ExplodedView
{
    /// <summary>
    /// Component attached to individual model parts to customize their explosion vector, direction preset, or magnitude.
    /// </summary>
    public class ExplodedPart : MonoBehaviour
    {
        public enum DirectionPreset
        {
            Auto_SmartRule,
            X_OutwardLeftRight,
            Y_Up,
            Y_Down,
            Z_Forward,
            Z_Backward,
            CustomVector
        }

        [Header("Custom Part Explosion Settings")]
        [Tooltip("Direction preset for this specific component.")]
        public DirectionPreset directionPreset = DirectionPreset.Auto_SmartRule;

        [Tooltip("Multiplier applied to the base explosion distance for this specific part.")]
        [Range(0.0f, 5.0f)]
        public float distanceMultiplier = 1.0f;

        [Tooltip("Custom explosion direction in local space of the parent model (only used if directionPreset is CustomVector).")]
        public Vector3 customDirectionLocal = Vector3.up;

        public Vector3 GetDirection(Vector3 defaultXDirection, Vector3 defaultRadialDirection)
        {
            switch (directionPreset)
            {
                case DirectionPreset.X_OutwardLeftRight:
                    return defaultXDirection;
                case DirectionPreset.Y_Up:
                    return Vector3.up;
                case DirectionPreset.Y_Down:
                    return Vector3.down;
                case DirectionPreset.Z_Forward:
                    return Vector3.forward;
                case DirectionPreset.Z_Backward:
                    return Vector3.back;
                case DirectionPreset.CustomVector:
                    return customDirectionLocal.normalized;
                case DirectionPreset.Auto_SmartRule:
                default:
                    return Vector3.zero; // Signals manager to use smart category rule
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 dir = customDirectionLocal;
            if (directionPreset == DirectionPreset.Y_Up) dir = Vector3.up;
            else if (directionPreset == DirectionPreset.Z_Backward) dir = Vector3.back;
            else if (directionPreset == DirectionPreset.Z_Forward) dir = Vector3.forward;

            Vector3 endPos = transform.position + transform.parent.TransformDirection(dir) * distanceMultiplier * 0.3f;
            Gizmos.DrawLine(transform.position, endPos);
            Gizmos.DrawSphere(endPos, 0.02f);
        }
    }
}
