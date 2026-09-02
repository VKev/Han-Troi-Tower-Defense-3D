using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GameFlow.Editor
{
    /// <summary>
    /// Lifts the application overlays out of the safe area so their backdrops run edge to edge -
    /// under the notch included - instead of leaving a border of whatever sits behind the screen.
    ///
    /// Only the screen-filling ones move. Everything each of them says sits in a centred card far
    /// narrower than the display, so nothing readable ends up under a cutout. The save warning
    /// stays inset because it is a banner pinned to the top edge, which is exactly where a notch
    /// is, and a banner sliding under one would be unreadable.
    /// </summary>
    public static class ApplicationUiOverlayLayout
    {
        public const string PrefabPath = "Assets/Resources/Prefabs/ApplicationUI.prefab";

        /// <summary>
        /// Canvas children in draw order, back to front. The journey map is the backdrop, the safe
        /// area holds the chrome, and the overlays cover both - the input blocker last of all,
        /// which is the order they were in inside the safe area.
        /// </summary>
        private static readonly string[] DrawOrder =
        {
            "Journey Map",
            "Safe Area",
            "Loading",
            "Blocking Error",
            "Input Blocker"
        };

        /// <summary>The overlays that fill the screen, and so belong outside the safe area.</summary>
        private static readonly string[] FullBleedOverlays =
        {
            "Loading",
            "Blocking Error",
            "Input Blocker"
        };

        [MenuItem("Tools/Tower Defense/Rebuild Application UI Full-Bleed Overlays")]
        public static void RebuildFromMenu()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
            {
                Debug.LogError("Application UI prefab is missing at " + PrefabPath);
                return;
            }

            try
            {
                Transform safeArea = root.transform.Find("Safe Area");
                if (safeArea == null)
                {
                    Debug.LogError("Application UI prefab has no Safe Area.");
                    return;
                }

                for (int index = 0; index < FullBleedOverlays.Length; index++)
                {
                    Lift(root.transform, safeArea, FullBleedOverlays[index]);
                }

                for (int index = 0; index < DrawOrder.Length; index++)
                {
                    Transform child = root.transform.Find(DrawOrder[index]);
                    if (child != null)
                    {
                        child.SetSiblingIndex(index);
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("Application UI overlays rebuilt full bleed.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Moves one overlay onto the canvas and stretches it over the whole display. Re-parenting
        /// rather than rebuilding, so the view wiring already on it survives untouched.
        /// </summary>
        private static void Lift(Transform canvas, Transform safeArea, string name)
        {
            Transform overlay = canvas.Find(name) ?? safeArea.Find(name);
            if (overlay == null)
            {
                Debug.LogWarning("Application UI prefab has no overlay named " + name + ".");
                return;
            }

            if (overlay.parent != canvas)
            {
                overlay.SetParent(canvas, false);
            }

            var rect = (RectTransform)overlay;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            // A plain quad, not the sliced rounded sprite the panels use: at full screen that
            // sprite rounds the display corners and leaves a hairline of the screen behind along
            // every edge.
            var backdrop = overlay.GetComponent<UnityEngine.UI.Image>();
            if (backdrop != null)
            {
                backdrop.sprite = null;
                backdrop.type = UnityEngine.UI.Image.Type.Simple;
            }
        }
    }
}
