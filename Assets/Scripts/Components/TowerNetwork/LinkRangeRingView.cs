using TowerDefense3D.Components.Core;
using TowerDefense3D.Towers;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Draws a reach as a flat disc on the board: a pale wash inside, a thin bright rim, and four
    /// diamonds sitting on the rim at north, east, south and west.
    /// </summary>
    /// <remarks>
    /// The wash and the rim are two renderers because they are two colours, and a renderer carries
    /// one. The diamonds ride in the rim's mesh: same colour, same idea - the outline of the reach -
    /// so they cost no extra draw.
    ///
    /// The wash sits a shade lower than the rim so an overhead camera sorts it behind both the rim
    /// and anything else drawn on the ground, rather than fighting them for the same depth.
    ///
    /// Meshes are rebuilt only when the radius actually changes. A tower whose reach is edited, or
    /// upgraded, gets a new circle the next time it is shown; one shown repeatedly at the same
    /// reach rebuilds nothing.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class LinkRangeRingView : MonoBehaviour, ILinkRangeView
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Renderers")]
        [SerializeField] private MeshFilter rimMeshFilter;
        [SerializeField] private MeshRenderer rimRenderer;
        [SerializeField] private MeshFilter fillMeshFilter;
        [SerializeField] private MeshRenderer fillRenderer;

        [Header("Look")]
        [Tooltip("The rim and the four cardinal markers.")]
        [SerializeField] private Color rimColor = new Color(0.96f, 0.98f, 1f, 0.90f);

        [Tooltip("The wash inside the rim. White rather than black: lifting the reach reads as ground being lit up for the player, where darkening it read as ground being switched off.")]
        [SerializeField] private Color fillColor = new Color(1f, 1f, 1f, 0.28f);

        [Tooltip("Rim width in metres. About sixty screen pixels make a metre at the game camera, so a tenth of a metre is a drawn line and half a metre is a painted band.")]
        [SerializeField, Min(0.02f)] private float rimThickness = 0.12f;

        [Tooltip("Half-width of the diamonds on the rim. They give the circle a read at a glance and mark the axes the board is built on.")]
        [SerializeField, Min(0f)] private float markerRadius = 0.5f;

        [SerializeField, Range(16, 256)] private int segments = 96;

        [Tooltip("Lifts the ring off the ground so it is not co-planar with the board and does not z-fight it.")]
        [SerializeField, Min(0f)] private float surfaceOffset = 0.025f;

        private Mesh rimMesh;
        private Mesh fillMesh;
        private MaterialPropertyBlock rimProperties;
        private MaterialPropertyBlock fillProperties;
        private float cachedRadius = -1f;

        private void Awake()
        {
            EnsureMeshes();
            SetVisible(false);
        }

        public void Show(Vector3 centre, float radiusMeters)
        {
            if (radiusMeters <= 0f)
            {
                Hide();
                return;
            }

            EnsureMeshes();
            if (!Mathf.Approximately(cachedRadius, radiusMeters))
            {
                cachedRadius = radiusMeters;
                BuildRim(radiusMeters);
                BuildFill(radiusMeters);
            }

            transform.position = centre;
            ApplyColors();
            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        /// <summary>
        /// The rim: an annulus, plus four diamonds on the axes carried in the same mesh.
        /// </summary>
        private void BuildRim(float radius)
        {
            int count = Mathf.Max(16, segments);
            float outer = radius;
            float inner = Mathf.Max(0f, radius - rimThickness);

            // Room for the ring plus four diamonds of four vertices each.
            var vertices = new Vector3[(count * 2) + 16];
            var uvs = new Vector2[(count * 2) + 16];
            var triangles = new int[(count * 6) + 24];

            for (int index = 0; index < count; index++)
            {
                float angle = index / (float)count * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                vertices[index * 2] = new Vector3(cos * inner, surfaceOffset, sin * inner);
                vertices[(index * 2) + 1] = new Vector3(cos * outer, surfaceOffset, sin * outer);
                uvs[index * 2] = new Vector2(index / (float)count, 0f);
                uvs[(index * 2) + 1] = new Vector2(index / (float)count, 1f);
            }

            for (int index = 0; index < count; index++)
            {
                int next = (index + 1) % count;
                int innerNow = index * 2;
                int outerNow = innerNow + 1;
                int innerNext = next * 2;
                int outerNext = innerNext + 1;
                int triangle = index * 6;

                // Wound so the ring faces up, the same way the footprint quads are wound. Wound
                // the other way it is a perfectly good mesh that is simply back-facing, so it
                // renders as nothing at all from a camera looking down at the board.
                triangles[triangle] = innerNow;
                triangles[triangle + 1] = outerNext;
                triangles[triangle + 2] = outerNow;
                triangles[triangle + 3] = innerNow;
                triangles[triangle + 4] = innerNext;
                triangles[triangle + 5] = outerNext;
            }

            AppendMarkers(radius, vertices, uvs, triangles, count);

            rimMesh.Clear();
            rimMesh.vertices = vertices;
            rimMesh.triangles = triangles;
            rimMesh.uv = uvs;
            rimMesh.RecalculateBounds();
            rimMesh.RecalculateNormals();
        }

        private void AppendMarkers(
            float radius,
            Vector3[] vertices,
            Vector2[] uvs,
            int[] triangles,
            int count)
        {
            int vertex = count * 2;
            int triangle = count * 6;
            float half = markerRadius;
            Vector3[] centres =
            {
                new Vector3(0f, surfaceOffset, radius),
                new Vector3(radius, surfaceOffset, 0f),
                new Vector3(0f, surfaceOffset, -radius),
                new Vector3(-radius, surfaceOffset, 0f)
            };

            for (int index = 0; index < centres.Length; index++)
            {
                Vector3 centre = centres[index];

                // A zero marker size still writes degenerate triangles rather than leaving stale
                // vertices from a previous, larger radius in the buffer.
                vertices[vertex] = centre + new Vector3(0f, 0f, half);
                vertices[vertex + 1] = centre + new Vector3(half, 0f, 0f);
                vertices[vertex + 2] = centre + new Vector3(0f, 0f, -half);
                vertices[vertex + 3] = centre + new Vector3(-half, 0f, 0f);
                uvs[vertex] = new Vector2(0.5f, 1f);
                uvs[vertex + 1] = new Vector2(1f, 0.5f);
                uvs[vertex + 2] = new Vector2(0.5f, 0f);
                uvs[vertex + 3] = new Vector2(0f, 0.5f);

                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 1;
                triangles[triangle + 2] = vertex + 2;
                triangles[triangle + 3] = vertex;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;

                vertex += 4;
                triangle += 6;
            }
        }

        /// <summary>The wash: a fan from the centre out to the same circle.</summary>
        private void BuildFill(float radius)
        {
            int count = Mathf.Max(16, segments);
            float height = surfaceOffset * 0.4f;

            var vertices = new Vector3[count + 1];
            var uvs = new Vector2[count + 1];
            var triangles = new int[count * 3];

            vertices[0] = new Vector3(0f, height, 0f);
            uvs[0] = new Vector2(0.5f, 0.5f);
            for (int index = 0; index < count; index++)
            {
                float angle = index / (float)count * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                vertices[index + 1] = new Vector3(cos * radius, height, sin * radius);
                uvs[index + 1] = new Vector2((cos * 0.5f) + 0.5f, (sin * 0.5f) + 0.5f);
            }

            for (int index = 0; index < count; index++)
            {
                int next = ((index + 1) % count) + 1;
                int triangle = index * 3;

                // Centre, next, current: the order that leaves the fan facing up.
                triangles[triangle] = 0;
                triangles[triangle + 1] = next;
                triangles[triangle + 2] = index + 1;
            }

            fillMesh.Clear();
            fillMesh.vertices = vertices;
            fillMesh.triangles = triangles;
            fillMesh.uv = uvs;
            fillMesh.RecalculateBounds();
            fillMesh.RecalculateNormals();
        }

        private void ApplyColors()
        {
            if (rimProperties == null)
            {
                rimProperties = new MaterialPropertyBlock();
                fillProperties = new MaterialPropertyBlock();
            }

            rimProperties.SetColor(BaseColorId, rimColor);
            rimProperties.SetColor(ColorId, rimColor);
            rimRenderer.SetPropertyBlock(rimProperties);

            fillProperties.SetColor(BaseColorId, fillColor);
            fillProperties.SetColor(ColorId, fillColor);
            fillRenderer.SetPropertyBlock(fillProperties);
        }

        private void EnsureMeshes()
        {
            if (rimMeshFilter == null || rimRenderer == null)
            {
                throw new MissingReferenceException("LinkRangeRingView requires an authored Rim renderer.");
            }

            if (fillMeshFilter == null || fillRenderer == null)
            {
                throw new MissingReferenceException("LinkRangeRingView requires an authored Fill renderer.");
            }

            if (rimMesh == null)
            {
                rimMesh = new Mesh { name = "Link Range Rim" };
                rimMesh.MarkDynamic();
                rimMeshFilter.sharedMesh = rimMesh;
            }

            if (fillMesh == null)
            {
                fillMesh = new Mesh { name = "Link Range Fill" };
                fillMesh.MarkDynamic();
                fillMeshFilter.sharedMesh = fillMesh;
            }

            Configure(rimRenderer);
            Configure(fillRenderer);
        }

        private static void Configure(MeshRenderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        private void SetVisible(bool visible)
        {
            if (rimRenderer != null)
            {
                rimRenderer.enabled = visible;
            }

            if (fillRenderer != null)
            {
                fillRenderer.enabled = visible;
            }
        }

        private void OnDestroy()
        {
            RuntimeObjectDestroyer.Destroy(rimMesh);
            RuntimeObjectDestroyer.Destroy(fillMesh);
        }
    }
}
