using TowerDefense3D.Towers;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class EnemyElementStatusView : MonoBehaviour
    {
        [SerializeField] private Transform iconRoot;
        [SerializeField] private Transform fireIcon;
        [SerializeField] private Transform waterIcon;
        [SerializeField] private Transform earthIcon;
        [SerializeField] private Transform windIcon;
        [SerializeField, Min(0f)] private float reactionDisplaySeconds = 0.5f;
        [SerializeField, Min(0f)] private float reactionIconSpacing = 0.55f;
        [SerializeField, Min(0.01f)] private float iconWorldScale = 0.55f;

        private Quaternion cameraFacingRotation = Quaternion.identity;
        private Transform[] slotIcons;
        private MeshFilter[] slotFilters;
        private Mesh[] slotMeshes;
        private float reactionRemainingSeconds;

        /// <summary>
        /// The board camera never moves, so the billboard rotation is resolved once here instead
        /// of every frame. The icon scale is normalised at the same time so enemies authored at
        /// different scales all show the same icon size.
        /// </summary>
        public void Configure(Camera camera)
        {
            if (camera != null)
            {
                cameraFacingRotation = Quaternion.LookRotation(
                    camera.transform.up,
                    -camera.transform.forward);
            }

            Vector3 parentScale = iconRoot.parent != null ? iconRoot.parent.lossyScale : Vector3.one;
            iconRoot.localScale = new Vector3(
                Normalise(parentScale.x),
                Normalise(parentScale.y),
                Normalise(parentScale.z));
        }

        public void Bind(EnemyElementState state)
        {
            reactionRemainingSeconds = 0f;
            RenderMark(state);
        }

        public void Render(EnemyElementState state, float deltaTime)
        {
            if (reactionRemainingSeconds > 0f)
            {
                reactionRemainingSeconds = Mathf.Max(0f, reactionRemainingSeconds - deltaTime);
                if (reactionRemainingSeconds == 0f)
                {
                    HideAll();
                }

                return;
            }

            RenderMark(state);
        }

        public void ShowReaction(ElementPair pair)
        {
            reactionRemainingSeconds = reactionDisplaySeconds;
            HideAll();

            float halfSpacing = reactionIconSpacing * 0.5f;
            ShowSlot(SlotOf(pair.First), pair.First, new Vector3(-halfSpacing, 0f, 0f));

            // A same-element reaction needs two copies of one icon, but there is only one quad
            // per element. Borrow an idle quad and point it at the same mesh; every show
            // re-assigns the mesh, so the borrowed quad never keeps a stale icon.
            int secondSlot = pair.First == pair.Second
                ? SpareSlotFor(pair.First)
                : SlotOf(pair.Second);
            ShowSlot(secondSlot, pair.Second, new Vector3(halfSpacing, 0f, 0f));
            iconRoot.gameObject.SetActive(true);
        }

        public void Release()
        {
            reactionRemainingSeconds = 0f;
            HideAll();
        }

        private void LateUpdate()
        {
            if (!iconRoot.gameObject.activeSelf)
            {
                return;
            }

            // Re-applied every frame only because the enemy root turns to face its movement
            // direction and would otherwise drag the icons around with it.
            iconRoot.rotation = cameraFacingRotation;
        }

        private float Normalise(float parentScale)
        {
            return parentScale == 0f ? iconWorldScale : iconWorldScale / parentScale;
        }

        private void RenderMark(EnemyElementState state)
        {
            if (state.Phase != EnemyElementPhase.Marked)
            {
                HideAll();
                return;
            }

            HideAll();
            ShowSlot(SlotOf(state.Element), state.Element, Vector3.zero);
            iconRoot.gameObject.SetActive(true);
        }

        private void HideAll()
        {
            fireIcon.gameObject.SetActive(false);
            waterIcon.gameObject.SetActive(false);
            earthIcon.gameObject.SetActive(false);
            windIcon.gameObject.SetActive(false);
            iconRoot.gameObject.SetActive(false);
        }

        private void ShowSlot(int slot, ElementType element, Vector3 localPosition)
        {
            EnsureSlotCache();
            MeshFilter filter = slotFilters[slot];
            Mesh mesh = slotMeshes[SlotOf(element)];
            if (filter != null && mesh != null)
            {
                filter.sharedMesh = mesh;
            }

            Transform icon = slotIcons[slot];
            icon.localPosition = localPosition;
            icon.gameObject.SetActive(true);
        }

        private void EnsureSlotCache()
        {
            if (slotIcons != null)
            {
                return;
            }

            // Indexed by ElementType: Fire, Water, Wind, Earth.
            slotIcons = new[] { fireIcon, waterIcon, windIcon, earthIcon };
            slotFilters = new MeshFilter[slotIcons.Length];
            slotMeshes = new Mesh[slotIcons.Length];
            for (int index = 0; index < slotIcons.Length; index++)
            {
                slotFilters[index] = slotIcons[index].GetComponent<MeshFilter>();
                slotMeshes[index] = slotFilters[index] != null
                    ? slotFilters[index].sharedMesh
                    : null;
            }
        }

        private static int SlotOf(ElementType element)
        {
            return (int)element;
        }

        private static int SpareSlotFor(ElementType element)
        {
            return element == ElementType.Fire ? SlotOf(ElementType.Water) : SlotOf(ElementType.Fire);
        }
    }
}
