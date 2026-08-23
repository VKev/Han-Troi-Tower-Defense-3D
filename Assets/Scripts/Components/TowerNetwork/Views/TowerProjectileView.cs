using UnityEngine;

namespace TowerDefense3D.Towers
{
    [DisallowMultipleComponent]
    public sealed class TowerProjectileView : MonoBehaviour
    {
        private LineRenderer lineRenderer;

        public long ProjectileId { get; private set; }

        public void Initialize(Material sharedMaterial)
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            lineRenderer.sharedMaterial = sharedMaterial;
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = false;
            lineRenderer.positionCount = 2;
            lineRenderer.widthMultiplier = 0.18f;
            lineRenderer.numCapVertices = 4;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.SetPosition(0, new Vector3(0f, -0.08f, 0f));
            lineRenderer.SetPosition(1, new Vector3(0f, 0.08f, 0f));
            gameObject.SetActive(false);
        }

        public void Show(TowerProjectileSnapshot snapshot)
        {
            Show(
                snapshot.ProjectileId,
                snapshot.Payload.Kind,
                new Vector3(snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z));
        }

        public void Show(long projectileId, ProjectilePayloadKind payloadKind, Vector3 renderedPosition)
        {
            ProjectileId = projectileId;
            transform.position = renderedPosition;
            Color color = GetPayloadColor(payloadKind);
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            gameObject.SetActive(true);
        }

        public void ResetForPool()
        {
            ProjectileId = 0L;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (lineRenderer != null)
            {
                lineRenderer.startColor = Color.white;
                lineRenderer.endColor = Color.white;
            }

            gameObject.SetActive(false);
        }

        private static Color GetPayloadColor(ProjectilePayloadKind kind)
        {
            switch (kind)
            {
                case ProjectilePayloadKind.Fire:
                    return new Color(1f, 0.24f, 0.08f, 1f);
                case ProjectilePayloadKind.Water:
                    return new Color(0.1f, 0.55f, 1f, 1f);
                case ProjectilePayloadKind.Wind:
                    return new Color(0.35f, 1f, 0.65f, 1f);
                case ProjectilePayloadKind.Earth:
                    return new Color(0.72f, 0.42f, 0.16f, 1f);
                default:
                    return new Color(1f, 0.92f, 0.38f, 1f);
            }
        }
    }
}
