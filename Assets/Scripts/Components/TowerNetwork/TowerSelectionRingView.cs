using TowerDefense3D.Components.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerDefense3D.Towers
{
    /// <summary>
    /// The bracket drawn flat on the board around the selected tower: a circle broken into a few
    /// arcs with rounded ends, in a green bright enough to bloom.
    /// </summary>
    /// <remarks>
    /// A mesh rather than a line renderer, for two reasons. A line renderer's colour goes through
    /// an eight-bit gradient, so it cannot carry a value above one and cannot reach the bloom
    /// threshold - the halo around the ring is the post-process picking up an over-bright colour,
    /// and there is no way to hand it one through a gradient. And a broken circle is four separate
    /// strokes; a looping line renderer draws one.
    ///
    /// The circle is broken rather than closed because a closed ring reads as a wall around the
    /// tower. Gaps make it read as a bracket pointing at the tower, which is what a selection is.
    ///
    /// Drawn on the ground at the tower's foot, not floating at its head, so it sits under the
    /// tower and is occluded by it - which is what puts the tower inside the ring rather than in
    /// front of a decal.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class TowerSelectionRingView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Renderer")]
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;

        [Header("Look")]
        [Tooltip("Green past one so the bloom in the level's volume profile picks it up. The halo is the glow; the colour itself is only the core of the stroke.")]
        [SerializeField, ColorUsage(true, true)]
        private Color ringColor = new Color(0.30f, 2.60f, 0.65f, 1f);

        [Tooltip("Stroke width in metres.")]
        [SerializeField, Min(0.01f)] private float thickness = 0.09f;

        [Tooltip("How many arcs the circle is broken into.")]
        [SerializeField, Range(1, 12)] private int arcCount = 4;

        [Tooltip("Width of each gap, in degrees. The arcs share out whatever is left.")]
        [SerializeField, Range(0f, 60f)] private float gapDegrees = 26f;

        [Tooltip("Turns the whole bracket. This is what places the gaps against the board's axes rather than on them.")]
        [SerializeField, Range(0f, 360f)] private float rotationDegrees = 45f;

        [Tooltip("Subdivisions along each arc. Enough that the arc reads as a curve, not a chord.")]
        [SerializeField, Range(2, 64)] private int stepsPerArc = 20;

        [Tooltip("Subdivisions in each rounded end. Four already reads as round at this stroke width.")]
        [SerializeField, Range(1, 16)] private int stepsPerCap = 6;

        [Tooltip("Lifts the ring off the board so it does not z-fight the ground it lies on.")]
        [SerializeField, Min(0f)] private float surfaceOffset = 0.03f;

        private Mesh mesh;
        private MaterialPropertyBlock properties;
        private float cachedRadius = -1f;

        private void Awake()
        {
            EnsureMesh();
            SetVisible(false);
        }

        public void Show(Vector3 centre, float radius)
        {
            if (radius <= 0f)
            {
                Hide();
                return;
            }

            EnsureMesh();

            // Rebuilt only when the radius actually moves, so a tower shown again at the same size
            // rebuilds nothing.
            if (!Mathf.Approximately(cachedRadius, radius))
            {
                cachedRadius = radius;
                Build(radius);
            }

            transform.position = centre;
            ApplyColor();
            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        /// <summary>
        /// Lays out the arcs: each one a strip between an inner and an outer circle, closed at
        /// both ends by a half-disc so the stroke ends round rather than cut square.
        /// </summary>
        private void Build(float radius)
        {
            int arcs = Mathf.Max(1, arcCount);
            int arcSteps = Mathf.Max(2, stepsPerArc);
            int capSteps = Mathf.Max(1, stepsPerCap);
            float half = thickness * 0.5f;
            float inner = Mathf.Max(0.001f, radius - half);
            float outer = radius + half;

            float slice = 360f / arcs;
            float gap = Mathf.Clamp(gapDegrees, 0f, slice - 1f);
            float sweep = slice - gap;

            int vertsPerArc = ((arcSteps + 1) * 2) + (2 * (capSteps + 2));
            int trisPerArc = (arcSteps * 6) + (2 * capSteps * 3);
            var vertices = new Vector3[arcs * vertsPerArc];
            var uvs = new Vector2[arcs * vertsPerArc];
            var triangles = new int[arcs * trisPerArc];

            int vertex = 0;
            int triangle = 0;
            for (int arc = 0; arc < arcs; arc++)
            {
                float centreDegrees = rotationDegrees + (arc * slice);
                float startDegrees = centreDegrees - (sweep * 0.5f);
                float endDegrees = centreDegrees + (sweep * 0.5f);

                int stripStart = vertex;
                for (int step = 0; step <= arcSteps; step++)
                {
                    float degrees = Mathf.Lerp(startDegrees, endDegrees, step / (float)arcSteps);
                    float radians = degrees * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(radians);
                    float sin = Mathf.Sin(radians);
                    float along = step / (float)arcSteps;

                    vertices[vertex] = new Vector3(cos * inner, surfaceOffset, sin * inner);
                    vertices[vertex + 1] = new Vector3(cos * outer, surfaceOffset, sin * outer);
                    uvs[vertex] = new Vector2(along, 0f);
                    uvs[vertex + 1] = new Vector2(along, 1f);
                    vertex += 2;
                }

                for (int step = 0; step < arcSteps; step++)
                {
                    int innerNow = stripStart + (step * 2);
                    int outerNow = innerNow + 1;
                    int innerNext = innerNow + 2;
                    int outerNext = innerNow + 3;

                    // Wound to face up, the same way the reach ring and the footprint quads are.
                    triangles[triangle] = innerNow;
                    triangles[triangle + 1] = outerNext;
                    triangles[triangle + 2] = outerNow;
                    triangles[triangle + 3] = innerNow;
                    triangles[triangle + 4] = innerNext;
                    triangles[triangle + 5] = outerNext;
                    triangle += 6;
                }

                // The ends bulge away from the arc, so each cap pushes out along the tangent at
                // its own end - backwards at the start, forwards at the finish.
                AppendCap(
                    radius, startDegrees, -1f, half, capSteps, vertices, uvs, triangles,
                    ref vertex, ref triangle);
                AppendCap(
                    radius, endDegrees, 1f, half, capSteps, vertices, uvs, triangles,
                    ref vertex, ref triangle);
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
        }

        /// <summary>
        /// A half-disc closing one end of an arc, as a fan.
        /// </summary>
        /// <remarks>
        /// The sweep runs in the frame of the direction the cap bulges, with its second axis taken
        /// as up cross bulge. That pairing is what makes every cap wind the same way up whichever
        /// end of whichever arc it closes, rather than half of them coming out back-facing and
        /// rendering as nothing.
        /// </remarks>
        private void AppendCap(
            float radius,
            float degrees,
            float direction,
            float capRadius,
            int capSteps,
            Vector3[] vertices,
            Vector2[] uvs,
            int[] triangles,
            ref int vertex,
            ref int triangle)
        {
            float radians = degrees * Mathf.Deg2Rad;
            var radial = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
            var tangent = new Vector3(-Mathf.Sin(radians), 0f, Mathf.Cos(radians));
            Vector3 bulge = tangent * direction;
            Vector3 side = Vector3.Cross(Vector3.up, bulge);
            Vector3 centre = (radial * radius) + (Vector3.up * surfaceOffset);

            int fanCentre = vertex;
            vertices[vertex] = centre;
            uvs[vertex] = new Vector2(0.5f, 0.5f);
            vertex++;

            for (int step = 0; step <= capSteps; step++)
            {
                float angle = Mathf.Lerp(-90f, 90f, step / (float)capSteps) * Mathf.Deg2Rad;
                vertices[vertex] = centre
                    + (bulge * (Mathf.Cos(angle) * capRadius))
                    + (side * (Mathf.Sin(angle) * capRadius));
                uvs[vertex] = new Vector2(
                    (Mathf.Cos(angle) * 0.5f) + 0.5f,
                    (Mathf.Sin(angle) * 0.5f) + 0.5f);
                vertex++;
            }

            for (int step = 0; step < capSteps; step++)
            {
                triangles[triangle] = fanCentre;
                triangles[triangle + 1] = fanCentre + 1 + step;
                triangles[triangle + 2] = fanCentre + 2 + step;
                triangle += 3;
            }
        }

        private void ApplyColor()
        {
            if (properties == null)
            {
                properties = new MaterialPropertyBlock();
            }

            properties.SetColor(BaseColorId, ringColor);
            properties.SetColor(ColorId, ringColor);
            meshRenderer.SetPropertyBlock(properties);
        }

        private void EnsureMesh()
        {
            if (meshFilter == null)
            {
                meshFilter = GetComponentInChildren<MeshFilter>(true);
            }

            if (meshRenderer == null)
            {
                meshRenderer = GetComponentInChildren<MeshRenderer>(true);
            }

            if (mesh == null)
            {
                mesh = new Mesh { name = "Tower Selection Ring" };
                mesh.MarkDynamic();
                meshFilter.sharedMesh = mesh;
            }

            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        private void SetVisible(bool visible)
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = visible;
            }
        }

        private void OnDestroy()
        {
            RuntimeObjectDestroyer.Destroy(mesh);
        }
    }
}
