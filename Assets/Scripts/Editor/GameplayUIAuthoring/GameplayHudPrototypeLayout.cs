using System.Collections.Generic;
using TowerDefense3D.Towers;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow.Editor
{
    /// <summary>
    /// Re-authors the gameplay HUD prefab into the prototype screen layout:
    /// next-wave plaque top-left, corner buttons top-right, frog stats bottom-left,
    /// tower build bar bottom-center, wave controls bottom-right.
    ///
    /// Flat colours and builtin sprites for the parts still prototyped here. The pieces that
    /// have real art - the next-wave plaque, the build buttons, the frog cluster - are authored
    /// in the prefab and this only places them, so a rebuild cannot repaint over the art.
    /// </summary>
    public static class GameplayHudPrototypeLayout
    {
        public const string PrefabPath = "Assets/Resources/Prefabs/GameplayUI.prefab";
        private const string TowerCatalogPath = "Assets/Config/Towers/Catalogs/TowerCatalog.asset";

        private static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.10f, 0.88f);
        private static readonly Color TextColor = new Color(0.93f, 0.95f, 0.97f, 1f);
        private static readonly Color MutedColor = new Color(0.62f, 0.66f, 0.72f, 1f);
        private static readonly Color AccentColor = new Color(1f, 0.72f, 0.26f, 1f);
        private static readonly Color StartWaveColor = new Color(0.84f, 0.15f, 0.13f, 1f);
        private static readonly Color NeutralButtonColor = new Color(0.13f, 0.15f, 0.18f, 0.94f);
        private static readonly Color BonusColor = new Color(1f, 0.90f, 0.66f, 1f);
        private static readonly Color DimColor = new Color(0.01f, 0.02f, 0.03f, 0.78f);
        private static readonly Color CardColor = new Color(0.09f, 0.10f, 0.13f, 0.98f);
        private static readonly Color NextLevelColor = new Color(0.13f, 0.52f, 0.30f, 1f);
        private static readonly Color CheatButtonColor = new Color(0.34f, 0.18f, 0.46f, 0.94f);

        // One build button, authored rather than derived: the prefab owns the hit area, the two
        // labels and the coin pip, which are the same for every tower.
        private const string TowerButtonPrefabPath =
            "Assets/Resources/Prefabs/TowerBuildButton.prefab";

        /// <summary>Anchor and pivot for anything hung off the top-left corner.</summary>
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

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

            BuildTowerBar(hud, catalog);
            BuildWaveControls(hud);
            BuildStatusCluster(safeArea);
            BuildCornerButtons(safeArea);
            BuildLevelOutcomeHud(root.transform, safeArea);

            // Backdrops first so the text and controls above them stay visible, and the tower
            // actions dead last: it is a popover that lands wherever the selected tower is, so
            // anything drawn after it would swallow the taps meant for Sell and Unlink.
            OrderChildren(
                hud,
                "Build Bar",
                "Tower Buttons",
                "Start Wave",
                "Next Wave Toggle",
                "Next Wave Grid",
                "Tower Actions");
            OrderChildren(
                safeArea,
                "Tower Network HUD",
                "Level Status HUD",
                "Pause HUD",
                "Pause Button",
                "Skip Waves Cheat");

            // The outcome overlay draws over the safe area, not inside it.
            OrderChildren(root.transform, "Safe Area", "Outcome HUD");
            WireViews(root, hud);
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

        /// <summary>
        /// Colours one build button and lets it read its own labels off its definition.
        /// </summary>
        /// <remarks>
        /// The button used to be assembled here out of a background, a name, a coin pip and a cost
        /// line. The prefab at <see cref="TowerButtonPrefabPath"/> owns all of that now, so the
        /// only thing left to decide is the tint, which is the one piece that genuinely differs
        /// per tower. Placing instances rather than re-deriving them means a tweak made to the
        /// prefab survives the next rebuild.
        /// </remarks>
        private static void BuildTowerButton(TowerPlacementDragButtonView view)
        {
            Transform button = view.transform;
            Image background = button.GetComponent<Image>();
            Color color;
            background.color = TowerColorsByName.TryGetValue(button.name, out color)
                ? color
                : NeutralButtonColor;

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

        /// <summary>
        /// Places the frog's stat cluster in the bottom-left third and leaves its contents alone.
        /// </summary>
        /// <remarks>
        /// The cluster is authored art now - a ringed portrait, a bar built from two sprites and a
        /// coin - so a rebuild has no business repainting it, the same bargain the level menu's
        /// coin panel gets. What a rebuild still owns is where the cluster sits, because that is
        /// what keeps the bottom band split into three: the frog bottom-left, the build bar
        /// centred, the wave controls bottom-right.
        ///
        /// The size is the tight part. 136 is the height of the build bar, so the two clusters
        /// occupy one band rather than two that nearly line up, and the portrait is sized off it
        /// to stand exactly as tall as a build button. Width is what runs out first: on a 4:3
        /// canvas, the narrowest the game supports, the canvas is 1663 wide and the centred
        /// 868-wide button row starts drawing at x 405, so a 365-wide cluster leaves sixteen
        /// pixels. The real margin is larger - the gold box is sized for five digits and the ink
        /// stops near x 277 - but the rect is the number to watch, because the 418-wide version
        /// this replaced overlapped the row outright.
        /// </remarks>
        private static void BuildStatusCluster(Transform safeArea)
        {
            var statusHud = (RectTransform)safeArea.Find("Level Status HUD");
            SetRect(
                statusHud,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(24f, 24f),
                new Vector2(365f, 136f));
        }

        /// <summary>
        /// Places the top-right corner controls and leaves their skins alone.
        /// </summary>
        /// <remarks>
        /// Both are authored now rather than derived. The pause button wears the same orange tile
        /// as the build buttons and carries drawn glyphs, and the skip cheat is deliberately
        /// invisible - alpha zero on a live raycast target - so a rebuild that restyled either of
        /// them would undo the design. What a rebuild still owns is where they sit.
        ///
        /// The cheat keeps a real rect and a real hit area; only its ink is gone. It sits where
        /// the MENU button used to, which is free space now that the pause modal carries the
        /// return-to-menu command.
        /// </remarks>
        private static void BuildCornerButtons(Transform safeArea)
        {
            var pause = (RectTransform)safeArea.Find("Pause Button");
            SetRect(
                pause,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-24f, -24f),
                new Vector2(96f, 96f));

            RectTransform skip = EnsureButton(safeArea, "Skip Waves Cheat");
            SetRect(
                skip,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-136f, -24f),
                new Vector2(128f, 64f));
            SetObjectReference(
                EnsureComponent<LevelSkipCheatView>(skip),
                "skipButton",
                skip.GetComponent<Button>());
        }

        /// <summary>
        /// The victory and defeat panel. It hangs off the canvas rather than off the safe area so
        /// its dim runs edge to edge - under the notch included - instead of leaving a border of
        /// live gameplay around a screen that is meant to be over. The card it dims behind is
        /// centred and far narrower than the display, so nothing readable lands under a cutout.
        /// </summary>
        private static void BuildLevelOutcomeHud(Transform canvas, Transform safeArea)
        {
            // Superseded by the shared victory/defeat panel below.
            DeleteChild(safeArea, "Victory HUD");

            Transform hud = canvas.Find("Outcome HUD");
            if (hud == null)
            {
                // Earlier layouts kept it inside the safe area; carry that one across whole so the
                // wiring already on it survives.
                hud = safeArea.Find("Outcome HUD");
                if (hud != null)
                {
                    hud.SetParent(canvas, false);
                }
            }

            if (hud == null)
            {
                var created = new GameObject("Outcome HUD", typeof(RectTransform), typeof(LevelOutcomeHudView));
                created.transform.SetParent(canvas, false);
                hud = created.transform;
            }

            StretchToParent((RectTransform)hud);

            RectTransform overlay = EnsurePanel(hud, "Outcome Root", DimColor);
            StretchToParent(overlay);
            Image dim = overlay.GetComponent<Image>();

            // A plain quad, not the sliced rounded sprite the panels use: at full screen that
            // sprite rounds the display corners and leaves a hairline of gameplay along every edge.
            dim.sprite = null;
            dim.type = Image.Type.Simple;

            // Blocks board and HUD input while the outcome panel is up.
            dim.raycastTarget = true;

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
            // The wave numbers live on the NEXT WAVE plaque now. The status line and the progress
            // bar went with the panel that used to hold them, and the view treats both as
            // optional, so there is nothing left here to point them at.
            SetObjectReference(
                waveHud,
                "waveCounterText",
                hud.Find("Next Wave Toggle/Wave Counter").GetComponent<Text>());
            SetObjectReference(
                waveHud,
                "enemiesLeftText",
                hud.Find("Next Wave Toggle/Enemies Left").GetComponent<Text>());
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
            GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(TowerButtonPrefabPath);
            if (template == null)
            {
                Debug.LogError($"Tower build button prefab is missing from '{TowerButtonPrefabPath}'.");
                return null;
            }

            var created = (GameObject)PrefabUtility.InstantiatePrefab(template, buttons);
            created.name = definition.Core.DisplayName;

            // The definition is the only reference a button carries that the prefab cannot: the
            // button, name and cost fields all point inside the button, so they travel with it.
            var view = created.GetComponent<TowerPlacementDragButtonView>();
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

        private static RectTransform EnsureButton(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return (RectTransform)existing;
            }

            var created = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            created.transform.SetParent(parent, false);
            created.GetComponent<Button>().targetGraphic = created.GetComponent<Image>();
            return (RectTransform)created.transform;
        }

        private static T EnsureComponent<T>(Component target)
            where T : Component
        {
            T existing = target.GetComponent<T>();
            return existing != null ? existing : target.gameObject.AddComponent<T>();
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
