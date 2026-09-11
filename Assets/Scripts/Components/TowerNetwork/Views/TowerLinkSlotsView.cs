using UnityEngine;

namespace TowerDefense3D.Towers
{
    /// <summary>
    /// Shows one square per incoming link port on a tower: green once a link occupies the port,
    /// red while it is still free.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerLinkSlotsView : MonoBehaviour, ITowerLinkSlotsView
    {
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

        [Tooltip("Turned to face the camera every frame. Holds the slot squares.")]
        [SerializeField] private Transform billboardRoot;

        [Tooltip("One renderer per input port, ordered left to right.")]
        [SerializeField] private Renderer[] slots;

        [SerializeField] private Color freeColor = new Color(2.4f, 0.14f, 0.1f, 1f);
        [SerializeField] private Color occupiedColor = new Color(0.16f, 2f, 0.42f, 1f);

        /// <summary>
        /// Exposed so the tower can leave the indicator out of its own silhouette measurement.
        /// </summary>
        public Transform BillboardRoot => billboardRoot;

        private MaterialPropertyBlock properties;
        private Camera billboardCamera;
        private int renderedSlotCount = -1;

        /// <summary>
        /// Colours the first <paramref name="occupiedSlotCount"/> squares as occupied and the rest
        /// as free. Called every frame, so it early-outs on an unchanged count.
        /// </summary>
        /// <remarks>
        /// Tinting goes through a property block rather than through <c>renderer.material</c>,
        /// which would instantiate a throwaway material per tower per frame.
        /// </remarks>
        public void Render(int occupiedSlotCount)
        {
            if (slots == null || slots.Length == 0)
            {
                return;
            }

            int occupied = Mathf.Clamp(occupiedSlotCount, 0, slots.Length);
            if (occupied == renderedSlotCount)
            {
                return;
            }

            renderedSlotCount = occupied;
            properties ??= new MaterialPropertyBlock();
            for (int index = 0; index < slots.Length; index++)
            {
                Renderer slot = slots[index];
                if (slot == null)
                {
                    continue;
                }

                properties.SetColor(BaseColorProperty, index < occupied ? occupiedColor : freeColor);
                slot.SetPropertyBlock(properties);
            }
        }

        /// <summary>
        /// Re-applied every frame because the tower itself turns to look down its link and would
        /// otherwise drag the squares edge-on to the camera.
        /// </summary>
        private void LateUpdate()
        {
            if (billboardRoot == null)
            {
                return;
            }

            if (billboardCamera == null)
            {
                // Null while the level is still building up or already tearing down.
                billboardCamera = Camera.main;
                if (billboardCamera == null)
                {
                    return;
                }
            }

            Transform cameraTransform = billboardCamera.transform;
            billboardRoot.rotation = Quaternion.LookRotation(cameraTransform.forward, cameraTransform.up);
        }
    }
}
