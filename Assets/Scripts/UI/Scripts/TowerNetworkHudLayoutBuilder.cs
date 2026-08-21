using System;
using System.Collections.Generic;
using TowerDefense3D.Towers;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    internal sealed class TowerNetworkHudControls
    {
        public TowerNetworkHudControls(
            GameObject root,
            IReadOnlyList<Button> towerButtons,
            Button unlinkButton,
            Button startWaveButton,
            Text selectedText,
            Text chainText,
            Text queueText,
            Text feedbackText)
        {
            Root = root;
            TowerButtons = towerButtons;
            UnlinkButton = unlinkButton;
            StartWaveButton = startWaveButton;
            SelectedText = selectedText;
            ChainText = chainText;
            QueueText = queueText;
            FeedbackText = feedbackText;
        }

        public GameObject Root { get; }
        public IReadOnlyList<Button> TowerButtons { get; }
        public Button UnlinkButton { get; }
        public Button StartWaveButton { get; }
        public Text SelectedText { get; }
        public Text ChainText { get; }
        public Text QueueText { get; }
        public Text FeedbackText { get; }
    }

    internal static class TowerNetworkHudLayoutBuilder
    {
        private static readonly Color PanelColor = new Color(0.035f, 0.055f, 0.08f, 0.9f);
        private static readonly Color ButtonColor = new Color(0.12f, 0.2f, 0.3f, 0.98f);
        private static readonly Color PrimaryButtonColor = new Color(0.12f, 0.5f, 0.28f, 1f);
        private static readonly Color DangerButtonColor = new Color(0.55f, 0.18f, 0.15f, 1f);

        public static TowerNetworkHudControls Build(Transform safeArea, IReadOnlyList<TowerCombatDefinition> definitions)
        {
            if (safeArea == null)
            {
                throw new ArgumentNullException(nameof(safeArea));
            }

            GameObject root = CreateRectObject(
                "Tower Network HUD",
                safeArea,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(24f, 176f),
                new Vector2(600f, 508f));
            Image background = root.AddComponent<Image>();
            background.color = PanelColor;
            background.raycastTarget = true;

            CreateText(
                "Title",
                root.transform,
                "TOWER NETWORK",
                30,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Vector2(18f, -16f),
                new Vector2(564f, 44f));

            GameObject gridObject = CreateRectObject(
                "Tower Buttons",
                root.transform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -66f),
                new Vector2(564f, 204f));
            GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(276f, 60f);
            grid.spacing = new Vector2(12f, 12f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperLeft;

            var towerButtons = new List<Button>(definitions.Count);
            for (int index = 0; index < definitions.Count; index++)
            {
                TowerCombatDefinition definition = definitions[index];
                string label = definition?.Core?.DisplayName ?? "Missing Tower";
                towerButtons.Add(CreateButton(
                    label,
                    gridObject.transform,
                    label.ToUpperInvariant(),
                    GetFamilyColor(definition),
                    Vector2.zero,
                    grid.cellSize));
            }

            Text selectedText = CreateText(
                "Selected Status",
                root.transform,
                "Selected: None",
                22,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Vector2(18f, -282f),
                new Vector2(564f, 34f));
            Text chainText = CreateText(
                "Chain Status",
                root.transform,
                "Valid chains: 0",
                21,
                FontStyle.Normal,
                TextAnchor.MiddleLeft,
                new Vector2(18f, -320f),
                new Vector2(564f, 32f));
            Text queueText = CreateText(
                "Queue Status",
                root.transform,
                "Queue: --",
                21,
                FontStyle.Normal,
                TextAnchor.MiddleLeft,
                new Vector2(18f, -356f),
                new Vector2(564f, 32f));
            Text feedbackText = CreateText(
                "Network Feedback",
                root.transform,
                "Place towers, then drag one tower to another.",
                19,
                FontStyle.Italic,
                TextAnchor.UpperLeft,
                new Vector2(18f, -394f),
                new Vector2(564f, 48f));
            feedbackText.color = new Color(0.72f, 0.86f, 1f, 1f);

            Button unlinkButton = CreateButton(
                "Unlink",
                root.transform,
                "UNLINK",
                DangerButtonColor,
                new Vector2(18f, 18f),
                new Vector2(270f, 58f));
            Button startWaveButton = CreateButton(
                "Start Wave",
                root.transform,
                "START WAVE",
                PrimaryButtonColor,
                new Vector2(312f, 18f),
                new Vector2(270f, 58f));

            return new TowerNetworkHudControls(
                root,
                towerButtons,
                unlinkButton,
                startWaveButton,
                selectedText,
                chainText,
                queueText,
                feedbackText);
        }

        private static GameObject CreateRectObject(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rectTransform = (RectTransform)gameObject.transform;
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
            return gameObject;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject textObject = CreateRectObject(
                name,
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                anchoredPosition,
                sizeDelta);
            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = value;
            return text;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Color color,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject buttonObject = CreateRectObject(
                name,
                parent,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                anchoredPosition,
                sizeDelta);
            Image image = buttonObject.AddComponent<Image>();
            image.color = color;
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text labelText = CreateText(
                "Label",
                buttonObject.transform,
                label,
                22,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.zero);
            RectTransform labelRect = labelText.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(-12f, -8f);
            return button;
        }

        private static Color GetFamilyColor(TowerCombatDefinition definition)
        {
            if (definition == null)
            {
                return ButtonColor;
            }

            switch (definition.Family)
            {
                case TowerFamily.Fire:
                    return new Color(0.68f, 0.16f, 0.08f, 1f);
                case TowerFamily.Water:
                    return new Color(0.08f, 0.32f, 0.68f, 1f);
                case TowerFamily.Wind:
                    return new Color(0.12f, 0.5f, 0.38f, 1f);
                case TowerFamily.Earth:
                    return new Color(0.5f, 0.3f, 0.1f, 1f);
                case TowerFamily.SoulNexus:
                    return new Color(0.42f, 0.18f, 0.58f, 1f);
                default:
                    return ButtonColor;
            }
        }
    }
}
