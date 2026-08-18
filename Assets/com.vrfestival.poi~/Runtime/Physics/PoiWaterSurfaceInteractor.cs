using System.Collections.Generic;
using UnityEngine;

namespace Poi
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class PoiWaterSurfaceInteractor : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float minimumRippleSpeed = 0.03f;
        [SerializeField, Min(0f)] private float splashThreshold = 0.8f;
        [SerializeField, Min(0f)] private float strengthMultiplier = 0.8f;
        [SerializeField] private bool showDebug;

        private readonly Dictionary<PoiWaterVolume, float> previousSide = new Dictionary<PoiWaterVolume, float>(4);
        private Rigidbody body;
        private Vector3 previousPosition;
        private Vector3 lastCrossingPoint;
        private float lastStrength;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            previousPosition = body.worldCenterOfMass;
        }

        private void FixedUpdate()
        {
            Vector3 point = body.worldCenterOfMass;
            var volumes = PoiWaterVolume.ActiveVolumes;
            for (int i = 0; i < volumes.Count; i++)
            {
                PoiWaterVolume volume = volumes[i];
                if (volume == null || volume.SurfaceVisual == null) continue;
                BoxCollider box = volume.GetComponent<BoxCollider>();
                Vector3 local = volume.transform.InverseTransformPoint(point) - box.center;
                float side = local.y - box.size.y * 0.5f;
                if (!previousSide.TryGetValue(volume, out float oldSide))
                {
                    Vector3 oldLocal = volume.transform.InverseTransformPoint(previousPosition) - box.center;
                    oldSide = oldLocal.y - box.size.y * 0.5f;
                }
                bool crossed = (oldSide > 0f && side <= 0f) || (oldSide < 0f && side >= 0f);
                if (crossed)
                {
                    float denominator = oldSide - side;
                    float fraction = Mathf.Abs(denominator) > 0.00001f ? Mathf.Clamp01(oldSide / denominator) : 0.5f;
                    Vector3 crossing = Vector3.Lerp(previousPosition, point, fraction);
                    Vector3 crossingLocal = volume.transform.InverseTransformPoint(crossing) - box.center;
                    bool withinSurface = Mathf.Abs(crossingLocal.x) <= box.size.x * 0.5f && Mathf.Abs(crossingLocal.z) <= box.size.z * 0.5f;
                    float crossingSpeed = Mathf.Abs(Vector3.Dot(body.GetPointVelocity(crossing) - volume.GetWaterVelocity(crossing), volume.transform.up));
                    if (withinSurface && crossingSpeed >= minimumRippleSpeed)
                    {
                        float size = 0.05f;
                        Collider attached = GetComponentInChildren<Collider>();
                        if (attached != null) size = attached.bounds.extents.magnitude;
                        float strength = Mathf.Clamp01((crossingSpeed * strengthMultiplier) + size);
                        volume.SurfaceVisual.AddRipple(crossing, strength);
                        if (crossingSpeed >= splashThreshold)
                            volume.SurfaceVisual.AddSplash(crossing, Mathf.InverseLerp(splashThreshold, splashThreshold * 3f, crossingSpeed));
                        lastCrossingPoint = crossing;
                        lastStrength = strength;
                    }
                }
                previousSide[volume] = side;
            }
            previousPosition = point;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebug || lastStrength <= 0f) return;
            Gizmos.color = Color.Lerp(Color.cyan, Color.white, lastStrength);
            Gizmos.DrawWireSphere(lastCrossingPoint, 0.01f + lastStrength * 0.02f);
        }
    }
}
