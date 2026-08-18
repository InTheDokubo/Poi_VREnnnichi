using UnityEngine;

namespace Poi
{
    /// <summary>
    /// Owns paper-space coordinate conversion and the logical simulation grid.
    /// Paper space uses local X/Y; local +Z is the surface normal.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoiPaperSurface : MonoBehaviour
    {
        [Header("Paper Region")]
        [SerializeField, Min(0.001f)] private float radius = 0.042f;
        [SerializeField, Min(1)] private int gridResolution = 32;
        [SerializeField, Min(0f)] private float surfaceTolerance = 0.005f;

        [Header("Scene View Debug")]
        [SerializeField] private bool showOutline = true;
        [SerializeField] private bool showGrid = true;
        [SerializeField] private bool showValidCells;
        [SerializeField] private bool showInvalidCells;
        [SerializeField] private bool showAxes = true;

        [System.NonSerialized] private PoiPaperCell[] cells;

        public float Radius => radius;
        public int GridResolution => gridResolution;
        public float SurfaceTolerance => surfaceTolerance;
        public int CellCount => cells == null ? 0 : cells.Length;

        private void Awake()
        {
            EnsureGrid();
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.001f, radius);
            gridResolution = Mathf.Max(1, gridResolution);
            surfaceTolerance = Mathf.Max(0f, surfaceTolerance);
            RebuildGrid();
        }

        public void RebuildGrid()
        {
            int count = gridResolution * gridResolution;
            if (cells == null || cells.Length != count)
                cells = new PoiPaperCell[count];

            for (int y = 0; y < gridResolution; y++)
            {
                for (int x = 0; x < gridResolution; x++)
                {
                    Vector2 normalized = CellToNormalized(new Vector2Int(x, y));
                    Vector2 centered = normalized * 2f - Vector2.one;
                    cells[ToIndex(x, y)].IsPaper = centered.sqrMagnitude <= 1f;
                }
            }
        }

        /// <summary>Projects a world point onto the paper plane and returns local XY metres.</summary>
        public Vector2 WorldToPaperPosition(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            return new Vector2(local.x, local.y);
        }

        public Vector2 WorldToNormalizedPosition(Vector3 worldPosition)
        {
            return PaperToNormalized(WorldToPaperPosition(worldPosition));
        }

        public Vector2 PaperToNormalized(Vector2 paperPosition)
        {
            return paperPosition / (radius * 2f) + Vector2.one * 0.5f;
        }

        public Vector2 NormalizedToPaperPosition(Vector2 normalizedPosition)
        {
            return (normalizedPosition - Vector2.one * 0.5f) * (radius * 2f);
        }

        public bool IsInsidePaper(Vector2 normalizedPosition)
        {
            Vector2 centered = normalizedPosition * 2f - Vector2.one;
            return centered.sqrMagnitude <= 1f;
        }

        public bool IsInsidePaper(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            if (Mathf.Abs(local.z) > surfaceTolerance) return false;
            return local.x * local.x + local.y * local.y <= radius * radius;
        }

        public bool TryWorldToCell(Vector3 worldPosition, out Vector2Int cell)
        {
            if (!IsInsidePaper(worldPosition))
            {
                cell = default;
                return false;
            }

            cell = NormalizedToCell(WorldToNormalizedPosition(worldPosition));
            return IsValidCell(cell) && GetCell(cell).IsPaper;
        }

        public Vector2Int NormalizedToCell(Vector2 normalizedPosition)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(normalizedPosition.x * gridResolution), 0, gridResolution - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(normalizedPosition.y * gridResolution), 0, gridResolution - 1);
            return new Vector2Int(x, y);
        }

        public Vector2 CellToNormalized(Vector2Int cell)
        {
            float inverseResolution = 1f / gridResolution;
            return new Vector2(
                (cell.x + 0.5f) * inverseResolution,
                (cell.y + 0.5f) * inverseResolution);
        }

        public Vector3 CellToWorldPosition(Vector2Int cell)
        {
            if (!IsValidCell(cell))
                throw new System.ArgumentOutOfRangeException(nameof(cell), cell, "Cell is outside the paper grid.");

            Vector2 local = NormalizedToPaperPosition(CellToNormalized(cell));
            return transform.TransformPoint(new Vector3(local.x, local.y, 0f));
        }

        public bool IsValidCell(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < gridResolution && cell.y >= 0 && cell.y < gridResolution;
        }

        public PoiPaperCell GetCell(Vector2Int cell)
        {
            EnsureGrid();
            if (!IsValidCell(cell))
                throw new System.ArgumentOutOfRangeException(nameof(cell), cell, "Cell is outside the paper grid.");
            return cells[ToIndex(cell.x, cell.y)];
        }

        public void SetCell(Vector2Int cell, PoiPaperCell value)
        {
            EnsureGrid();
            if (!IsValidCell(cell))
                throw new System.ArgumentOutOfRangeException(nameof(cell), cell, "Cell is outside the paper grid.");
            cells[ToIndex(cell.x, cell.y)] = value;
        }

        public void ResetCellStates()
        {
            EnsureGrid();
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].Damage = 0f;
                cells[i].Wetness = 0f;
                cells[i].IsBroken = false;
            }
        }

        private void EnsureGrid()
        {
            if (cells == null || cells.Length != gridResolution * gridResolution)
                RebuildGrid();
        }

        private int ToIndex(int x, int y)
        {
            return y * gridResolution + x;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showOutline && !showGrid && !showValidCells && !showInvalidCells && !showAxes) return;
            EnsureGrid();

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;

            if (showOutline) DrawOutline();
            if (showGrid) DrawGrid();
            if (showValidCells || showInvalidCells) DrawCells();
            if (showAxes) DrawAxes();

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private void DrawOutline()
        {
            Gizmos.color = new Color(0.1f, 0.85f, 1f, 1f);
            const int segments = 64;
            Vector3 previous = new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 next = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        private void DrawGrid()
        {
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.22f);
            float diameter = radius * 2f;
            float step = diameter / gridResolution;
            for (int i = 0; i <= gridResolution; i++)
            {
                float value = -radius + i * step;
                Gizmos.DrawLine(new Vector3(-radius, value, 0f), new Vector3(radius, value, 0f));
                Gizmos.DrawLine(new Vector3(value, -radius, 0f), new Vector3(value, radius, 0f));
            }
        }

        private void DrawCells()
        {
            float size = radius * 2f / gridResolution;
            Vector3 cubeSize = new Vector3(size * 0.86f, size * 0.86f, 0.00015f);
            for (int y = 0; y < gridResolution; y++)
            {
                for (int x = 0; x < gridResolution; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    bool isPaper = cells[ToIndex(x, y)].IsPaper;
                    if ((isPaper && !showValidCells) || (!isPaper && !showInvalidCells)) continue;
                    Gizmos.color = isPaper
                        ? new Color(0.1f, 1f, 0.35f, 0.2f)
                        : new Color(1f, 0.2f, 0.15f, 0.14f);
                    Vector2 local = NormalizedToPaperPosition(CellToNormalized(cell));
                    Gizmos.DrawCube(new Vector3(local.x, local.y, 0f), cubeSize);
                }
            }
        }

        private void DrawAxes()
        {
            float length = radius * 1.25f;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(Vector3.zero, Vector3.right * length);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(Vector3.zero, Vector3.up * length);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * length * 0.5f);
            Gizmos.DrawSphere(Vector3.zero, radius * 0.025f);
        }
    }
}
