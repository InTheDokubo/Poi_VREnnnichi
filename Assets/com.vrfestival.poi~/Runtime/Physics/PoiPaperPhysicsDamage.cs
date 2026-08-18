using System.Collections.Generic;
using UnityEngine;

namespace Poi
{
    /// <summary>Converts Unity contact physics into generic PoiDamageRequest values.</summary>
    [DisallowMultipleComponent]
    public sealed class PoiPaperPhysicsDamage : MonoBehaviour
    {
        [Header("Impact")]
        [SerializeField, Min(0f)] private float impactDamageMultiplier = 35f;
        [SerializeField, Min(0f)] private float minimumImpactImpulse = 0.004f;
        [SerializeField, Min(0f)] private float maximumImpactDamage = 2.5f;
        [Header("Continuous Load")]
        [SerializeField, Min(0f)] private float loadDamageMultiplier = 0.45f;
        [SerializeField, Min(0f)] private float continuousLoadThreshold = 0.03f;
        [SerializeField, Min(0.02f)] private float loadSampleInterval = 0.1f;
        [SerializeField, Range(0f, 2f)] private float accelerationInfluence = 1f;
        [SerializeField, Min(0f)] private float maximumContinuousDamagePerSample = 0.12f;
        [Header("Contact")]
        [SerializeField, Min(0.001f)] private float baseContactRadius = 0.006f;
        [SerializeField, Min(0.001f)] private float maximumContactRadius = 0.018f;
        [Header("Debug")]
        [SerializeField] private bool showDebug = true;
        [SerializeField] private bool showDebugValues;

        [SerializeField] private string lastContactObject;
        [SerializeField] private float lastObjectMass;
        [SerializeField] private float lastImpact;
        [SerializeField] private float lastContinuousLoad;
        [SerializeField] private float lastGeneratedDamage;
        [SerializeField] private float lastGeneratedRadius;

        private PoiPaperSurface surface;
        private PoiPaperDamageSystem damageSystem;
        private Rigidbody poiBody;
        private Vector3 previousPoiVelocity;
        private Vector3 poiAcceleration;
        private readonly Dictionary<int, float> nextLoadSampleByBody = new Dictionary<int, float>(8);
        private Vector3 lastPoint;
        private Vector3 lastNormal;
        private Vector3 lastDirection;

        public void Configure(PoiPaperSurface paperSurface, PoiPaperDamageSystem paperDamageSystem)
        {
            surface = paperSurface;
            damageSystem = paperDamageSystem;
        }

        public float CalculateImpactDamage(float impulse, int contactCount)
        {
            float distributedImpulse = Mathf.Max(0f, impulse) / Mathf.Sqrt(Mathf.Max(1, contactCount));
            if (distributedImpulse < minimumImpactImpulse) return 0f;
            return Mathf.Min(maximumImpactDamage, (distributedImpulse - minimumImpactImpulse) * impactDamageMultiplier);
        }

        public float CalculateContinuousDamage(float mass, Vector3 gravity, Vector3 paperAcceleration, Vector3 paperNormal, int contactCount, float deltaTime)
        {
            Vector3 apparentAcceleration = gravity - paperAcceleration * accelerationInfluence;
            float normalAcceleration = Mathf.Abs(Vector3.Dot(apparentAcceleration, paperNormal.normalized));
            float loadForce = Mathf.Max(0f, mass) * normalAcceleration;
            float loadAboveThreshold = Mathf.Max(0f, loadForce - continuousLoadThreshold);
            float areaDistribution = 1f / Mathf.Sqrt(Mathf.Max(1, contactCount));
            float damage = loadAboveThreshold * loadDamageMultiplier * Mathf.Max(0f, deltaTime) * areaDistribution;
            return Mathf.Min(maximumContinuousDamagePerSample, damage);
        }

        private void Awake()
        {
            if (surface == null) surface = GetComponentInChildren<PoiPaperSurface>();
            if (damageSystem == null) damageSystem = GetComponentInChildren<PoiPaperDamageSystem>();
            poiBody = GetComponentInParent<Rigidbody>();
            if (poiBody != null) previousPoiVelocity = poiBody.linearVelocity;
        }

