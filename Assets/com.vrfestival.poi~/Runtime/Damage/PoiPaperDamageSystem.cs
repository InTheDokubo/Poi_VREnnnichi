using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Poi
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PoiPaperSurface), typeof(PoiPaperTearSystem), typeof(PoiPaperMeshGenerator))]
    public sealed class PoiPaperDamageSystem : MonoBehaviour
    {
        [Header("Damage")]
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;
        [SerializeField, Min(0.0005f)] private float defaultDamageRadius = 0.008f;
        [SerializeField, Min(0.1f)] private float falloffPower = 1.5f;
        [SerializeField, Min(0.01f)] private float breakThreshold = 1f;
        [SerializeField, Range(0.05f, 1f)] private float fullyWetStrengthMultiplier = 0.3f;
        [SerializeField, Min(0f)] private float tearRevealDuration = 0.22f;
        [SerializeField, Range(1, 12)] private int tearRevealSteps = 6;
        [Header("Seed")]
        [SerializeField] private bool useRandomSeed;
        [SerializeField] private int fixedSeed = 12345;
        [Header("Scene View Debug")]
        [SerializeField] private bool showDamage = true;
        [SerializeField] private bool showBroken = true;

        private PoiPaperSurface surface;
        private PoiPaperTearSystem tearSystem;
        private PoiPaperMeshGenerator meshGenerator;
        private int requestSequence;
        private readonly List<Vector2Int> pendingReveal = new List<Vector2Int>(128);
        private readonly List<Vector2Int> islandBuffer = new List<Vector2Int>(128);
        private bool[] connectedCells;
        private int[] floodQueue;
        private Coroutine revealCoroutine;

        public float BreakThreshold => breakThreshold;

        public void ApplySettings(PoiPaperSettings settings)
        {
            if (settings == null) return;
            breakThreshold = Mathf.Max(0.01f, settings.breakThreshold);
            damageMultiplier = Mathf.Max(0f, settings.damageMultiplier);
            fullyWetStrengthMultiplier = Mathf.Clamp(settings.fullyWetStrengthMultiplier, 0.05f, 1f);
            falloffPower = Mathf.Max(0.1f, settings.damageFalloffPower);
        }

        private void Awake()
        {
            Cache();
        }

        public bool ApplyDamage(PoiDamageRequest request)
        {
            Cache();
            if (request.Amount <= 0f || !surface.IsInsidePaper(request.WorldPosition)) return false;
            float radius = request.Radius > 0f ? request.Radius : defaultDamageRadius;
            Vector2 paperPosition = surface.WorldToPaperPosition(request.WorldPosition);
            Vector3 localDirection3 = surface.transform.InverseTransformDirection(request.WorldDirection);
            Vector2 paperDirection = new Vector2(localDirection3.x, localDirection3.y);
            bool thresholdReached = AccumulateDamage(paperPosition, request.Amount * damageMultiplier, radius);
            if (!thresholdReached) return true;

            int seed = ResolveSeed(request.Seed);
            bool brokenChanged = tearSystem.GenerateTear(paperPosition, paperDirection, request.Amount * damageMultiplier, radius, seed);
            if (brokenChanged)
            {
                if (Application.isPlaying && tearRevealDuration > 0f)
                    BeginReveal();
                else
                {
                    DetachUnsupportedIslands();
                    meshGenerator.Rebuild();
                }
            }
            return true;
        }

        public void ResetPaper()
        {
            Cache();
            if (revealCoroutine != null) StopCoroutine(revealCoroutine);
            revealCoroutine = null;
            pendingReveal.Clear();
            surface.ResetCellStates();
            tearSystem.ResetTearMap();
            requestSequence = 0;
            meshGenerator.ClearDetachedFragments();
            meshGenerator.Rebuild();
        }

        public void ResolveUnsupportedIslands()
        {
            Cache();
            CompletePendingReveal();
            DetachUnsupportedIslands();
            meshGenerator.Rebuild();
        }

        private void BeginReveal()
        {
            IReadOnlyList<Vector2Int> newlyBroken = tearSystem.NewlyBrokenCells;
            for (int i = 0; i < newlyBroken.Count; i++)
            {
                Vector2Int coordinate = newlyBroken[i];
                PoiPaperCell cell = surface.GetCell(coordinate);
                cell.IsBroken = false;
                surface.SetCell(coordinate, cell);
                pendingReveal.Add(coordinate);
            }

            // Several contacts can cross the threshold during the same tear. Keep
            // feeding the active reveal instead of completing and rebuilding it
            // once for every physics callback.
            if (revealCoroutine != null) return;
            revealCoroutine = StartCoroutine(RevealTearRoutine());
        }

        private IEnumerator RevealTearRoutine()
        {
            int revealed = 0;
            int steps = Mathf.Max(1, tearRevealSteps);
            WaitForSeconds wait = new WaitForSeconds(tearRevealDuration / steps);
            for (int step = 1; step <= steps; step++)
            {
                int target = Mathf.CeilToInt(pendingReveal.Count * (float)step / steps);
                for (; revealed < target; revealed++) SetBroken(pendingReveal[revealed]);
                // Collider recooking is substantially more expensive than drawing
                // the evolving edge. Defer it until the tear has fully opened.
                meshGenerator.RebuildVisualOnly();
                if (step < steps) yield return wait;
            }
            pendingReveal.Clear();
            revealCoroutine = null;
            DetachUnsupportedIslands();
            meshGenerator.Rebuild();
        }

        private void CompletePendingReveal()
        {
            if (pendingReveal.Count == 0) return;
            if (revealCoroutine != null) StopCoroutine(revealCoroutine);
            for (int i = 0; i < pendingReveal.Count; i++) SetBroken(pendingReveal[i]);
            pendingReveal.Clear();
            revealCoroutine = null;
            DetachUnsupportedIslands();
            meshGenerator.Rebuild();
        }

        private void SetBroken(Vector2Int coordinate)
        {
            PoiPaperCell cell = surface.GetCell(coordinate);
            cell.IsBroken = true;
            cell.Damage = Mathf.Max(cell.Damage, breakThreshold);
            surface.SetCell(coordinate, cell);
        }

        private void DetachUnsupportedIslands()
        {
            int resolution = surface.GridResolution;
            int count = resolution * resolution;
            if (connectedCells == null || connectedCells.Length != count) connectedCells = new bool[count];
            else System.Array.Clear(connectedCells, 0, connectedCells.Length);
            if (floodQueue == null || floodQueue.Length != count) floodQueue = new int[count];

            int head = 0;
            int tail = 0;
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    if (!IsSolid(x, y) || !TouchesPaperEdge(x, y)) continue;
                    int index = y * resolution + x;
                    if (connectedCells[index]) continue;
                    connectedCells[index] = true;
                    floodQueue[tail++] = index;
                }
            }
            FloodConnected(ref head, ref tail, resolution);

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int startIndex = y * resolution + x;
                    if (connectedCells[startIndex] || !IsSolid(x, y)) continue;
                    islandBuffer.Clear();
                    head = 0;
                    tail = 0;
                    connectedCells[startIndex] = true;
                    floodQueue[tail++] = startIndex;
                    while (head < tail)
                    {
                        int index = floodQueue[head++];
                        int cellX = index % resolution;
                        int cellY = index / resolution;
                        islandBuffer.Add(new Vector2Int(cellX, cellY));
                        EnqueueSolid(cellX - 1, cellY, ref tail, resolution);
                        EnqueueSolid(cellX + 1, cellY, ref tail, resolution);
                        EnqueueSolid(cellX, cellY - 1, ref tail, resolution);
                        EnqueueSolid(cellX, cellY + 1, ref tail, resolution);
                    }
                    meshGenerator.CreateDetachedFragment(islandBuffer);
                    tearSystem.MarkDetachedCells(islandBuffer);
                    for (int i = 0; i < islandBuffer.Count; i++) SetBroken(islandBuffer[i]);
                }
            }
        }

        private void FloodConnected(ref int head, ref int tail, int resolution)
        {
            while (head < tail)
            {
                int index = floodQueue[head++];
                int x = index % resolution;
                int y = index / resolution;
                EnqueueSolid(x - 1, y, ref tail, resolution);
                EnqueueSolid(x + 1, y, ref tail, resolution);
                EnqueueSolid(x, y - 1, ref tail, resolution);
                EnqueueSolid(x, y + 1, ref tail, resolution);
            }
        }

        private void EnqueueSolid(int x, int y, ref int tail, int resolution)
        {
            if (x < 0 || x >= resolution || y < 0 || y >= resolution || !IsSolid(x, y)) return;
            int index = y * resolution + x;
            if (connectedCells[index]) return;
            connectedCells[index] = true;
            floodQueue[tail++] = index;
        }

        private bool IsSolid(int x, int y)
        {
            PoiPaperCell cell = surface.GetCell(new Vector2Int(x, y));
            return cell.IsPaper && !cell.IsBroken;
        }

        private bool TouchesPaperEdge(int x, int y)
        {
            return !IsPaperCell(x - 1, y) || !IsPaperCell(x + 1, y) || !IsPaperCell(x, y - 1) || !IsPaperCell(x, y + 1);
        }

        private bool IsPaperCell(int x, int y)
        {
            if (x < 0 || x >= surface.GridResolution || y < 0 || y >= surface.GridResolution) return false;
            return surface.GetCell(new Vector2Int(x, y)).IsPaper;
        }

        private bool AccumulateDamage(Vector2 center, float amount, float radius)
        {
            Vector2 normalized = surface.PaperToNormalized(center);
            Vector2Int centerCell = surface.NormalizedToCell(normalized);
            float cellSize = surface.Radius * 2f / surface.GridResolution;
            int range = Mathf.CeilToInt(radius / cellSize) + 1;
            bool thresholdReached = false;
            for (int y = centerCell.y - range; y <= centerCell.y + range; y++)
            {
                for (int x = centerCell.x - range; x <= centerCell.x + range; x++)
                {
                    Vector2Int coordinate = new Vector2Int(x, y);
                    if (!surface.IsValidCell(coordinate)) continue;
                    PoiPaperCell cell = surface.GetCell(coordinate);
                    if (!cell.IsPaper) continue;
                    Vector2 local = surface.NormalizedToPaperPosition(surface.CellToNormalized(coordinate));
                    // Treat a cell as an area rather than a point. A request inside the
                    // cell receives full strength instead of losing damage to grid quantization.
                    float distanceToCenter = Vector2.Distance(local, center);
                    float distanceToCell = Mathf.Max(0f, distanceToCenter - cellSize * 0.70710678f);
                    if (distanceToCell > radius) continue;
                    float falloff = Mathf.Pow(1f - distanceToCell / Mathf.Max(radius, 0.0001f), falloffPower);
                    cell.Damage = Mathf.Max(0f, cell.Damage + amount * falloff);
                    surface.SetCell(coordinate, cell);
                    float effectiveThreshold = breakThreshold * Mathf.Lerp(1f, fullyWetStrengthMultiplier, cell.Wetness);
                    // Existing damage remains when the paper gets wet. A cell that
                    // was safe while dry can therefore cross its reduced wet limit.
                    if (!cell.IsBroken && cell.Damage >= effectiveThreshold)
                        thresholdReached = true;
                }
            }
            return thresholdReached;
        }

        private int ResolveSeed(int requestSeed)
        {
            if (requestSeed != 0) return requestSeed;
            if (useRandomSeed) return Random.Range(int.MinValue, int.MaxValue);
            return fixedSeed + requestSequence++ * 486187739;
        }

        private void Cache()
        {
            if (surface == null) surface = GetComponent<PoiPaperSurface>();
            if (tearSystem == null) tearSystem = GetComponent<PoiPaperTearSystem>();
            if (meshGenerator == null) meshGenerator = GetComponent<PoiPaperMeshGenerator>();
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDamage && !showBroken) return;
            Cache();
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Color oldColor = Gizmos.color;
            Gizmos.matrix = surface.transform.localToWorldMatrix;
            float size = surface.Radius * 2f / surface.GridResolution;
            Vector3 cubeSize = new Vector3(size * 0.76f, size * 0.76f, 0.00025f);
            for (int y = 0; y < surface.GridResolution; y++)
            {
                for (int x = 0; x < surface.GridResolution; x++)
                {
                    Vector2Int coordinate = new Vector2Int(x, y);
                    PoiPaperCell cell = surface.GetCell(coordinate);
                    if (!cell.IsPaper) continue;
                    if (cell.IsBroken && showBroken) Gizmos.color = new Color(0.08f, 0.08f, 0.08f, 0.8f);
                    else if (cell.Damage > 0f && showDamage) Gizmos.color = new Color(1f, 0.15f, 0f, Mathf.Clamp01(cell.Damage / breakThreshold) * 0.65f);
                    else continue;
                    Vector2 local = surface.NormalizedToPaperPosition(surface.CellToNormalized(coordinate));
                    Gizmos.DrawCube(new Vector3(local.x, local.y, 0f), cubeSize);
                }
            }
            Gizmos.matrix = oldMatrix;
            Gizmos.color = oldColor;
        }
    }
}
