using UnityEngine;

namespace Poi
{
    [CreateAssetMenu(menuName = "Poi/Paper Settings", fileName = "PoiPaperSettings")]
    public sealed class PoiPaperSettings : ScriptableObject
    {
        [Header("Durability")]
        [Min(0.01f)] public float breakThreshold = 1f;
        [Min(0f)] public float damageMultiplier = 1f;
        [Range(0.05f, 1f)] public float fullyWetStrengthMultiplier = 0.3f;
        [Min(0.1f)] public float damageFalloffPower = 1.5f;

        [Header("Wetting")]
        [Min(0f)] public float wettingRate = 0.75f;
        [Min(0f)] public float wetnessDiffusionRate = 0.9f;
        [Min(0f)] public float dryingRate = 0.025f;
        [Range(0.02f, 0.5f)] public float wetnessUpdateInterval = 0.1f;

        [Header("Detached Fragments")]
        [Tooltip("Seconds a detached paper fragment remains under normal Rigidbody physics before dissolving.")]
        [Min(0f)] public float fragmentLifetime = 0.9f;
        [Tooltip("Seconds used by the noise dissolve before the fragment is destroyed.")]
        [Min(0.05f)] public float fragmentDissolveDuration = 0.75f;

        [Header("Water Damage")]
        [Min(0f)] public float minimumWaterDamageSpeed = 0.35f;
        [Min(0f)] public float waterDamageMultiplier = 0.7f;
        [Min(0f)] public float waterEntryMultiplier = 0.35f;
        [Min(0f)] public float maximumWaterDamagePerSample = 0.18f;
    }
}