        private void FixedUpdate()
        {
            if (poiBody == null) return;
            float deltaTime = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            Vector3 velocity = poiBody.linearVelocity;
            poiAcceleration = (velocity - previousPoiVelocity) / deltaTime;
            previousPoiVelocity = velocity;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!TryGetPaperContact(collision, out ContactSummary contact)) return;
            Rigidbody otherBody = collision.rigidbody;
            float mass = otherBody != null ? otherBody.mass : 0f;
            float impulse = collision.impulse.magnitude;
            if (impulse <= 0.00001f)
            {
                float normalSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.Normal));
                impulse = mass * normalSpeed;
            }

            float damage = CalculateImpactDamage(impulse, contact.Count);
            if (damage <= 0f) return;
            float radius = EstimateContactRadius(contact.Count);
            Vector3 direction = ProjectDirectionToPaper(collision.relativeVelocity);
            damageSystem.ApplyDamage(new PoiDamageRequest(contact.Point, direction, damage, radius, 0));
            StoreDebug(collision, contact, mass, impulse, 0f, damage, radius, direction);
        }

        private void OnCollisionStay(Collision collision)
        {
            Rigidbody otherBody = collision.rigidbody;
            if (otherBody == null) return;
            int bodyId = otherBody.GetInstanceID();
            if (nextLoadSampleByBody.TryGetValue(bodyId, out float nextTime) && Time.time < nextTime) return;
            nextLoadSampleByBody[bodyId] = Time.time + loadSampleInterval;
            if (!TryGetPaperContact(collision, out ContactSummary contact)) return;

            Vector3 paperNormal = surface.transform.forward.normalized;
            float loadForce = otherBody.mass * Mathf.Abs(Vector3.Dot(Physics.gravity - poiAcceleration * accelerationInfluence, paperNormal));
            float damage = CalculateContinuousDamage(otherBody.mass, Physics.gravity, poiAcceleration, paperNormal, contact.Count, loadSampleInterval);
            if (damage <= 0f) return;
            float radius = EstimateContactRadius(contact.Count);
            Vector3 relativePlanarVelocity = ProjectDirectionToPaper(collision.relativeVelocity);
            damageSystem.ApplyDamage(new PoiDamageRequest(contact.Point, relativePlanarVelocity, damage, radius, 0));
            StoreDebug(collision, contact, otherBody.mass, 0f, loadForce, damage, radius, relativePlanarVelocity);
        }

        private void OnCollisionExit(Collision collision)
        {
            Rigidbody otherBody = collision.rigidbody;
            if (otherBody != null) nextLoadSampleByBody.Remove(otherBody.GetInstanceID());
        }

        private bool TryGetPaperContact(Collision collision, out ContactSummary summary)
        {
            Vector3 pointSum = Vector3.zero;
            Vector3 normalSum = Vector3.zero;
            int validCount = 0;
            int count = collision.contactCount;
            for (int i = 0; i < count; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                Transform colliderTransform = contact.thisCollider != null ? contact.thisCollider.transform : null;
                if (colliderTransform == null || (colliderTransform != surface.transform && !colliderTransform.IsChildOf(surface.transform)))
                    continue;
                if (!surface.IsInsidePaper(contact.point)) continue;
                pointSum += contact.point;
                normalSum += contact.normal;
                validCount++;
            }
            if (validCount == 0)
            {
                summary = default;
                return false;
            }
            summary = new ContactSummary
            {
                Point = pointSum / validCount,
                Normal = normalSum.sqrMagnitude > 0.0001f ? normalSum.normalized : surface.transform.forward,
                Count = validCount
            };
            return true;
        }

        private Vector3 ProjectDirectionToPaper(Vector3 worldDirection)
        {
            return Vector3.ProjectOnPlane(worldDirection, surface.transform.forward);
        }

        private float EstimateContactRadius(int contactCount)
        {
            return Mathf.Min(maximumContactRadius, baseContactRadius * Mathf.Sqrt(Mathf.Max(1, contactCount)));
        }

        private void StoreDebug(Collision collision, ContactSummary contact, float mass, float impact, float load, float damage, float radius, Vector3 direction)
        {
            if (!showDebugValues && !showDebug) return;
            lastContactObject = collision.gameObject.name;
            lastObjectMass = mass;
            lastImpact = impact;
            lastContinuousLoad = load;
            lastGeneratedDamage = damage;
            lastGeneratedRadius = radius;
            lastPoint = contact.Point;
            lastNormal = contact.Normal;
            lastDirection = direction;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebug || lastGeneratedRadius <= 0f) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lastPoint, lastGeneratedRadius);
            Gizmos.DrawLine(lastPoint, lastPoint + lastNormal * 0.03f);
            Gizmos.color = Color.magenta;
            if (lastDirection.sqrMagnitude > 0.0001f)
                Gizmos.DrawLine(lastPoint, lastPoint + lastDirection.normalized * 0.04f);
        }

        private struct ContactSummary
        {
            public Vector3 Point;
            public Vector3 Normal;
            public int Count;
        }
    }
}
