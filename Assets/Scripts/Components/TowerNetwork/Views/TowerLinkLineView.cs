using UnityEngine;

namespace TowerDefense3D.Towers
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class TowerLinkLineView : MonoBehaviour
    {
        private LineRenderer lineRenderer;

        public void Initialize(Material material, float width, bool loop, int positionCount)
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.sharedMaterial = material;
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = loop;
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

        public void ShowRing(Vector3 center, float radius, Color color)
        {
            for (int index = 0; index < lineRenderer.positionCount; index++)
            {
                float angle = (Mathf.PI * 2f * index) / lineRenderer.positionCount;
                lineRenderer.SetPosition(index, center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius));
            }

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
