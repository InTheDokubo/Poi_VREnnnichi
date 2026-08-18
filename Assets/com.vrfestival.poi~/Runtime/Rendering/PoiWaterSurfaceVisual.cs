using UnityEngine;

namespace Poi
{
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PoiWaterSurfaceVisual : MonoBehaviour
    {
        [SerializeField] private PoiWaterVolume waterVolume;
        [SerializeField, Range(4, 40)] private int meshResolution = 20;
        [SerializeField, Range(1, 16)] private int maximumActiveRipples = 12;
        [SerializeField, Min(0.05f)] private float rippleLifetime = 1.4f;
        [SerializeField, Min(0.01f)] private float rippleSpeed = 0.12f;
        [SerializeField, Min(0.0005f)] private float rippleWidth = 0.003f;
        [SerializeField] private Material rippleMaterial;
        [SerializeField] private ParticleSystem splashParticles;
        [SerializeField] private bool showDebug;

        private Mesh surfaceMesh;
        private Mesh rippleMesh;
        private Ripple[] ripples;
        private int nextRipple;
        private BoxCollider volumeCollider;
        private MaterialPropertyBlock propertyBlock;

        public PoiWaterVolume WaterVolume { get => waterVolume; set => waterVolume = value; }
        public Material RippleMaterial { set => rippleMaterial = value; }
        public ParticleSystem SplashParticles { set => splashParticles = value; }

        private void OnEnable()
        {
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            Cache();
            BuildSurfaceMesh();
            if (Application.isPlaying) EnsureRipplePool();
            FollowVolume();
        }

        private void LateUpdate()
        {
            Cache();
            FollowVolume();
            if (!Application.isPlaying) return;
            UpdateRipples();
        }

        public void AddRipple(Vector3 worldPosition, float strength)
        {
            if (!Application.isPlaying || waterVolume == null) return;
            EnsureRipplePool();
            Ripple ripple = ripples[nextRipple];
            nextRipple = (nextRipple + 1) % ripples.Length;
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            local.y = 0.001f;
            ripple.Root.transform.localPosition = local;
            ripple.Root.transform.localRotation = Quaternion.identity;
            ripple.Strength = Mathf.Clamp01(strength);
            ripple.StartTime = Time.time;
            ripple.Root.SetActive(true);
        }

        public void AddSplash(Vector3 worldPosition, float strength)
        {
            if (splashParticles == null || strength <= 0f) return;
            splashParticles.transform.position = worldPosition;
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                velocity = transform.up * Mathf.Lerp(0.18f, 0.65f, Mathf.Clamp01(strength)),
                startSize = Mathf.Lerp(0.004f, 0.012f, Mathf.Clamp01(strength))
            };
            splashParticles.Emit(emit, Mathf.Clamp(Mathf.RoundToInt(2f + strength * 7f), 2, 9));
        }

        private void FollowVolume()
        {
            if (waterVolume == null || volumeCollider == null) return;
            Transform source = waterVolume.transform;
            transform.SetPositionAndRotation(
                source.TransformPoint(volumeCollider.center + Vector3.up * volumeCollider.size.y * 0.5f),
                source.rotation);
            Vector3 lossy = source.lossyScale;
            transform.localScale = new Vector3(
                Mathf.Abs(lossy.x * volumeCollider.size.x), 1f,
                Mathf.Abs(lossy.z * volumeCollider.size.z));
        }

        private void BuildSurfaceMesh()
        {
            if (surfaceMesh != null) return;
            int r = Mathf.Max(4, meshResolution);
            var vertices = new Vector3[(r + 1) * (r + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[r * r * 6];
            for (int z = 0; z <= r; z++)
            for (int x = 0; x <= r; x++)
            {
                int i = z * (r + 1) + x;
                float u = x / (float)r;
                float v = z / (float)r;
                vertices[i] = new Vector3(u - 0.5f, 0f, v - 0.5f);
                uv[i] = new Vector2(u, v);
            }
            int t = 0;
            for (int z = 0; z < r; z++)
            for (int x = 0; x < r; x++)
            {
                int a = z * (r + 1) + x;
                int b = a + 1;
                int c = a + r + 1;
                int d = c + 1;
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = d;
                triangles[t++] = a; triangles[t++] = d; triangles[t++] = b;
            }
            surfaceMesh = new Mesh { name = "PoiWaterSurface_Runtime", hideFlags = HideFlags.DontSave };
            surfaceMesh.vertices = vertices; surfaceMesh.uv = uv; surfaceMesh.triangles = triangles;
            surfaceMesh.RecalculateNormals(); surfaceMesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = surfaceMesh;
        }

        private void EnsureRipplePool()
        {
            if (ripples != null && ripples.Length == maximumActiveRipples) return;
            if (rippleMesh == null) rippleMesh = BuildRippleMesh(48);
            ripples = new Ripple[Mathf.Max(1, maximumActiveRipples)];
            for (int i = 0; i < ripples.Length; i++)
            {
                GameObject root = new GameObject("Ripple_" + i);
                root.transform.SetParent(transform, false);
                root.AddComponent<MeshFilter>().sharedMesh = rippleMesh;
                root.AddComponent<MeshRenderer>().sharedMaterial = rippleMaterial;
                root.SetActive(false);
                ripples[i] = new Ripple { Root = root };
            }
        }

        private void UpdateRipples()
        {
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            for (int i = 0; i < ripples.Length; i++)
            {
                Ripple ripple = ripples[i];
                if (!ripple.Root.activeSelf) continue;
                float age = Time.time - ripple.StartTime;
                if (age >= rippleLifetime) { ripple.Root.SetActive(false); continue; }
                float normalized = age / rippleLifetime;
                float radius = Mathf.Max(rippleWidth, age * rippleSpeed);
                ripple.Root.transform.localScale = new Vector3(radius / transform.lossyScale.x, 1f, radius / transform.lossyScale.z);
                Color color = new Color(0.65f, 0.95f, 1f, (1f - normalized) * ripple.Strength * 0.72f);
                ripple.Root.GetComponent<MeshRenderer>().GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_Color", color);
                ripple.Root.GetComponent<MeshRenderer>().SetPropertyBlock(propertyBlock);
            }
        }

        private static Mesh BuildRippleMesh(int segments)
        {
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            const float inner = 0.82f;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = direction * inner;
                vertices[i * 2 + 1] = direction;
                int next = (i + 1) % segments;
                int t = i * 6;
                triangles[t] = i * 2; triangles[t + 1] = next * 2; triangles[t + 2] = next * 2 + 1;
                triangles[t + 3] = i * 2; triangles[t + 4] = next * 2 + 1; triangles[t + 5] = i * 2 + 1;
            }
            Mesh mesh = new Mesh { name = "PoiWaterRipple_Shared", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private void Cache()
        {
            if (waterVolume == null) waterVolume = GetComponentInParent<PoiWaterVolume>();
            if (waterVolume != null) volumeCollider = waterVolume.GetComponent<BoxCollider>();
        }

        private void OnDestroy()
        {
            if (surfaceMesh != null) { if (Application.isPlaying) Destroy(surfaceMesh); else DestroyImmediate(surfaceMesh); }
            if (rippleMesh != null) { if (Application.isPlaying) Destroy(rippleMesh); else DestroyImmediate(rippleMesh); }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebug) return;
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(1f, 0.001f, 1f));
        }

        private sealed class Ripple { public GameObject Root; public float StartTime; public float Strength; }
    }
}
