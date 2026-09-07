using UnityEngine;

namespace TowerDefense3D.Towers
{
    /// <summary>
    /// One straight run of line: a link between two towers, or the line under a link being
    /// dragged. Circles are not drawn here - see <see cref="TowerSelectionRingView"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class TowerLinkLineView : MonoBehaviour
    {
        private LineRenderer lineRenderer;

        public void Initialize(Material material, float width, int positionCount)
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.sharedMaterial = material;
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = false;
            lineRenderer.positionCount = positionCount;
            lineRenderer.widthMultiplier = width;
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            gameObject.SetActive(false);
        }

        public void ShowLine(Vector3 start, Vector3 end, Color color)
        {
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
            SetColor(color);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void SetColor(Color color)
        {
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
        }
    }
}
