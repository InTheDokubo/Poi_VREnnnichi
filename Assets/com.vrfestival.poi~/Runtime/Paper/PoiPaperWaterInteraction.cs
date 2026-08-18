using UnityEngine;

namespace Poi
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PoiPaperSurface), typeof(PoiPaperDamageSystem))]
    public sealed class PoiPaperWaterInteraction : MonoBehaviour
    {
        [Header("Wetness")]
        [SerializeField, Min(0f)] private float wettingRate = 0.75f;
        [SerializeField, Min(0f)] private float diffusionRate = 0.9f;
        [SerializeField, Min(0f)] private float dryingRate = 0.025f;
        [SerializeField, Range(0.02f, 0.5f)] private float updateInterval = 0.1f;
        [Header("Water damage")]
        [SerializeField, Min(0f)] private float minimumDamageSpeed = 0.35f;
        [SerializeField, Min(0f)] private float waterDamageMultiplier = 0.7f;
        [SerializeField, Min(0f)] private float waterEntryMultiplier = 0.35f;
        [SerializeField, Min(0f)] private float maximumDamagePerSample = 0.18f;
        [Header("Debug")]
        [SerializeField] private bool showWetCells = true;

        private PoiPaperSurface surface;
        private PoiPaperDamageSystem damageSystem;
        private Rigidbody body;
        private float[] nextWetness;
        private float nextUpdateTime;
        private int previousSubmergedCount;
        private Texture2D wetnessTexture;
        private Color32[] wetnessPixels;
        private MaterialPropertyBlock wetnessProperties;
        private MeshRenderer paperRenderer;
        private Transform motionTransform;
        private Vector3 previousMotionPosition;
        private Quaternion previousMotionRotation;
        private Vector3 sampledLinearVelocity;
        private Vector3 sampledAngularVelocity;
        private bool hasPreviousMotionPose;

        public void ApplySettings(PoiPaperSettings settings)
        {
            if (settings == null) return;
            wettingRate = Mathf.Max(0f, settings.wettingRate);
            diffusionRate = Mathf.Max(0f, settings.wetnessDiffusionRate);
            dryingRate = Mathf.Max(0f, settings.dryingRate);
            updateInterval = Mathf.Clamp(settings.wetnessUpdateInterval, 0.02f, 0.5f);
            minimumDamageSpeed = Mathf.Max(0f, settings.minimumWaterDamageSpeed);
            waterDamageMultiplier = Mathf.Max(0f, settings.waterDamageMultiplier);
            waterEntryMultiplier = Mathf.Max(0f, settings.waterEntryMultiplier);
            maximumDamagePerSample = Mathf.Max(0f, settings.maximumWaterDamagePerSample);
        }

        private void Awake()
        {
            surface = GetComponent<PoiPaperSurface>();
            damageSystem = GetComponent<PoiPaperDamageSystem>();
            body = GetComponentInParent<Rigidbody>();
            motionTransform = body != null ? body.transform : transform.root;
            paperRenderer = GetComponent<MeshRenderer>();
            CreateWetnessTexture();
        }

        private void OnEnable()
        {
            hasPreviousMotionPose = false;
            if (Application.isPlaying) CreateWetnessTexture();
        }

        private void OnDestroy()
        {
            if (wetnessTexture != null) Destroy(wetnessTexture);
        }

        private void FixedUpdate()
        {
            SampleMotion(Time.fixedDeltaTime);
            if (Time.time < nextUpdateTime) return;
            float dt = Mathf.Max(updateInterval, Time.fixedDeltaTime);
            nextUpdateTime = Time.time + updateInterval;
            UpdateWater(dt);
        }

        private void UpdateWater(float deltaTime)
        {
            int resolution = surface.GridResolution;
            int count = resolution * resolution;
            if (nextWetness == null || nextWetness.Length != count) nextWetness = new float[count];
            int submergedCount = 0;
            Vector3 damagePointSum = Vector3.zero;
            Vector3 velocitySum = Vector3.zero;
            float speedSquaredSum = 0f;
            int damagingCellCount = 0;

            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                int index = y * resolution + x;
                Vector2Int coordinate = new Vector2Int(x, y);
                PoiPaperCell cell = surface.GetCell(coordinate);
                nextWetness[index] = cell.Wetness;
                if (!cell.IsPaper || cell.IsBroken) continue;
                Vector3 point = surface.CellToWorldPosition(coordinate);
                if (!TryGetWater(point, out PoiWaterVolume water)) continue;
                submergedCount++;
                nextWetness[index] = Mathf.Clamp01(cell.Wetness + wettingRate * deltaTime);
                Vector3 relativeVelocity = GetPointVelocity(point) - water.GetWaterVelocity(point);
                float speed = relativeVelocity.magnitude;
                if (speed > minimumDamageSpeed)
                {
                    damagePointSum += point;
                    velocitySum += relativeVelocity;
                    speedSquaredSum += speed * speed;
                    damagingCellCount++;
                }
            }

            DiffuseAndDry(resolution, deltaTime);

            if (speedSquaredSum > 0f && damagingCellCount > 0)
            {
                float areaFraction = submergedCount / (float)Mathf.Max(1, CountPaperCells());
                float entryBoost = previousSubmergedCount == 0 ? waterEntryMultiplier : 0f;
                float amount = Mathf.Min(maximumDamagePerSample,
                    (speedSquaredSum / damagingCellCount) * areaFraction * waterDamageMultiplier * deltaTime + entryBoost);
                Vector3 point = damagePointSum / damagingCellCount;
                damageSystem.ApplyDamage(new PoiDamageRequest(point, velocitySum, amount, surface.Radius * Mathf.Sqrt(areaFraction), 0));
            }
            previousSubmergedCount = submergedCount;
            UpdateWetnessVisual();
        }

        private void SampleMotion(float deltaTime)
        {
            if (motionTransform == null) motionTransform = body != null ? body.transform : transform.root;
            Vector3 position = motionTransform.position;
            Quaternion rotation = motionTransform.rotation;
            if (!hasPreviousMotionPose || deltaTime <= Mathf.Epsilon)
            {
                previousMotionPosition = position;
                previousMotionRotation = rotation;
                sampledLinearVelocity = Vector3.zero;
                sampledAngularVelocity = Vector3.zero;
                hasPreviousMotionPose = true;
                return;
            }

            sampledLinearVelocity = (position - previousMotionPosition) / deltaTime;
            Quaternion delta = rotation * Quaternion.Inverse(previousMotionRotation);
            delta.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (angleDegrees > 180f) angleDegrees -= 360f;
            sampledAngularVelocity = axis.sqrMagnitude > 0.000001f
                ? axis.normalized * (angleDegrees * Mathf.Deg2Rad / deltaTime)
                : Vector3.zero;
            previousMotionPosition = position;
            previousMotionRotation = rotation;
        }

        private Vector3 GetPointVelocity(Vector3 worldPoint)
        {
            Vector3 transformVelocity = sampledLinearVelocity +
                                        Vector3.Cross(sampledAngularVelocity, worldPoint - motionTransform.position);
            if (body == null) return transformVelocity;
            Vector3 rigidbodyVelocity = body.GetPointVelocity(worldPoint);
            // XRGrabInteractable's Instantaneous and some Kinematic configurations move
            // the Transform without publishing a useful Rigidbody velocity.
            return transformVelocity.sqrMagnitude > rigidbodyVelocity.sqrMagnitude
                ? transformVelocity
                : rigidbodyVelocity;
        }

        private void CreateWetnessTexture()
        {
            if (surface == null) surface = GetComponent<PoiPaperSurface>();
            if (paperRenderer == null) paperRenderer = GetComponent<MeshRenderer>();
            int resolution = surface.GridResolution;
            if (wetnessTexture != null && wetnessTexture.width == resolution) return;
            if (wetnessTexture != null) Destroy(wetnessTexture);
            wetnessTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
            {
                name = "PoiPaper_Wetness_Runtime",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            wetnessPixels = new Color32[resolution * resolution];
            if (wetnessProperties == null) wetnessProperties = new MaterialPropertyBlock();
            paperRenderer.GetPropertyBlock(wetnessProperties);
            wetnessProperties.SetTexture("_WetnessMap", wetnessTexture);
            paperRenderer.SetPropertyBlock(wetnessProperties);
            UpdateWetnessVisual();
        }

        private void UpdateWetnessVisual()
        {
            if (wetnessTexture == null || wetnessPixels == null) return;
            int resolution = surface.GridResolution;
            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                PoiPaperCell cell = surface.GetCell(new Vector2Int(x, y));
                byte value = cell.IsPaper && !cell.IsBroken ? (byte)Mathf.RoundToInt(Mathf.Clamp01(cell.Wetness) * 255f) : (byte)0;
                wetnessPixels[y * resolution + x] = new Color32(value, value, value, 255);
            }
            wetnessTexture.SetPixels32(wetnessPixels);
            wetnessTexture.Apply(false, false);
        }

        private void DiffuseAndDry(int resolution, float deltaTime)
        {
            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                int index = y * resolution + x;
                Vector2Int coordinate = new Vector2Int(x, y);
                PoiPaperCell cell = surface.GetCell(coordinate);
                if (!cell.IsPaper || cell.IsBroken) { nextWetness[index] = 0f; continue; }
                float neighborSum = 0f;
                float neighborWeight = 0f;
                AddNeighbor(x - 1, y, 1f); AddNeighbor(x + 1, y, 1f);
                AddNeighbor(x, y - 1, 1f); AddNeighbor(x, y + 1, 1f);
                const float diagonalWeight = 0.70710678f;
                AddNeighbor(x - 1, y - 1, diagonalWeight); AddNeighbor(x + 1, y - 1, diagonalWeight);
                AddNeighbor(x - 1, y + 1, diagonalWeight); AddNeighbor(x + 1, y + 1, diagonalWeight);
                float diffusion = neighborWeight > 0f
                    ? (neighborSum / neighborWeight - cell.Wetness) * Mathf.Min(1f, diffusionRate * deltaTime)
                    : 0f;
                bool submerged = TryGetWater(surface.CellToWorldPosition(coordinate), out _);
                nextWetness[index] = Mathf.Clamp01(nextWetness[index] + diffusion - (submerged ? 0f : dryingRate * deltaTime));

                void AddNeighbor(int nx, int ny, float weight)
                {
                    Vector2Int neighbor = new Vector2Int(nx, ny);
                    if (!surface.IsValidCell(neighbor)) return;
                    PoiPaperCell other = surface.GetCell(neighbor);
                    if (!other.IsPaper || other.IsBroken) return;
                    neighborSum += other.Wetness * weight;
                    neighborWeight += weight;
                }
            }
            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                Vector2Int coordinate = new Vector2Int(x, y);
                PoiPaperCell cell = surface.GetCell(coordinate);
                cell.Wetness = nextWetness[y * resolution + x];
                surface.SetCell(coordinate, cell);
            }
        }

        private int CountPaperCells()
        {
            int total = 0;
            for (int y = 0; y < surface.GridResolution; y++)
            for (int x = 0; x < surface.GridResolution; x++)
                if (surface.GetCell(new Vector2Int(x, y)).IsPaper) total++;
            return total;
        }

        private static bool TryGetWater(Vector3 point, out PoiWaterVolume result)
        {
            var volumes = PoiWaterVolume.ActiveVolumes;
            for (int i = 0; i < volumes.Count; i++)
                if (volumes[i] != null && volumes[i].Contains(point)) { result = volumes[i]; return true; }
            result = null;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showWetCells || surface == null) return;
            float size = surface.Radius * 2f / surface.GridResolution * 0.75f;
            for (int y = 0; y < surface.GridResolution; y++)
            for (int x = 0; x < surface.GridResolution; x++)
            {
                PoiPaperCell cell = surface.GetCell(new Vector2Int(x, y));
                if (!cell.IsPaper) continue;
                Vector3 point = surface.CellToWorldPosition(new Vector2Int(x, y));
                if (cell.IsBroken) Gizmos.color = new Color(0.12f, 0.12f, 0.12f, 0.65f);
                else if (TryGetWater(point, out _)) Gizmos.color = new Color(0f, 0.9f, 1f, 0.8f);
                else if (cell.Wetness > 0.001f) Gizmos.color = Color.Lerp(new Color(0.2f, 0.8f, 1f, 0.08f), new Color(0f, 0.25f, 1f, 0.7f), cell.Wetness);
                else continue;
                Gizmos.DrawCube(point, Vector3.one * size);
            }
        }
    }
}
