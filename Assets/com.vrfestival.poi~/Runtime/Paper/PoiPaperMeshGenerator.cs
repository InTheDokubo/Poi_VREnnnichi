using System.Collections.Generic;
using UnityEngine;

namespace Poi
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PoiPaperSurface), typeof(MeshFilter))]
    public sealed class PoiPaperMeshGenerator : MonoBehaviour
    {
        [Header("Broken Boundary")]
        [SerializeField, Range(0f, 0.45f)] private float boundaryRoughness = 0.22f;
        [SerializeField] private int boundarySeed = 1847;
        [Tooltip("Additional visual subdivisions per simulation cell. Increase this to improve tear-edge detail without increasing the simulation grid resolution.")]
        [SerializeField, Range(1, 4)] private int meshSubdivisions = 4;
        [SerializeField, Range(0.3f, 0.7f)] private float contourThreshold = 0.48f;
        [Tooltip("Visual-only connected components smaller than this fraction of the generated paper area are omitted. The largest component is always retained.")]
        [SerializeField, Range(0f, 0.01f)] private float visualIslandMinimumAreaRatio = 0.0005f;
        [Header("Collision")]
        [SerializeField, Min(0.0002f)] private float colliderThickness = 0.0006f;
        [Header("Detached Fragments")]
        [SerializeField, Min(0f)] private float fragmentLifetime = 0.9f;
        [SerializeField, Min(0.05f)] private float fragmentDissolveDuration = 0.75f;

        private PoiPaperSurface surface;
        private MeshFilter meshFilter;
        private PoiPaperTearSystem tearSystem;
        private Mesh runtimeMesh;
        private readonly List<BoxCollider> colliderPool = new List<BoxCollider>(64);
        private readonly List<Vector3> vertices = new List<Vector3>(1200);
        private readonly List<Vector3> normals = new List<Vector3>(2400);
        private readonly List<Vector2> uvs = new List<Vector2>(1200);
        private readonly List<int> triangles = new List<int>(7000);
        private readonly List<int> candidateTriangles = new List<int>(3500);
        private readonly List<GameObject> detachedFragments = new List<GameObject>(8);
        private readonly Dictionary<int, int> firstTriangleByVertex = new Dictionary<int, int>(1200);
        private int[] componentParents;
        private float[] componentAreas;

        public int LastVisualComponentCount { get; private set; }
        public int LastRemovedVisualIslandCount { get; private set; }
        public float LastRemovedVisualIslandArea { get; private set; }

        public int DetachedFragmentCount
        {
            get
            {
                PruneDetachedFragmentList();
                return detachedFragments.Count;
            }
        }

        public void ApplySettings(PoiPaperSettings settings)
        {
            if (settings == null) return;
            fragmentLifetime = Mathf.Max(0f, settings.fragmentLifetime);
            fragmentDissolveDuration = Mathf.Max(0.05f, settings.fragmentDissolveDuration);
        }

        private void Awake()
        {
            Cache();
            Rebuild();
        }

        private void OnDestroy()
        {
            if (runtimeMesh != null) Destroy(runtimeMesh);
        }

        public void Rebuild()
        {
            Cache();
            BuildMesh();
            BuildMergedRowColliders();
        }

        public void RebuildVisualOnly()
        {
            Cache();
            BuildMesh();
        }

        private void BuildMesh()
        {
            int resolution = surface.GridResolution * Mathf.Max(1, meshSubdivisions);
            int vertexResolution = resolution + 1;
            float diameter = surface.Radius * 2f;
            float step = diameter / resolution;
            vertices.Clear();
            normals.Clear();
            uvs.Clear();
            triangles.Clear();
            candidateTriangles.Clear();

            for (int y = 0; y < vertexResolution; y++)
            {
                for (int x = 0; x < vertexResolution; x++)
                {
                    Vector2 normalized = new Vector2((float)x / resolution, (float)y / resolution);
                    Vector2 local = surface.NormalizedToPaperPosition(normalized);
                    if (x > 0 && x < resolution && y > 0 && y < resolution)
                    {
                        float jitter = step * boundaryRoughness;
                        local.x += (Hash01(x, y, boundarySeed) - 0.5f) * jitter;
                        local.y += (Hash01(x, y, boundarySeed ^ 0x2c1b3c6d) - 0.5f) * jitter;
                    }
                    vertices.Add(new Vector3(local.x, local.y, 0f));
                    normals.Add(Vector3.forward);
                    uvs.Add(normalized);
                }
            }

            int frontVertexCount = vertices.Count;
            for (int i = 0; i < frontVertexCount; i++)
            {
                vertices.Add(vertices[i]);
                normals.Add(Vector3.back);
                uvs.Add(uvs[i]);
            }

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int a = y * vertexResolution + x;
                    int b = a + 1;
                    int c = a + vertexResolution;
                    int d = c + 1;
                    Vector2 bottomLeft = new Vector2((float)x / resolution, (float)y / resolution);
                    Vector2 bottomRight = new Vector2((float)(x + 1) / resolution, (float)y / resolution);
                    Vector2 topLeft = new Vector2((float)x / resolution, (float)(y + 1) / resolution);
                    Vector2 topRight = new Vector2((float)(x + 1) / resolution, (float)(y + 1) / resolution);

                    Vector2 firstCentroid = (bottomLeft + topLeft + topRight) / 3f;
                    if (ShouldRenderTriangle(firstCentroid, x, y, 0))
                        AddCandidateTriangle(a, c, d);

                    Vector2 secondCentroid = (bottomLeft + topRight + bottomRight) / 3f;
                    if (ShouldRenderTriangle(secondCentroid, x, y, 1))
                        AddCandidateTriangle(a, d, b);
                }
            }

            BuildConnectedVisualMesh(frontVertexCount);

            if (runtimeMesh == null)
            {
                runtimeMesh = new Mesh { name = "PoiPaper_Runtime" };
                runtimeMesh.MarkDynamic();
            }
            else runtimeMesh.Clear();

            runtimeMesh.SetVertices(vertices);
            runtimeMesh.SetNormals(normals);
            runtimeMesh.SetUVs(0, uvs);
            runtimeMesh.SetTriangles(triangles, 0);
            runtimeMesh.RecalculateBounds();
            meshFilter.sharedMesh = runtimeMesh;
        }

        private void AddDoubleSidedTriangle(int a, int b, int c, int backOffset)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a + backOffset); triangles.Add(c + backOffset); triangles.Add(b + backOffset);
        }

        private void AddCandidateTriangle(int a, int b, int c)
        {
            candidateTriangles.Add(a);
            candidateTriangles.Add(b);
            candidateTriangles.Add(c);
        }

        private void BuildConnectedVisualMesh(int backOffset)
        {
            int triangleCount = candidateTriangles.Count / 3;
            LastVisualComponentCount = 0;
            LastRemovedVisualIslandCount = 0;
            LastRemovedVisualIslandArea = 0f;
            if (triangleCount == 0) return;

            EnsureComponentBuffers(triangleCount);
            firstTriangleByVertex.Clear();
            for (int triangle = 0; triangle < triangleCount; triangle++)
            {
                componentParents[triangle] = triangle;
                componentAreas[triangle] = 0f;
                int offset = triangle * 3;
                ConnectVertex(candidateTriangles[offset], triangle);
                ConnectVertex(candidateTriangles[offset + 1], triangle);
                ConnectVertex(candidateTriangles[offset + 2], triangle);
            }

            for (int triangle = 0; triangle < triangleCount; triangle++)
            {
                int offset = triangle * 3;
                Vector3 a = vertices[candidateTriangles[offset]];
                Vector3 b = vertices[candidateTriangles[offset + 1]];
                Vector3 c = vertices[candidateTriangles[offset + 2]];
                float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                int root = FindComponent(triangle);
                componentAreas[root] += area;
            }

            int largestRoot = -1;
            float largestArea = -1f;
            for (int triangle = 0; triangle < triangleCount; triangle++)
            {
                if (FindComponent(triangle) != triangle) continue;
                LastVisualComponentCount++;
                if (componentAreas[triangle] > largestArea)
                {
                    largestArea = componentAreas[triangle];
                    largestRoot = triangle;
                }
            }

            var removedRoots = new HashSet<int>();
            // Use the full physical paper area rather than triangle count or
            // current remaining area, so the cutoff is stable across visual
            // subdivisions and does not shrink as the paper is damaged.
            float minimumArea = Mathf.PI * surface.Radius * surface.Radius * visualIslandMinimumAreaRatio;
            for (int triangle = 0; triangle < triangleCount; triangle++)
            {
                int root = FindComponent(triangle);
                if (root != largestRoot && componentAreas[root] < minimumArea)
                {
                    if (removedRoots.Add(root))
                    {
                        LastRemovedVisualIslandCount++;
                        LastRemovedVisualIslandArea += componentAreas[root];
                    }
                    continue;
                }

                int offset = triangle * 3;
                AddDoubleSidedTriangle(
                    candidateTriangles[offset],
                    candidateTriangles[offset + 1],
                    candidateTriangles[offset + 2],
                    backOffset);
            }
        }

        private void ConnectVertex(int vertex, int triangle)
        {
            if (firstTriangleByVertex.TryGetValue(vertex, out int connectedTriangle))
                UnionComponents(triangle, connectedTriangle);
            else
                firstTriangleByVertex.Add(vertex, triangle);
        }

        private void EnsureComponentBuffers(int triangleCount)
        {
            if (componentParents == null || componentParents.Length < triangleCount)
                componentParents = new int[Mathf.NextPowerOfTwo(triangleCount)];
            if (componentAreas == null || componentAreas.Length < triangleCount)
                componentAreas = new float[Mathf.NextPowerOfTwo(triangleCount)];
            else
                System.Array.Clear(componentAreas, 0, triangleCount);
        }

        private int FindComponent(int triangle)
        {
            int root = triangle;
            while (componentParents[root] != root) root = componentParents[root];
            while (componentParents[triangle] != triangle)
            {
                int next = componentParents[triangle];
                componentParents[triangle] = root;
                triangle = next;
            }
            return root;
        }

        private void UnionComponents(int a, int b)
        {
            int rootA = FindComponent(a);
            int rootB = FindComponent(b);
            if (rootA != rootB) componentParents[rootB] = rootA;
        }

        private bool ShouldRenderTriangle(Vector2 normalized, int fineX, int fineY, int triangleIndex)
        {
            Vector2 local = surface.NormalizedToPaperPosition(normalized);
            if (local.sqrMagnitude > surface.Radius * surface.Radius) return false;

            float coarseReveal = SampleBrokenField(normalized);
            float brokenField = tearSystem != null
                ? Mathf.Min(tearSystem.SampleTear(normalized), coarseReveal)
                : coarseReveal;
            float noise = Hash01(fineX, fineY, boundarySeed + triangleIndex * 7919) - 0.5f;
            float threshold = contourThreshold + noise * boundaryRoughness * 0.16f;
            return brokenField < threshold;
        }

        private float SampleBrokenField(Vector2 normalized)
        {
            int grid = surface.GridResolution;
            float gridX = normalized.x * grid - 0.5f;
            float gridY = normalized.y * grid - 0.5f;
            int x0 = Mathf.FloorToInt(gridX);
            int y0 = Mathf.FloorToInt(gridY);
            float tx = gridX - x0;
            float ty = gridY - y0;
            float a = SampleBrokenCell(x0, y0);
            float b = SampleBrokenCell(x0 + 1, y0);
            float c = SampleBrokenCell(x0, y0 + 1);
            float d = SampleBrokenCell(x0 + 1, y0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private float SampleBrokenCell(int x, int y)
        {
            Vector2Int coordinate = new Vector2Int(x, y);
            if (!surface.IsValidCell(coordinate)) return 0f;
            PoiPaperCell cell = surface.GetCell(coordinate);
            return cell.IsPaper && cell.IsBroken ? 1f : 0f;
        }

        public void CreateDetachedFragment(IReadOnlyList<Vector2Int> fragmentCells)
        {
            if (fragmentCells == null || fragmentCells.Count == 0) return;
            Cache();
            var fragmentVertices = new List<Vector3>(fragmentCells.Count * 8);
            var fragmentNormals = new List<Vector3>(fragmentCells.Count * 8);
            var fragmentUvs = new List<Vector2>(fragmentCells.Count * 8);
            var fragmentTriangles = new List<int>(fragmentCells.Count * 12);
            float cellSize = surface.Radius * 2f / surface.GridResolution;
            for (int i = 0; i < fragmentCells.Count; i++)
            {
                Vector2 center = surface.NormalizedToPaperPosition(surface.CellToNormalized(fragmentCells[i]));
                float half = cellSize * 0.5f;
                int start = fragmentVertices.Count;
                AddFragmentVertex(center + new Vector2(-half, -half), Vector3.forward);
                AddFragmentVertex(center + new Vector2(half, -half), Vector3.forward);
                AddFragmentVertex(center + new Vector2(-half, half), Vector3.forward);
                AddFragmentVertex(center + new Vector2(half, half), Vector3.forward);
                AddFragmentVertex(center + new Vector2(-half, -half), Vector3.back);
                AddFragmentVertex(center + new Vector2(half, -half), Vector3.back);
                AddFragmentVertex(center + new Vector2(-half, half), Vector3.back);
                AddFragmentVertex(center + new Vector2(half, half), Vector3.back);
                fragmentTriangles.Add(start); fragmentTriangles.Add(start + 2); fragmentTriangles.Add(start + 3);
                fragmentTriangles.Add(start); fragmentTriangles.Add(start + 3); fragmentTriangles.Add(start + 1);
                fragmentTriangles.Add(start + 4); fragmentTriangles.Add(start + 7); fragmentTriangles.Add(start + 6);
                fragmentTriangles.Add(start + 4); fragmentTriangles.Add(start + 5); fragmentTriangles.Add(start + 7);

                void AddFragmentVertex(Vector2 point, Vector3 normal)
                {
                    fragmentVertices.Add(new Vector3(point.x, point.y, 0f));
                    fragmentNormals.Add(normal);
                    fragmentUvs.Add(surface.PaperToNormalized(point));
                }
            }

            Mesh fragmentMesh = new Mesh { name = "PoiPaper_DetachedFragment" };
            fragmentMesh.SetVertices(fragmentVertices);
            fragmentMesh.SetNormals(fragmentNormals);
            fragmentMesh.SetUVs(0, fragmentUvs);
            fragmentMesh.SetTriangles(fragmentTriangles, 0);
            fragmentMesh.RecalculateBounds();

            GameObject fragment = new GameObject("DetachedPaperFragment");
            fragment.transform.SetPositionAndRotation(transform.position, transform.rotation);
            fragment.transform.localScale = transform.lossyScale;
            fragment.AddComponent<MeshFilter>().sharedMesh = fragmentMesh;
            MeshRenderer sourceRenderer = GetComponent<MeshRenderer>();
            MeshRenderer fragmentRenderer = fragment.AddComponent<MeshRenderer>();
            fragmentRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            var sourceProperties = new MaterialPropertyBlock();
            sourceRenderer.GetPropertyBlock(sourceProperties);
            fragmentRenderer.SetPropertyBlock(sourceProperties);
            BoxCollider collider = fragment.AddComponent<BoxCollider>();
            collider.center = fragmentMesh.bounds.center;
            collider.size = new Vector3(fragmentMesh.bounds.size.x, fragmentMesh.bounds.size.y, colliderThickness);
            Rigidbody body = fragment.AddComponent<Rigidbody>();
            body.mass = Mathf.Max(0.0005f, fragmentCells.Count * 0.00003f);
            body.linearDamping = 0.08f;
            body.angularDamping = 0.08f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            PoiPaperDetachedFragment fragmentLifecycle = fragment.AddComponent<PoiPaperDetachedFragment>();
            fragmentLifecycle.OwnedMesh = fragmentMesh;
            fragmentLifecycle.Initialize(
                this,
                fragmentRenderer,
                collider,
                body,
                fragmentLifetime,
                fragmentDissolveDuration,
                Hash01(fragmentCells[0].x, fragmentCells[0].y, fragmentCells.Count) * 100f);
            PruneDetachedFragmentList();
            detachedFragments.Add(fragment);
        }

        public void ClearDetachedFragments()
        {
            for (int i = detachedFragments.Count - 1; i >= 0; i--)
                if (detachedFragments[i] != null)
                {
                    PoiPaperDetachedFragment lifecycle = detachedFragments[i].GetComponent<PoiPaperDetachedFragment>();
                    if (lifecycle != null) lifecycle.Dispose();
                    else
                    {
                        detachedFragments[i].SetActive(false);
                        Destroy(detachedFragments[i]);
                    }
                }
            detachedFragments.Clear();
        }

        internal void NotifyDetachedFragmentDestroyed(GameObject fragment)
        {
            detachedFragments.Remove(fragment);
        }

        private void PruneDetachedFragmentList()
        {
            for (int i = detachedFragments.Count - 1; i >= 0; i--)
                if (detachedFragments[i] == null) detachedFragments.RemoveAt(i);
        }

        private void BuildMergedRowColliders()
        {
            CacheColliderPool();
            int used = 0;
            int resolution = surface.GridResolution;
            float size = surface.Radius * 2f / resolution;
            for (int y = 0; y < resolution; y++)
            {
                int x = 0;
                while (x < resolution)
                {
                    while (x < resolution && !IsSolid(x, y)) x++;
                    if (x >= resolution) break;
                    int start = x;
                    while (x < resolution && IsSolid(x, y)) x++;
                    int end = x;
                    BoxCollider collider = GetCollider(used++);
                    collider.enabled = true;
                    float width = (end - start) * size;
                    float centerX = -surface.Radius + (start + end) * 0.5f * size;
                    float centerY = -surface.Radius + (y + 0.5f) * size;
                    collider.center = new Vector3(centerX, centerY, 0f);
                    collider.size = new Vector3(width, size, colliderThickness);
                }
            }
            for (int i = used; i < colliderPool.Count; i++) colliderPool[i].enabled = false;
        }

        private bool IsSolid(int x, int y)
        {
            PoiPaperCell cell = surface.GetCell(new Vector2Int(x, y));
            return cell.IsPaper && !cell.IsBroken;
        }

        private void CacheColliderPool()
        {
            if (colliderPool.Count > 0) return;
            GetComponents(colliderPool);
        }

        private BoxCollider GetCollider(int index)
        {
            while (colliderPool.Count <= index)
                colliderPool.Add(gameObject.AddComponent<BoxCollider>());
            return colliderPool[index];
        }

        private void Cache()
        {
            if (surface == null) surface = GetComponent<PoiPaperSurface>();
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (tearSystem == null) tearSystem = GetComponent<PoiPaperTearSystem>();
        }

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
