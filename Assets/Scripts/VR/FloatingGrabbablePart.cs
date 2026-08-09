using System.Collections;
using UnityEngine;

namespace F1AR.VR
{
    /// <summary>
    /// Component attached to individual CAD parts to make them grabbable in VR.
    /// When released, the part floats in place in 3D space.
    /// When B button / Reset is triggered, it smoothly snaps back into its assembled position on the F1 car.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FloatingGrabbablePart : MonoBehaviour
    {
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private Vector3 _initialLocalScale;
        private Rigidbody _rb;
        private bool _isGrabbed = false;
        private bool _hasUserCustomOffset = false;
        private Vector3 _userCustomLocalOffset = Vector3.zero;
        private Coroutine _resetCoroutine;

        public bool IsGrabbed => _isGrabbed;
        public bool HasUserCustomOffset => _hasUserCustomOffset;
        public Vector3 UserCustomLocalOffset => _userCustomLocalOffset;

        private void Awake()
        {
            _initialLocalPosition = transform.localPosition;
            _initialLocalRotation = transform.localRotation;
            _initialLocalScale = transform.localScale;

            _rb = GetComponent<Rigidbody>();
            if (_rb != null)
            {
                _rb.isKinematic = true;
                _rb.useGravity = false;
            }

            // Ensure collider exists for ray / hand grab
            Collider col = GetComponent<Collider>();
            if (col == null)
            {
                MeshFilter mf = GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    MeshCollider mc = gameObject.AddComponent<MeshCollider>();
                    mc.convex = true;
                }
                else
                {
                    gameObject.AddComponent<BoxCollider>();
                }
            }
        }

        /// <summary>
        /// Called when VR hand / controller grabs this part.
        /// </summary>
        public void OnGrabbed()
        {
            _isGrabbed = true;
            if (_resetCoroutine != null) StopCoroutine(_resetCoroutine);
        }

        /// <summary>
        /// Called when VR hand / controller releases this part. It floats in place!
        /// </summary>
        public void OnReleased()
        {
            _isGrabbed = false;
            _hasUserCustomOffset = true;
            
            // Record new local offset relative to original position so it stays floating right where user left it
            _userCustomLocalOffset = transform.localPosition - _initialLocalPosition;

            if (_rb != null)
            {
                _rb.isKinematic = true;
                _rb.useGravity = false;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            Debug.Log($"[FloatingGrabbablePart] Released '{gameObject.name}'. Floating in place at offset {_userCustomLocalOffset}.");
        }

        /// <summary>
        /// Smoothly snaps back to its original assembled position on the F1 car when B button is pressed.
        /// </summary>
        public void SnapBackToAssembled(float duration = 0.8f)
        {
            _isGrabbed = false;
            _hasUserCustomOffset = false;
            _userCustomLocalOffset = Vector3.zero;

            if (_resetCoroutine != null) StopCoroutine(_resetCoroutine);
            if (gameObject.activeInHierarchy)
            {
                _resetCoroutine = StartCoroutine(SnapBackRoutine(duration));
            }
            else
            {
                transform.localPosition = _initialLocalPosition;
                transform.localRotation = _initialLocalRotation;
                transform.localScale = _initialLocalScale;
            }
        }

        private IEnumerator SnapBackRoutine(float duration)
        {
            Vector3 startPos = transform.localPosition;
            Quaternion startRot = transform.localRotation;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

                transform.localPosition = Vector3.Lerp(startPos, _initialLocalPosition, t);
                transform.localRotation = Quaternion.Slerp(startRot, _initialLocalRotation, t);
                yield return null;
            }

            transform.localPosition = _initialLocalPosition;
            transform.localRotation = _initialLocalRotation;
            transform.localScale = _initialLocalScale;
            _resetCoroutine = null;
        }
    }
}
