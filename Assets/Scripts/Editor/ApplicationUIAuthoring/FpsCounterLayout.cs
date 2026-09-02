using TowerDefense3D.Mobile;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow.Editor
{
    /// <summary>
    /// Authors the frame rate readout into the bottom-left corner of the application canvas.
    ///
    /// It goes on that canvas because Bootstrap owns it and never tears it down, so one readout
    /// covers the level menu and gameplay alike, and it goes inside the safe area because a corner
    /// is exactly where a display cuts its own edges off.
    /// </summary>
    public static class FpsCounterLayout
    {
        public const string PrefabPath = "Assets/Resources/Prefabs/ApplicationUI.prefab";
        private const string CounterName = "Fps Counter";

        private static readonly Color OutlineColor = new Color(0f, 0f, 0f, 0.85f);

        [MenuItem("Tools/Tower Defense/Rebuild FPS Counter")]
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

                Build(safeArea);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("FPS counter rebuilt.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Build(Transform safeArea)
        {
            Transform existing = safeArea.Find(CounterName);
            GameObject owner;
            if (existing != null)
            {
                owner = existing.gameObject;
            }
            else
            {
                owner = new GameObject(
                    CounterName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text));
                owner.transform.SetParent(safeArea, false);
            }

            var rect = (RectTransform)owner.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(14f, 10f);
            rect.sizeDelta = new Vector2(200f, 28f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            // Last, so it draws over whatever screen is up rather than under it.
            rect.SetAsLastSibling();

            Text label = EnsureComponent<Text>(owner);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 20;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = "-- FPS";

            // No panel behind it, because a panel in the corner would cover the screens underneath.
            // An outline carries the number over both the bright backdrop of the menu and the dark
            // panels of the HUD instead.
            Outline outline = EnsureComponent<Outline>(owner);
            outline.effectColor = OutlineColor;
            outline.effectDistance = new Vector2(1.6f, -1.6f);
            outline.useGraphicAlpha = true;

            var view = EnsureComponent<FpsCounterView>(owner);
            SetObjectReference(view, "label", label);
            owner.SetActive(true);
        }

        private static T EnsureComponent<T>(GameObject target)
            where T : Component
        {
            T existing = target.GetComponent<T>();
            return existing != null ? existing : target.AddComponent<T>();
        }

        private static void SetObjectReference(Object owner, string fieldName, Object value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError(owner.GetType().Name + " has no serialized field " + fieldName);
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
