using System.Collections.Generic;
using UnityEngine;

namespace Poi
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PoiWaterVolume : MonoBehaviour
    {
        private static readonly List<PoiWaterVolume> activeVolumes = new List<PoiWaterVolume>(4);
        [SerializeField] private Vector3 waterVelocity;
        [SerializeField] private PoiWaterSurfaceVisual surfaceVisual;
        [SerializeField] private bool showWaterVolume = true;
        private BoxCollider volume;

        public static IReadOnlyList<PoiWaterVolume> ActiveVolumes => activeVolumes;
        public Vector3 WaterVelocity { get => waterVelocity; set => waterVelocity = value; }
        public PoiWaterSurfaceVisual SurfaceVisual { get => surfaceVisual; set => surfaceVisual = value; }

        private void Awake() => Cache();
        private void OnEnable() { if (!activeVolumes.Contains(this)) activeVolumes.Add(this); }
        private void OnDisable() => activeVolumes.Remove(this);
        private void OnValidate() { Cache(); volume.isTrigger = true; }

        public bool Contains(Vector3 worldPosition)
        {
            Cache();
            Vector3 local = transform.InverseTransformPoint(worldPosition) - volume.center;
            Vector3 half = volume.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        public float GetDepth(Vector3 worldPosition)
        {
            if (!Contains(worldPosition)) return 0f;
            Cache();
            Vector3 local = transform.InverseTransformPoint(worldPosition) - volume.center;
            float localDepth = volume.size.y * 0.5f - local.y;
            return Mathf.Max(0f, transform.TransformVector(Vector3.up * localDepth).magnitude);
        }

        public Vector3 GetWaterVelocity(Vector3 worldPosition) => Contains(worldPosition) ? waterVelocity : Vector3.zero;

        private void Cache()
        {
            if (volume == null) volume = GetComponent<BoxCollider>();
            if (volume != null) volume.isTrigger = true;
        }

        private void OnDrawGizmos()
        {
            if (!showWaterVolume) return;
            Cache();
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.05f, 0.55f, 1f, 0.18f);
            Gizmos.DrawCube(volume.center, volume.size);
            Gizmos.color = new Color(0.15f, 0.8f, 1f, 0.85f);
            Gizmos.DrawWireCube(volume.center, volume.size);
            Vector3 top = volume.center + Vector3.up * volume.size.y * 0.5f;
            Gizmos.DrawLine(top - Vector3.right * volume.size.x * 0.5f, top + Vector3.right * volume.size.x * 0.5f);
            Gizmos.matrix = old;
        }
    }
}
