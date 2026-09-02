using System.Collections.Generic;
using TowerDefense3D.Towers;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow.Editor
{
    /// <summary>
    /// Re-authors the gameplay HUD prefab into the prototype screen layout:
    /// status cluster top-left, wave panel top-center, wave preview bottom-left,
    /// selection strip and tower build bar bottom-center, wave controls bottom-right.
    /// Flat colours and builtin sprites only - no production art.
    /// </summary>
    public static class GameplayHudPrototypeLayout
    {
        public const string PrefabPath = "Assets/Resources/Prefabs/GameplayUI.prefab";
        private const string TowerCatalogPath = "Assets/Config/Towers/Catalogs/TowerCatalog.asset";

        private static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.10f, 0.88f);
        private static readonly Color SunkenColor = new Color(0.03f, 0.04f, 0.05f, 0.90f);
        private static readonly Color TextColor = new Color(0.93f, 0.95f, 0.97f, 1f);
        private static readonly Color MutedColor = new Color(0.62f, 0.66f, 0.72f, 1f);
        private static readonly Color AccentColor = new Color(1f, 0.72f, 0.26f, 1f);
        private static readonly Color GoldColor = new Color(1f, 0.83f, 0.31f, 1f);
        private static readonly Color StartWaveColor = new Color(0.84f, 0.15f, 0.13f, 1f);
        private static readonly Color NeutralButtonColor = new Color(0.13f, 0.15f, 0.18f, 0.94f);
        private static readonly Color BonusColor = new Color(1f, 0.90f, 0.66f, 1f);
        private static readonly Color HealthFillColor = new Color(0.33f, 0.83f, 0.42f, 1f);
        private static readonly Color DimColor = new Color(0.01f, 0.02f, 0.03f, 0.78f);
        private static readonly Color CardColor = new Color(0.09f, 0.10f, 0.13f, 0.98f);
        private static readonly Color NextLevelColor = new Color(0.13f, 0.52f, 0.30f, 1f);

        private static readonly Dictionary<string, Color> TowerColorsByName =
            new Dictionary<string, Color>
            {
                { "Generator", new Color(0.30f, 0.32f, 0.36f, 1f) },
                { "Fire", new Color(0.55f, 0.13f, 0.18f, 1f) },
                { "Water", new Color(0.08f, 0.42f, 0.48f, 1f) },
                { "Wind", new Color(0.11f, 0.42f, 0.28f, 1f) },
                { "Soul Nexus", new Color(0.34f, 0.15f, 0.50f, 1f) },
                { "Hero", new Color(0.62f, 0.36f, 0.10f, 1f) }
            };

        private static Font uiFont;
        private static Sprite roundedSprite;

        [MenuItem("Tools/Tower Defense/Rebuild Gameplay HUD Prototype Layout")]
        public static void RebuildFromMenu()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<TowerCatalog>(TowerCatalogPath);
            if (catalog == null)
            {
                Debug.LogError("Tower Catalog is missing at " + TowerCatalogPath);
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
            {
                Debug.LogError("Gameplay UI prefab is missing at " + PrefabPath);
                return;
            }

            try
            {
                uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                Rebuild(root, catalog);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("Gameplay HUD prototype layout rebuilt.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Rebuild(GameObject root, TowerCatalog catalog)
        {
            Transform safeArea = root.transform.Find("Safe Area");
            Transform hud = safeArea.Find("Tower Network HUD");

            StretchToParent((RectTransform)hud);
            RemoveGraphic(hud);
            DeleteChild(hud, "Title");
            DeleteChild(safeArea, "Instructions");

            BuildWavePanel(hud);
            BuildPreviewPanel(hud);
            BuildSelectionStrip(hud);
            BuildTowerBar(hud, catalog);
            BuildWaveControls(hud);
            BuildStatusCluster(safeArea);
            BuildCornerButtons(safeArea);
            BuildLevelOutcomeHud(safeArea);

            // Backdrops first so the text and controls above them stay visible.
            OrderChildren(
                hud,
                "Wave Panel",
                "Preview Panel",
                "Selected Panel",
                "Build Bar",
                "Wave Preview",
                "Chain Status",
                "Queue Status",
                "Selected Status",
                "Network Feedback",
                "Tower Buttons",
                "Tower Actions",
                "Start Wave");
            OrderChildren(
                safeArea,
                "Tower Network HUD",
                "Level Status HUD",
                "Pause Button",
                "Return To Level Menu",
                "Cancel",
                "Outcome HUD");
            WireViews(root, hud);
        }

        private static void BuildWavePanel(Transform hud)
        {
            RectTransform panel = EnsurePanel(hud, "Wave Panel", PanelColor);
            SetRect(
                panel,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -24f),
                new Vector2(640f, 84f));

            Label(panel, "Wave Label", "WAVE", new Vector2(22f, -12f), new Vector2(110f, 18f),
                14, MutedColor, TextAnchor.UpperLeft, FontStyle.Bold);
            Label(panel, "Wave Counter", "01 / 08", new Vector2(22f, -30f), new Vector2(130f, 42f),
                32, TextColor, TextAnchor.UpperLeft, FontStyle.Bold);
            Label(panel, "Status Label", "WAVE PROGRESS", new Vector2(172f, -12f), new Vector2(260f, 18f),
                14, MutedColor, TextAnchor.UpperLeft, FontStyle.Bold);
            Label(panel, "Wave Status", "READY TO START", new Vector2(172f, -32f), new Vector2(260f, 26f),
                20, AccentColor, TextAnchor.UpperLeft, FontStyle.Bold);
            Label(panel, "Enemies Label", "ENEMIES LEFT", new Vector2(468f, -12f), new Vector2(152f, 18f),
                14, MutedColor, TextAnchor.UpperLeft, FontStyle.Bold);
            Label(panel, "Enemies Left", "00", new Vector2(468f, -30f), new Vector2(152f, 42f),
                32, TextColor, TextAnchor.UpperLeft, FontStyle.Bold);

            RectTransform progressBackground = EnsurePanel(panel, "Wave Progress Background", SunkenColor);
            SetRect(
                progressBackground,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(172f, -64f),
                new Vector2(260f, 10f));

            RectTransform progressFill = EnsurePanel(progressBackground, "Wave Progress Fill", AccentColor);
            StretchToParent(progressFill);
            ConfigureFillImage(progressFill.GetComponent<Image>(), AccentColor);
        }

        private static void BuildPreviewPanel(Transform hud)
        {
            RectTransform panel = EnsurePanel(hud, "Preview Panel", PanelColor);
            SetRect(panel, Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(24f, 24f), new Vector2(340f, 200f));
            Label(panel, "Preview Title", "NEXT WAVE PREVIEW", new Vector2(18f, -14f), new Vector2(304f, 20f),
                15, AccentColor, TextAnchor.UpperLeft, FontStyle.Bold);

            Text preview = ConfigureText(
                hud.Find("Wave Preview").GetComponent<Text>(),
                17,
                TextColor,
                TextAnchor.UpperLeft,
                FontStyle.Normal);
            preview.verticalOverflow = VerticalWrapMode.Overflow;
            SetRect((RectTransform)preview.transform, Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(42f, 112f), new Vector2(306f, 76f));

            PlaceFootnote(hud, "Chain Status", 80f);
            PlaceFootnote(hud, "Queue Status", 50f);
        }

        private static void PlaceFootnote(Transform hud, string childName, float bottomOffset)
        {
            Transform child = hud.Find(childName);
            ConfigureText(child.GetComponent<Text>(), 14, MutedColor, TextAnchor.UpperLeft, FontStyle.Normal);
            SetRect((RectTransform)child, Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(42f, bottomOffset), new Vector2(306f, 26f));
        }

        private static void BuildSelectionStrip(Transform hud)
        {
            RectTransform panel = EnsurePanel(hud, "Selected Panel", PanelColor);
            SetRect(
                panel,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 172f),
                new Vector2(900f, 40f));

            Transform selected = hud.Find("Selected Status");
            ConfigureText(selected.GetComponent<Text>(), 17, AccentColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetRect((RectTransform)selected, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero,
                new Vector2(-430f, 178f), new Vector2(400f, 28f));

            Transform feedback = hud.Find("Network Feedback");
            ConfigureText(feedback.GetComponent<Text>(), 15, MutedColor, TextAnchor.MiddleLeft, FontStyle.Normal);
            SetRect((RectTransform)feedback, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero,
                new Vector2(-10f, 178f), new Vector2(430f, 28f));
        }

        private static void BuildTowerBar(Transform hud, TowerCatalog catalog)
        {
            RectTransform bar = EnsurePanel(hud, "Build Bar", PanelColor);
            SetRect(
                bar,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 24f),
                new Vector2(900f, 136f));

            Transform buttons = hud.Find("Tower Buttons");
            SetRect(
                (RectTransform)buttons,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 38f),
                new Vector2(868f, 108f));

            GridLayoutGroup grid = buttons.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                Object.DestroyImmediate(grid);
            }

            HorizontalLayoutGroup row = buttons.GetComponent<HorizontalLayoutGroup>();
            if (row == null)
            {
                row = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            row.padding = new RectOffset(8, 8, 8, 8);
            row.spacing = 10f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;

            var placedButtons = new List<TowerPlacementDragButtonView>();
            for (int index = 0; index < catalog.Definitions.Count; index++)
            {
                TowerCombatDefinition definition = catalog.Definitions[index];
                TowerPlacementDragButtonView view = FindButtonView(buttons, definition)
                    ?? CreateButtonView(buttons, definition);

                view.transform.SetSiblingIndex(placedButtons.Count);
                placedButtons.Add(view);
                BuildTowerButton(view);
            }

            // Anything left past the catalog-ordered buttons is a tower that has since
            // left the catalog, so its button would drag a null definition onto the board.
            for (int index = buttons.childCount - 1; index >= placedButtons.Count; index--)
            {
                GameObject orphan = buttons.GetChild(index).gameObject;
                Debug.Log("Removed the stale tower build button " + orphan.name + ".");
                Object.DestroyImmediate(orphan);
            }

            SetObjectReferenceArray(
                hud.GetComponent<TowerNetworkHudView>(),
                "towerDragButtons",
                placedButtons);
        }

        private static void BuildTowerButton(TowerPlacementDragButtonView view)
        {
            Transform button = view.transform;
            Image background = button.GetComponent<Image>();
            Color color;
            StylePanelImage(
                background,
                TowerColorsByName.TryGetValue(button.name, out color) ? color : NeutralButtonColor);
            background.raycastTarget = true;

            DeleteChild(button, "Label");

            Text nameText = Label(button, "Name", button.name.ToUpperInvariant(), Vector2.zero, Vector2.zero,
                15, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(
                (RectTransform)nameText.transform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -16f),
                new Vector2(-14f, 44f));

            RectTransform coin = EnsurePanel(button, "Coin", GoldColor);
            SetRect(
                coin,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(1f, 0.5f),
                new Vector2(-4f, 28f),
                new Vector2(13f, 13f));

            Text cost = Label(button, "Cost", "0", Vector2.zero, Vector2.zero,
                17, GoldColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetRect(
                (RectTransform)cost.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 0.5f),
                new Vector2(2f, 28f),
                new Vector2(70f, 24f));

            SetObjectReference(view, "nameText", nameText);
            SetObjectReference(view, "costText", cost);
            view.ApplyDefinitionLabels();
        }

        private static void BuildWaveControls(Transform hud)
        {
            // The tower actions live in a panel that follows the selected tower, so their
            // rects are driven by that panel's layout group rather than pinned here.
            var unlink = (RectTransform)(hud.Find("Tower Actions/Unlink") ?? hud.Find("Unlink"));
            StyleButton(unlink, NeutralButtonColor);
            Text unlinkLabel = ConfigureText(
                unlink.Find("Label").GetComponent<Text>(),
                18,
                TextColor,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            unlinkLabel.text = "UNLINK";
            StretchToParent((RectTransform)unlinkLabel.transform);

            var startWave = (RectTransform)hud.Find("Start Wave");
            SetRect(
                startWave,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-24f, 32f),
                new Vector2(300f, 116f));
            StyleButton(startWave, StartWaveColor);

            Text startLabel = ConfigureText(
                startWave.Find("Label").GetComponent<Text>(),
                30,
                TextColor,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            startLabel.text = "START WAVE";
            SetRect(
                (RectTransform)startLabel.transform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -24f),
                new Vector2(280f, 40f));

            Text bonus = Label(startWave, "Bonus", "+0 CLEAR BONUS", Vector2.zero, Vector2.zero,
                17, BonusColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(
                (RectTransform)bonus.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 18f),
                new Vector2(280f, 26f));
        }

        private static void BuildStatusCluster(Transform safeArea)
        {
            var statusHud = (RectTransform)safeArea.Find("Level Status HUD");
            SetRect(
                statusHud,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(28f, -24f),
                new Vector2(418f, 84f));

            var healthPanel = (RectTransform)statusHud.Find("Health Panel");
            SetRect(
                healthPanel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                Vector2.zero,
                new Vector2(230f, 84f));
            StylePanelImage(healthPanel.GetComponent<Image>(), PanelColor);

            var goldPanel = (RectTransform)statusHud.Find("Gold Panel");
            SetRect(
                goldPanel,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(244f, 0f),
                new Vector2(174f, 84f));
            StylePanelImage(goldPanel.GetComponent<Image>(), PanelColor);

            Label(healthPanel, "Health Label", "CÓC HP", new Vector2(16f, -12f), new Vector2(120f, 20f),
                14, MutedColor, TextAnchor.UpperLeft, FontStyle.Bold);
            Text healthValue = Label(healthPanel, "Health Value", "10/10", Vector2.zero, Vector2.zero,
                18, TextColor, TextAnchor.UpperRight, FontStyle.Bold);
            SetRect(
                (RectTransform)healthValue.transform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(214f, -12f),
                new Vector2(94f, 22f));

            Label(goldPanel, "Gold Icon", "●", new Vector2(14f, -22f), new Vector2(34f, 36f),
                28, GoldColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Label(goldPanel, "Gold Label", "GOLD", new Vector2(54f, -12f), new Vector2(100f, 18f),
                13, MutedColor, TextAnchor.UpperLeft, FontStyle.Bold);
            Label(goldPanel, "Gold Value", "400", new Vector2(52f, -30f), new Vector2(110f, 40f),
                28, GoldColor, TextAnchor.UpperLeft, FontStyle.Bold);

            var healthBackground = (RectTransform)healthPanel.Find("Health Bar Background");
            StylePanelImage(healthBackground.GetComponent<Image>(), SunkenColor);
            SetRect(
                healthBackground,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(16f, -52f),
                new Vector2(198f, 14f));

            var healthFill = (RectTransform)healthBackground.Find("Health Fill");
            StretchToParent(healthFill);
            ConfigureFillImage(healthFill.GetComponent<Image>(), HealthFillColor);
        }

        private static void BuildCornerButtons(Transform safeArea)
        {
            var pause = (RectTransform)safeArea.Find("Pause Button");
            SetRect(
                pause,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-28f, -24f),
                new Vector2(64f, 64f));
            StyleButton(pause, NeutralButtonColor);
            Text pauseIcon = ConfigureText(
                pause.Find("Pause Icon").GetComponent<Text>(),
                26,
                TextColor,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            StretchToParent((RectTransform)pauseIcon.transform);

            var menu = (RectTransform)safeArea.Find("Return To Level Menu");
            SetRect(
                menu,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-104f, -24f),
                new Vector2(112f, 64f));
            StyleButton(menu, NeutralButtonColor);
            Text menuLabel = ConfigureText(
                menu.Find("Label").GetComponent<Text>(),
                18,
                TextColor,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            menuLabel.text = "MENU";
            StretchToParent((RectTransform)menuLabel.transform);

            var cancel = (RectTransform)safeArea.Find("Cancel");
            SetRect(
                cancel,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-24f, 234f),
                new Vector2(190f, 44f));
            StyleButton(cancel, NeutralButtonColor);
            Text cancelLabel = ConfigureText(
                cancel.Find("Label").GetComponent<Text>(),
                16,
                TextColor,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            cancelLabel.text = "CANCEL";
            StretchToParent((RectTransform)cancelLabel.transform);
        }

        private static void BuildLevelOutcomeHud(Transform safeArea)
        {
            // Superseded by the shared victory/defeat panel below.
            DeleteChild(safeArea, "Victory HUD");

            Transform hud = safeArea.Find("Outcome HUD");
            if (hud == null)
            {
                var created = new GameObject("Outcome HUD", typeof(RectTransform), typeof(LevelOutcomeHudView));
                created.transform.SetParent(safeArea, false);
                hud = created.transform;
            }

            StretchToParent((RectTransform)hud);

            RectTransform overlay = EnsurePanel(hud, "Outcome Root", DimColor);
            StretchToParent(overlay);
            // Blocks board and HUD input while the outcome panel is up.
            overlay.GetComponent<Image>().raycastTarget = true;

            RectTransform card = EnsurePanel(overlay, "Outcome Card", CardColor);
            SetRect(
                card,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(760f, 320f));

            Text title = Label(card, "Outcome Title", "VICTORY", Vector2.zero, Vector2.zero,
                44, AccentColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetRect(
                (RectTransform)title.transform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -36f),
                new Vector2(700f, 62f));

            Text summary = Label(card, "Outcome Summary", "Level cleared", Vector2.zero, Vector2.zero,
                20, MutedColor, TextAnchor.MiddleCenter, FontStyle.Normal);
            SetRect(
                (RectTransform)summary.transform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -112f),
                new Vector2(700f, 36f));

            RectTransform buttons = EnsureRow(card, "Outcome Buttons");
            SetRect(
                buttons,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 44f),
                new Vector2(700f, 92f));

            Button playAgain = EnsureOutcomeButton(buttons, "Play Again", "PLAY AGAIN", NeutralButtonColor, 0);
            Button nextLevel = EnsureOutcomeButton(buttons, "Next Level", "NEXT LEVEL", NextLevelColor, 1);
            Button levelSelect = EnsureOutcomeButton(buttons, "Level Select", "LEVEL SELECT", NeutralButtonColor, 2);

            var view = hud.GetComponent<LevelOutcomeHudView>();
            SetObjectReference(view, "root", overlay.gameObject);
            SetObjectReference(view, "titleText", title);
            SetObjectReference(view, "summaryText", summary);
            SetObjectReference(view, "playAgainButton", playAgain);
            SetObjectReference(view, "nextLevelButton", nextLevel);
            SetObjectReference(view, "returnToLevelMenuButton", levelSelect);
            overlay.gameObject.SetActive(false);
        }

        private static RectTransform EnsureRow(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            GameObject owner;
            if (existing != null)
            {
                owner = existing.gameObject;
            }
            else
            {
                owner = new GameObject(name, typeof(RectTransform));
                owner.transform.SetParent(parent, false);
            }

            HorizontalLayoutGroup row = owner.GetComponent<HorizontalLayoutGroup>();
            if (row == null)
            {
                row = owner.AddComponent<HorizontalLayoutGroup>();
            }

            row.padding = new RectOffset(0, 0, 0, 0);
            row.spacing = 16f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            return (RectTransform)owner.transform;
        }

        private static Button EnsureOutcomeButton(
            Transform parent,
            string name,
            string label,
            Color color,
            int siblingIndex)
        {
            Transform existing = parent.Find(name);
            GameObject owner;
            if (existing != null)
            {
                owner = existing.gameObject;
            }
            else
            {
                owner = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                owner.transform.SetParent(parent, false);
            }

            owner.transform.SetSiblingIndex(siblingIndex);
            StyleButton(owner.transform, color);
            Button button = owner.GetComponent<Button>();
            if (button == null)
            {
                button = owner.AddComponent<Button>();
            }

            button.targetGraphic = owner.GetComponent<Image>();
            Text text = Label(owner.transform, "Label", label, Vector2.zero, Vector2.zero,
                22, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.text = label;
            StretchToParent((RectTransform)text.transform);
            return button;
        }

        private static void WireViews(GameObject root, Transform hud)
        {
            var waveHud = hud.GetComponent<WaveHudView>();
            SetObjectReference(waveHud, "startWaveButton", hud.Find("Start Wave").GetComponent<Button>());
            SetObjectReference(waveHud, "startWaveText", hud.Find("Start Wave/Label").GetComponent<Text>());
            SetObjectReference(waveHud, "startWaveBonusText", hud.Find("Start Wave/Bonus").GetComponent<Text>());
            SetObjectReference(waveHud, "waveCounterText", hud.Find("Wave Panel/Wave Counter").GetComponent<Text>());
            SetObjectReference(waveHud, "statusText", hud.Find("Wave Panel/Wave Status").GetComponent<Text>());
            SetObjectReference(
                waveHud,
                "waveProgressFill",
                hud.Find("Wave Panel/Wave Progress Background/Wave Progress Fill").GetComponent<Image>());
            SetObjectReference(waveHud, "enemiesLeftText", hud.Find("Wave Panel/Enemies Left").GetComponent<Text>());
            SetObjectReference(waveHud, "previewText", hud.Find("Wave Preview").GetComponent<Text>());

            SetObjectReference(
                root.GetComponent<PlacementHudView>(),
                "root",
                hud.Find("Selected Panel").gameObject);
        }

        /// <summary>
        /// Authors the build button for a tower the catalog gained since this prefab was last
        /// rebuilt. The button is named after the tower's display name because the colour table
        /// and the button label both key off that name.
        /// </summary>
        private static TowerPlacementDragButtonView CreateButtonView(
            Transform buttons,
            TowerCombatDefinition definition)
        {
            var created = new GameObject(
                definition.Core.DisplayName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(TowerPlacementDragButtonView));
            created.transform.SetParent(buttons, false);

            var button = created.GetComponent<Button>();
            button.targetGraphic = created.GetComponent<Image>();

            var view = created.GetComponent<TowerPlacementDragButtonView>();
            SetObjectReference(view, "button", button);
            SetObjectReference(view, "definition", definition);
            Debug.Log("Added the missing tower build button " + created.name + ".");
            return view;
        }

        private static TowerPlacementDragButtonView FindButtonView(
            Transform buttons,
            TowerCombatDefinition definition)
        {
            TowerPlacementDragButtonView[] views =
                buttons.GetComponentsInChildren<TowerPlacementDragButtonView>(true);
            for (int index = 0; index < views.Length; index++)
            {
                if (views[index].Definition == definition)
                {
                    return views[index];
                }
            }

            return null;
        }

        private static Text Label(
            Transform parent,
            string name,
            string content,
            Vector2 position,
            Vector2 size,
            int fontSize,
            Color color,
            TextAnchor anchor,
            FontStyle style)
        {
            Transform existing = parent.Find(name);
            Text text;
            if (existing != null)
            {
                text = existing.GetComponent<Text>();
            }
            else
            {
                var created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                created.transform.SetParent(parent, false);
                text = created.GetComponent<Text>();
                text.text = content;
            }

            ConfigureText(text, fontSize, color, anchor, style);
            text.raycastTarget = false;
            if (size != Vector2.zero)
            {
                SetRect(
                    (RectTransform)text.transform,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    position,
                    size);
            }

            return text;
        }

        private static Text ConfigureText(
            Text text,
            int fontSize,
            Color color,
            TextAnchor anchor,
            FontStyle style)
        {
            text.font = uiFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = false;
            return text;
        }

        private static RectTransform EnsurePanel(Transform parent, string name, Color color)
        {
            Transform existing = parent.Find(name);
            Image image;
            if (existing != null)
            {
                image = existing.GetComponent<Image>();
            }
            else
            {
                var created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                created.transform.SetParent(parent, false);
                image = created.GetComponent<Image>();
            }

            StylePanelImage(image, color);
            image.raycastTarget = false;
            return (RectTransform)image.transform;
        }

        private static void StylePanelImage(Image image, Color color)
        {
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.color = color;
        }

        private static void ConfigureFillImage(Image image, Color color)
        {
            image.sprite = roundedSprite;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = 0;
            image.color = color;
            image.raycastTarget = false;
        }

        private static void StyleButton(Transform button, Color color)
        {
            Image image = button.GetComponent<Image>();
            StylePanelImage(image, color);
            image.raycastTarget = true;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void RemoveGraphic(Transform target)
        {
            Image image = target.GetComponent<Image>();
            if (image != null)
            {
                Object.DestroyImmediate(image);
            }

            CanvasRenderer renderer = target.GetComponent<CanvasRenderer>();
            if (renderer != null)
            {
                Object.DestroyImmediate(renderer);
            }
        }

        private static void DeleteChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void OrderChildren(Transform parent, params string[] names)
        {
            for (int index = 0; index < names.Length; index++)
            {
                Transform child = parent.Find(names[index]);
                if (child != null)
                {
                    child.SetSiblingIndex(index);
                }
            }
        }

        private static void SetObjectReferenceArray<T>(
            Object owner,
            string fieldName,
            IReadOnlyList<T> values)
            where T : Object
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null || !property.isArray)
            {
                Debug.LogError(owner.GetType().Name + " has no serialized array " + fieldName);
                return;
            }

            property.arraySize = values.Count;
            for (int index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
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
