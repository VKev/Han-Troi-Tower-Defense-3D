using TowerDefense3D.Components.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Candidate presentation: the footprint the tower will stand on, the ring showing how far
    /// it will be able to link from there, and nothing above either.
    /// </summary>
    /// <remarks>
    /// The ghost volume that used to rise to the tower's height is built and kept, but never
    /// drawn. What the player is choosing is a cell, and a translucent box floating over the
    /// board read as an object already placed rather than as a target.
    /// </remarks>
    public sealed class GridPlacementView : MonoBehaviour, IGridPlacementView
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Combined renderers")]
        [SerializeField] private MeshFilter footprintMeshFilter;
        [SerializeField] private MeshRenderer footprintRenderer;
        [SerializeField] private MeshFilter ghostMeshFilter;
        [SerializeField] private MeshRenderer ghostRenderer;

        [Tooltip("The reach ring drawn around the candidate cell. Its own component, shared in shape and colour with the one tower selection puts around a tower already standing.")]
        [SerializeField] private LinkRangeRingView linkRangeRing;

        [Header("World projection")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private LayerMask placementSurfaceMask = ~0;
        [SerializeField, Min(1f)] private float maxRayDistance = 500f;

        [Header("Candidate colors")]
        [SerializeField] private Color validColor = new Color(0.15f, 1f, 0.25f, 0.38f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.15f, 0.12f, 0.38f);
        [SerializeField, Range(0f, 0.25f)] private float cellInset = 0.06f;
        [SerializeField, Min(0f)] private float surfaceOffset = 0.025f;
        [SerializeField, Range(0f, 1f)] private float ghostAlphaMultiplier = 0.35f;

        [Header("Finger offset")]
        [Tooltip("How far above the pointer the board is sampled during a drag, as a fraction of screen height. A fingertip sits over roughly a tenth of a phone screen, so the cell being chosen is otherwise the one part of the board the player cannot see.")]
        [SerializeField, Range(0f, 0.3f)] private float fingerOffsetUp = 0.10f;

        [Tooltip("How far left of the pointer the board is sampled during a drag, as a fraction of screen height.")]
        [SerializeField, Range(0f, 0.3f)] private float fingerOffsetLeft = 0.04f;

        private Mesh footprintMesh;
        private Mesh ghostMesh;
        private MaterialPropertyBlock footprintProperties;
        private MaterialPropertyBlock ghostProperties;
        private int cachedWidth = -1;
        private int cachedDepth = -1;
        private int cachedHeight = -1;
        private float cachedCellSize = -1f;
        private float cachedHeightUnit = -1f;

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            EnsureRenderers();
            SetVisible(false);
        }

        public Camera WorldCamera => worldCamera;

        public bool TryGetWorldPoint(Vector2 screenPosition, bool offsetForFinger, out Vector3 worldPoint)
        {
            if (worldCamera == null)
            {
                worldPoint = default;
                return false;
            }

            if (offsetForFinger)
            {
                screenPosition = ApplyFingerOffset(screenPosition);
            }

            Ray ray = worldCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    maxRayDistance,
                    placementSurfaceMask,
                    QueryTriggerInteraction.Ignore))
            {
                worldPoint = default;
                return false;
            }

            worldPoint = hit.point;
            return true;
        }

        /// <summary>
        /// Moves the sampled point up and to the left of the pointer, out from under the finger.
        /// </summary>
        /// <remarks>
        /// Measured against screen height rather than in raw pixels, because a fingertip covers a
        /// roughly fixed fraction of a phone screen whatever its resolution - a pixel offset tuned
        /// on one device would be half a finger on the next and two fingers on the one after.
        ///
        /// Up and left rather than straight up: a right hand comes at the screen from the lower
        /// right, so the knuckle trails below and to the right of the fingertip and clears more of
        /// the board on that diagonal.
        /// </remarks>
        private Vector2 ApplyFingerOffset(Vector2 screenPosition)
        {
            // The camera's own pixel height, not Screen.height. They are the same number in a
            // normal build, and only the camera's is right when they are not - rendering to a
            // texture, or a camera that does not own the whole display.
            float unit = worldCamera.pixelHeight;
            return new Vector2(
                screenPosition.x - (fingerOffsetLeft * unit),
                screenPosition.y + (fingerOffsetUp * unit));
        }

        public void Show(
            TowerFootprint footprint,
            Vector3 footprintBottomCenter,
            float cellSize,
            float heightUnit,
            float linkRangeMeters,
            bool isValid)
        {
            if (footprint.Width <= 0 || footprint.Depth <= 0 || footprint.Height <= 0)
            {
                Hide();
                return;
            }

            EnsureRenderers();
            if (NeedsRebuild(footprint, cellSize, heightUnit))
            {
                RebuildMeshes(footprint, cellSize, heightUnit);
            }

            transform.position = footprintBottomCenter;

            // Moved after this object, never before it. The ring is a child and is placed in world
            // space, so setting it first and then moving the parent carries it away by however far
            // the parent travelled - which puts it off the board entirely on the second candidate.
            if (linkRangeRing != null)
            {
                linkRangeRing.Show(footprintBottomCenter, linkRangeMeters);
            }

            ApplyColor(isValid ? validColor : invalidColor);
            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private bool NeedsRebuild(TowerFootprint footprint, float cellSize, float heightUnit)
        {
            return cachedWidth != footprint.Width
                || cachedDepth != footprint.Depth
                || cachedHeight != footprint.Height
                || !Mathf.Approximately(cachedCellSize, cellSize)
                || !Mathf.Approximately(cachedHeightUnit, heightUnit);
        }

        private void RebuildMeshes(TowerFootprint footprint, float cellSize, float heightUnit)
        {
            cachedWidth = footprint.Width;
            cachedDepth = footprint.Depth;
            cachedHeight = footprint.Height;
            cachedCellSize = cellSize;
            cachedHeightUnit = heightUnit;

            BuildFootprintMesh(footprint.Width, footprint.Depth, cellSize);
            BuildGhostMesh(
                footprint.Width * cellSize,
                footprint.Height * heightUnit,
                footprint.Depth * cellSize);
        }

        private void BuildFootprintMesh(int width, int depth, float cellSize)
        {
            int cellCount = width * depth;
            var vertices = new Vector3[cellCount * 4];
            var triangles = new int[cellCount * 6];
            var uvs = new Vector2[cellCount * 4];
            float halfWidth = width * cellSize * 0.5f;
            float halfDepth = depth * cellSize * 0.5f;
            float inset = Mathf.Min(cellInset, cellSize * 0.25f);

            int cellIndex = 0;
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++, cellIndex++)
                {
                    float x0 = -halfWidth + (x * cellSize) + inset;
                    float x1 = -halfWidth + ((x + 1) * cellSize) - inset;
                    float z0 = -halfDepth + (z * cellSize) + inset;
                    float z1 = -halfDepth + ((z + 1) * cellSize) - inset;
                    int vertex = cellIndex * 4;
                    int triangle = cellIndex * 6;

                    vertices[vertex] = new Vector3(x0, surfaceOffset, z0);
                    vertices[vertex + 1] = new Vector3(x0, surfaceOffset, z1);
                    vertices[vertex + 2] = new Vector3(x1, surfaceOffset, z1);
                    vertices[vertex + 3] = new Vector3(x1, surfaceOffset, z0);
                    uvs[vertex] = new Vector2(0f, 0f);
                    uvs[vertex + 1] = new Vector2(0f, 1f);
                    uvs[vertex + 2] = new Vector2(1f, 1f);
                    uvs[vertex + 3] = new Vector2(1f, 0f);
                    triangles[triangle] = vertex;
                    triangles[triangle + 1] = vertex + 1;
                    triangles[triangle + 2] = vertex + 2;
                    triangles[triangle + 3] = vertex;
                    triangles[triangle + 4] = vertex + 2;
                    triangles[triangle + 5] = vertex + 3;
                }
            }

            footprintMesh.Clear();
            footprintMesh.vertices = vertices;
            footprintMesh.triangles = triangles;
            footprintMesh.uv = uvs;
            footprintMesh.RecalculateBounds();
            footprintMesh.RecalculateNormals();
        }

        private void BuildGhostMesh(float width, float height, float depth)
        {
            float halfWidth = width * 0.5f;
            float halfDepth = depth * 0.5f;
            var vertices = new[]
            {
                new Vector3(-halfWidth, 0f, -halfDepth),
                new Vector3(-halfWidth, 0f, halfDepth),
                new Vector3(halfWidth, 0f, halfDepth),
                new Vector3(halfWidth, 0f, -halfDepth),
                new Vector3(-halfWidth, height, -halfDepth),
                new Vector3(-halfWidth, height, halfDepth),
                new Vector3(halfWidth, height, halfDepth),
                new Vector3(halfWidth, height, -halfDepth)
            };
            var triangles = new[]
            {
                0, 1, 2, 0, 2, 3,
                4, 6, 5, 4, 7, 6,
                0, 4, 5, 0, 5, 1,
                1, 5, 6, 1, 6, 2,
                2, 6, 7, 2, 7, 3,
                3, 7, 4, 3, 4, 0
            };

            ghostMesh.Clear();
            ghostMesh.vertices = vertices;
            ghostMesh.triangles = triangles;
            ghostMesh.RecalculateBounds();
            ghostMesh.RecalculateNormals();
        }

        private void ApplyColor(Color color)
        {
            footprintProperties.SetColor(BaseColorId, color);
            footprintProperties.SetColor(ColorId, color);
            footprintRenderer.SetPropertyBlock(footprintProperties);

            color.a *= ghostAlphaMultiplier;
            ghostProperties.SetColor(BaseColorId, color);
            ghostProperties.SetColor(ColorId, color);
            ghostRenderer.SetPropertyBlock(ghostProperties);

            // The ring is never tinted by validity, and is not tinted here at all: how far the
            // tower reaches is the same answer whether or not this cell will take it, so the ring
            // owns its own colours.
        }

        private void EnsureRenderers()
        {
            if (footprintProperties == null)
            {
                footprintProperties = new MaterialPropertyBlock();
                ghostProperties = new MaterialPropertyBlock();
            }

            if (footprintMeshFilter == null || footprintRenderer == null)
            {
                throw new MissingReferenceException("GridPlacementView requires an authored Footprint renderer.");
            }

            if (ghostMeshFilter == null || ghostRenderer == null)
            {
                throw new MissingReferenceException("GridPlacementView requires an authored GhostVolume renderer.");
            }

            if (footprintMesh == null)
            {
                footprintMesh = new Mesh { name = "Grid Placement Footprint Preview" };
                footprintMesh.MarkDynamic();
                footprintMeshFilter.sharedMesh = footprintMesh;
            }

            if (ghostMesh == null)
            {
                ghostMesh = new Mesh { name = "Grid Placement Ghost Preview" };
                ghostMesh.MarkDynamic();
                ghostMeshFilter.sharedMesh = ghostMesh;
            }

            ConfigureRenderer(footprintRenderer);
            ConfigureRenderer(ghostRenderer);

        }

        private static void ConfigureRenderer(MeshRenderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        private void SetVisible(bool visible)
        {
            if (footprintRenderer != null)
            {
                footprintRenderer.enabled = visible;
            }

            if (ghostRenderer != null)
            {
                // Never shown: only the footprint marks the candidate.
                ghostRenderer.enabled = false;
            }

            if (!visible && linkRangeRing != null)
            {
                linkRangeRing.Hide();
            }
        }

        private void OnDestroy()
        {
            RuntimeObjectDestroyer.Destroy(footprintMesh);
            RuntimeObjectDestroyer.Destroy(ghostMesh);
        }
    }
}
