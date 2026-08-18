using System;
using System.Collections.Generic;
using UnityEngine;

namespace Poi
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PoiPaperSurface))]
    public sealed class PoiPaperTearSystem : MonoBehaviour
    {
        [Header("Tear Shape")]
        [SerializeField, Min(0.001f)] private float tearLength = 0.025f;
        [SerializeField, Min(0.0005f)] private float tearWidth = 0.0035f;
        [SerializeField, Range(0f, 1f)] private float branchChance = 0.45f;
        [SerializeField, Range(0f, 1f)] private float directionInfluence = 0.7f;
        [SerializeField, Range(0f, 1f)] private float randomness = 0.4f;
        [SerializeField, Range(0f, 1f)] private float edgeInfluence = 0.35f;
        [SerializeField, Range(0f, 1f)] private float existingDamageInfluence = 0.65f;
        [SerializeField, Range(0f, 1f)] private float boundaryRoughness = 0.45f;
        [SerializeField, Range(2, 6)] private int tearMapResolutionMultiplier = 4;

        private PoiPaperSurface surface;
        private readonly List<Vector2Int> newlyBrokenCells = new List<Vector2Int>(128);
        private bool[] tearMap;
        private int tearMapResolution;

        public IReadOnlyList<Vector2Int> NewlyBrokenCells => newlyBrokenCells;

        private void Awake() => Cache();

        public bool GenerateTear(Vector2 paperPosition, Vector2 forceDirection, float amount, float inputRadius, int seed)
        {
            Cache();
            EnsureTearMap();
            newlyBrokenCells.Clear();
            var random = new System.Random(seed);
            bool directional = forceDirection.sqrMagnitude > 0.01f;
            Vector2 primary = directional ? forceDirection.normalized : RandomDirection(random);
            float strength = Mathf.Max(0.15f, amount);
            float length = tearLength * Mathf.Lerp(0.65f, 1.8f, Mathf.Clamp01(strength)) + inputRadius * 0.5f;
            float width = tearWidth * Mathf.Lerp(0.7f, 1.5f, Mathf.Clamp01(strength));
            bool changed = MarkBrush(paperPosition, Mathf.Max(width, inputRadius * 0.28f), seed);

            int branchCount = directional ? 1 : 4;
            if (strength > 0.85f) branchCount++;
            for (int branch = 0; branch < branchCount; branch++)
            {
                Vector2 direction = directional
                    ? Rotate(primary, Mathf.Lerp(-35f, 35f, NextFloat(random)))
                    : Rotate(primary, 360f * branch / branchCount + Mathf.Lerp(-24f, 24f, NextFloat(random)));
                changed |= WalkTear(paperPosition, direction, primary, length * Mathf.Lerp(0.65f, 1f, NextFloat(random)), width, seed + branch * 977, random);
            }

            if (NextFloat(random) < branchChance * Mathf.Clamp01(strength))
            {
                Vector2 branchDirection = Rotate(primary, Mathf.Lerp(-80f, 80f, NextFloat(random)));
                changed |= WalkTear(paperPosition, branchDirection, primary, length * 0.6f, width * 0.8f, seed ^ 0x5f3759df, random);
            }
            return changed;
        }

        private bool WalkTear(Vector2 start, Vector2 direction, Vector2 forceDirection, float length, float width, int seed, System.Random random)
        {
            float cellSize = surface.Radius * 2f / surface.GridResolution;
            float step = cellSize * 0.55f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / step));
            Vector2 position = start;
            bool changed = false;
            for (int i = 0; i < steps; i++)
            {
                Vector2 noiseDirection = RandomDirection(random);
                direction = Vector2.Lerp(direction, noiseDirection, randomness * 0.22f).normalized;
                if (forceDirection.sqrMagnitude > 0.01f)
                    direction = Vector2.Lerp(direction, forceDirection.normalized, directionInfluence * 0.16f).normalized;

                Vector2 brokenAttraction = FindNearbyBrokenDirection(position);
                if (brokenAttraction.sqrMagnitude > 0.01f)
                    direction = Vector2.Lerp(direction, brokenAttraction, existingDamageInfluence * 0.28f).normalized;

                float radial = position.magnitude / surface.Radius;
                if (radial > 0.7f)
                    direction = Vector2.Lerp(direction, position.normalized, edgeInfluence * (radial - 0.7f)).normalized;

                position += direction * step;
                float progress = steps <= 1 ? 1f : i / (float)(steps - 1);
                float taperedWidth = width * Mathf.Lerp(1.05f, 0.18f, progress);
                taperedWidth *= Mathf.Lerp(0.78f, 1.18f, NextFloat(random));
                changed |= MarkBrush(position, taperedWidth, seed + i * 131);

                // Short hairline fibres branching away from the main crack.
                if (i > 1 && i < steps - 2 && NextFloat(random) < branchChance * 0.12f)
                {
                    Vector2 fibreDirection = Rotate(direction, NextFloat(random) < 0.5f ? -55f : 55f);
                    Vector2 fibreTip = position + fibreDirection * cellSize * Mathf.Lerp(0.8f, 2.2f, NextFloat(random));
                    MarkVisualBrush(fibreTip, Mathf.Max(cellSize * 0.16f, taperedWidth * 0.22f), seed ^ (i * 3571));
                }
                if (position.magnitude > surface.Radius + cellSize) break;
            }
            return changed;
        }

        private bool MarkBrush(Vector2 paperPosition, float radius, int seed)
        {
            MarkVisualBrush(paperPosition, radius, seed);
            Vector2 normalized = surface.PaperToNormalized(paperPosition);
            Vector2Int center = surface.NormalizedToCell(normalized);
            float cellSize = surface.Radius * 2f / surface.GridResolution;
            int range = Mathf.Max(1, Mathf.CeilToInt(radius / cellSize) + 1);
            bool changed = false;
            for (int y = center.y - range; y <= center.y + range; y++)
            {
                for (int x = center.x - range; x <= center.x + range; x++)
                {
                    Vector2Int coordinate = new Vector2Int(x, y);
                    if (!surface.IsValidCell(coordinate)) continue;
                    PoiPaperCell cell = surface.GetCell(coordinate);
                    if (!cell.IsPaper || cell.IsBroken) continue;
                    Vector2 local = surface.NormalizedToPaperPosition(surface.CellToNormalized(coordinate));
                    float noise = Hash01(x, y, seed);
                    float roughRadius = radius * Mathf.Lerp(1f - boundaryRoughness * 0.45f, 1f + boundaryRoughness * 0.45f, noise);
                    if ((local - paperPosition).sqrMagnitude > roughRadius * roughRadius) continue;
                    cell.IsBroken = true;
                    cell.Damage = Mathf.Max(1f, cell.Damage);
                    surface.SetCell(coordinate, cell);
                    newlyBrokenCells.Add(coordinate);
                    changed = true;
                }
            }
            return changed;
        }

        public float SampleTear(Vector2 normalized)
        {
            EnsureTearMap();
            float x = normalized.x * tearMapResolution - 0.5f;
            float y = normalized.y * tearMapResolution - 0.5f;
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float tx = x - x0;
            float ty = y - y0;
            float a = TearValue(x0, y0);
            float b = TearValue(x0 + 1, y0);
            float c = TearValue(x0, y0 + 1);
            float d = TearValue(x0 + 1, y0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        public void ResetTearMap()
        {
            EnsureTearMap();
            System.Array.Clear(tearMap, 0, tearMap.Length);
        }

        public void MarkDetachedCells(IReadOnlyList<Vector2Int> cells)
        {
            float cellSize = surface.Radius * 2f / surface.GridResolution;
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2 center = surface.NormalizedToPaperPosition(surface.CellToNormalized(cells[i]));
                MarkVisualBrush(center, cellSize, cells[i].x * 73856093 ^ cells[i].y * 19349663);
            }
        }

        private void MarkVisualBrush(Vector2 paperPosition, float radius, int seed)
        {
            EnsureTearMap();
            Vector2 normalized = surface.PaperToNormalized(paperPosition);
            int centerX = Mathf.FloorToInt(normalized.x * tearMapResolution);
            int centerY = Mathf.FloorToInt(normalized.y * tearMapResolution);
            float pixelSize = surface.Radius * 2f / tearMapResolution;
            int range = Mathf.Max(1, Mathf.CeilToInt(radius / pixelSize) + 1);
            for (int y = centerY - range; y <= centerY + range; y++)
            {
                for (int x = centerX - range; x <= centerX + range; x++)
                {
                    if (x < 0 || x >= tearMapResolution || y < 0 || y >= tearMapResolution) continue;
                    Vector2 sampleNormalized = new Vector2((x + 0.5f) / tearMapResolution, (y + 0.5f) / tearMapResolution);
                    Vector2 samplePosition = surface.NormalizedToPaperPosition(sampleNormalized);
                    float fibreNoise = Hash01(x, y, seed);
                    float roughRadius = radius * Mathf.Lerp(0.72f, 1.22f, fibreNoise);
                    if ((samplePosition - paperPosition).sqrMagnitude <= roughRadius * roughRadius)
                        tearMap[y * tearMapResolution + x] = true;
                }
            }
        }

        private void EnsureTearMap()
        {
            Cache();
            int requiredResolution = surface.GridResolution * Mathf.Max(2, tearMapResolutionMultiplier);
            if (tearMap != null && tearMapResolution == requiredResolution) return;
            tearMapResolution = requiredResolution;
            tearMap = new bool[tearMapResolution * tearMapResolution];
        }

        private float TearValue(int x, int y)
        {
            if (x < 0 || x >= tearMapResolution || y < 0 || y >= tearMapResolution) return 0f;
            return tearMap[y * tearMapResolution + x] ? 1f : 0f;
        }

        private Vector2 FindNearbyBrokenDirection(Vector2 paperPosition)
        {
            Vector2Int center = surface.NormalizedToCell(surface.PaperToNormalized(paperPosition));
            Vector2 sum = Vector2.zero;
            for (int y = -3; y <= 3; y++)
            {
                for (int x = -3; x <= 3; x++)
                {
                    Vector2Int candidate = center + new Vector2Int(x, y);
                    if (!surface.IsValidCell(candidate) || !surface.GetCell(candidate).IsBroken) continue;
                    Vector2 delta = surface.NormalizedToPaperPosition(surface.CellToNormalized(candidate)) - paperPosition;
                    if (delta.sqrMagnitude > 0.000001f) sum += delta.normalized / Mathf.Max(1f, delta.magnitude * 100f);
                }
            }
            return sum.sqrMagnitude > 0.001f ? sum.normalized : Vector2.zero;
        }

        private void Cache()
        {
            if (surface == null) surface = GetComponent<PoiPaperSurface>();
        }

        private static Vector2 RandomDirection(System.Random random)
        {
            float angle = NextFloat(random) * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private static Vector2 Rotate(Vector2 value, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos).normalized;
        }

        private static float NextFloat(System.Random random) => (float)random.NextDouble();

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint value = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                value = (value ^ (value >> 13)) * 1274126177u;
                return (value ^ (value >> 16)) / (float)uint.MaxValue;
            }
        }
    }
}
