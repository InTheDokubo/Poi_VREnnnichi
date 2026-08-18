using UnityEngine;
using UnityEngine.Events;

namespace Poi
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class PoiGrabTarget : MonoBehaviour
    {
        [SerializeField] private Transform attachTransform;
        [SerializeField] private bool makeKinematicWhileHeld = true;
        [SerializeField] private UnityEvent onGrabbed;
        [SerializeField] private UnityEvent onReleased;

        private Rigidbody body;
        private Transform handTarget;
        private bool originalKinematic;
        private bool originalUseGravity;

        public Transform AttachTransform => attachTransform;
        public bool IsGrabbed => handTarget != null;
        public Transform HandTarget => handTarget;
        public Transform Attach { set => attachTransform = value; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (attachTransform == null) attachTransform = transform;
        }

        /// <summary>Package-independent entry point for a VR hand/interactor adapter.</summary>
        public void BeginGrab(Transform hand)
        {
            if (hand == null || IsGrabbed) return;
            body = body != null ? body : GetComponent<Rigidbody>();
            originalKinematic = body.isKinematic;
            originalUseGravity = body.useGravity;
            handTarget = hand;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.useGravity = false;
            if (makeKinematicWhileHeld) body.isKinematic = true;
            onGrabbed?.Invoke();
        }

        /// <summary>Releases the poi and transfers the tracked hand velocity.</summary>
        public void EndGrab(Vector3 linearVelocity, Vector3 angularVelocity)
        {
            if (!IsGrabbed) return;
            handTarget = null;
            body.isKinematic = originalKinematic;
            body.useGravity = originalUseGravity;
            if (!body.isKinematic)
            {
                body.linearVelocity = linearVelocity;
                body.angularVelocity = angularVelocity;
            }
            onReleased?.Invoke();
        }

        public void EndGrab() => EndGrab(Vector3.zero, Vector3.zero);

        private void FixedUpdate()
        {
            if (!IsGrabbed || attachTransform == null) return;
            Quaternion rootRotation = handTarget.rotation * Quaternion.Inverse(attachTransform.localRotation);
            Vector3 scaledLocalAttach = Vector3.Scale(attachTransform.localPosition, transform.lossyScale);
            Vector3 rootPosition = handTarget.position - rootRotation * scaledLocalAttach;
            body.MoveRotation(rootRotation);
            body.MovePosition(rootPosition);
        }

        private void OnDisable()
        {
            if (IsGrabbed) EndGrab();
        }
    }
}
