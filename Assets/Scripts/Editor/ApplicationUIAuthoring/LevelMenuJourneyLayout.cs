using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow.Editor
{
    /// <summary>
    /// Re-authors the level menu prefab into the journey screen: an emblem and title top-left, the
    /// standing of the run top-right, a scrollable weaving trail of level nodes behind it all, the
    /// two loadout buttons bottom-left, the picked level bottom-centre and the node legend
    /// bottom-right. The backdrop is authored art; everything over it is flat colours and builtin
    /// sprites.
    ///
    /// The trail is laid out here rather than by hand because every node needs the same eleven
    /// pieces wired the same way, and the dotted connectors have to follow wherever the nodes land.
    /// </summary>
    public static class LevelMenuJourneyLayout
    {
        public const string PrefabPath = "Assets/Resources/Prefabs/ApplicationUI.prefab";
        private const string LevelCatalogPath = "Assets/Config/GameFlow/LevelCatalog.asset";
        private const string BackdropTexturePath = "Assets/Art/UI/UI_Lv_BG.png";

        private static readonly Color BackdropColor = new Color(0.11f, 0.10f, 0.09f, 1f);
        private static readonly Color PanelColor = new Color(0.15f, 0.13f, 0.11f, 0.94f);
        private static readonly Color SunkenColor = new Color(0.05f, 0.05f, 0.05f, 0.92f);
        private static readonly Color TextColor = new Color(0.96f, 0.93f, 0.86f, 1f);
        private static readonly Color MutedColor = new Color(0.68f, 0.62f, 0.52f, 1f);
        private static readonly Color GoldColor = new Color(1f, 0.80f, 0.32f, 1f);
        private static readonly Color CompletedColor = new Color(0.16f, 0.52f, 0.40f, 1f);
        private static readonly Color CurrentColor = new Color(0.92f, 0.66f, 0.16f, 1f);
        private static readonly Color LockedColor = new Color(0.24f, 0.23f, 0.21f, 1f);
        private static readonly Color SelectedColor = new Color(0.96f, 0.80f, 0.30f, 1f);
        private static readonly Color RouteColor = new Color(0.35f, 0.21f, 0.09f, 0.85f);
        private static readonly Color CloudColor = new Color(1f, 1f, 1f, 0.34f);

        /// <summary>
        /// Ink for the words sitting straight on the backdrop art. The art is a light sunset
        /// gradient, so the cream that reads well on the dark panels disappears against it - the
        /// screen title, the node names and the trail all need to be dark instead.
        /// </summary>
        private static readonly Color InkColor = new Color(0.24f, 0.13f, 0.05f, 1f);
        private static readonly Color InkMutedColor = new Color(0.42f, 0.27f, 0.13f, 1f);

        /// <summary>
        /// Ink rather than gold: an empty star is an outline, and an amber outline on an amber sky
        /// is invisible. Earned stars can be filled gold once there is a score to show.
        /// </summary>
        private static readonly Color StarColor = new Color(0.42f, 0.27f, 0.13f, 1f);
        private static readonly Color EnterMapColor = new Color(0.16f, 0.52f, 0.40f, 1f);
        private static readonly Color LoadoutColor = new Color(0.19f, 0.17f, 0.14f, 0.96f);
        private static readonly Color DisabledTextColor = new Color(0.52f, 0.48f, 0.42f, 1f);

        /// <summary>Horizontal gap between one node and the next, in canvas units.</summary>
        private const float NodeSpacing = 300f;

        /// <summary>Trail inset at both ends of the scroll content, so node one is not flush left.</summary>
        private const float TrailMargin = 250f;

        /// <summary>How high the trail weaves above and below the middle of the map.</summary>
        private const float WeaveHeight = 140f;

        /// <summary>How fast the weave turns over: a little under one full wave every six nodes.</summary>
        private const float WeaveRate = 1.05f;

        private const float NodeDiameter = 118f;
        private const float DotSpacing = 30f;

        /// <summary>Gap left clear around a node so the dotted trail does not run under it.</summary>
        private const float DotClearance = 88f;

        private static Font uiFont;
        private static Sprite roundedSprite;
        private static Sprite circleSprite;
        private static Sprite backdropSprite;

        [MenuItem("Tools/Tower Defense/Rebuild Level Menu Journey Layout")]
        public static void RebuildFromMenu()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(LevelCatalogPath);
            if (catalog == null)
            {
                Debug.LogError("Level Catalog is missing at " + LevelCatalogPath);
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
            {
                Debug.LogError("Application UI prefab is missing at " + PrefabPath);
                return;
            }

            try
            {
                uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                backdropSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackdropTexturePath);
                if (backdropSprite == null)
                {
                    Debug.LogWarning(
                        "Backdrop art is missing at " + BackdropTexturePath
                        + "; falling back to a flat colour.");
                }
                Rebuild(root, catalog);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("Level menu journey layout rebuilt.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Rebuild(GameObject root, LevelCatalog catalog)
        {
            var menu = root.GetComponentInChildren<LevelMenuView>(true);
            Transform screen = menu.transform;
            StretchToParent((RectTransform)screen);

            // The backdrop and the map hang off the canvas rather than off the safe area, so they
            // run edge to edge - under the notch included - instead of leaving a border of whatever
            // sits behind the screen. The chrome stays inset, because that is the part that has to
            // be readable and reachable.
            RectTransform map = BuildMap(root.transform);
            RemoveGraphic(screen);

            // The header carries the screen title now, and the unlock hint moved onto the locked
            // nodes themselves, so the two old strap lines have nothing left to say.
            DeleteChild(screen, "Title");
            DeleteChild(screen, "Selection Hint");

            List<Vector2> nodePositions = BuildTrailPositions(catalog.Levels.Count);
            List<LevelButtonView> nodes = BuildScroll(map, catalog, nodePositions);
            BuildChrome(screen);
            WireMenu(menu, screen, map, nodes);
            OrderChildren(root.transform, "Journey Map", "Safe Area");
            OrderChildren(screen, "Journey Chrome");
        }

        /// <summary>
        /// The full-bleed layer: a flat backdrop plus the scrollable trail, parked on the canvas
        /// outside the safe area. It is authored hidden because the menu shows and hides it along
        /// with the rest of the screen.
        /// </summary>
        private static RectTransform BuildMap(Transform canvas)
        {
            RectTransform map = EnsureChild(canvas, "Journey Map");
            StretchToParent(map);
            Image backdrop = EnsureComponent<Image>(map);

            // Whatever art and tint are already on the sky stay. The path and colour below are
            // only a starting point; once an artist has picked a sky, a rebuild of the layout has
            // no business repainting it.
            bool wasEmpty = backdrop.sprite == null;
            if (wasEmpty)
            {
                backdrop.sprite = backdropSprite;
                backdrop.color = backdropSprite != null ? Color.white : BackdropColor;
            }

            backdrop.type = Image.Type.Simple;

            // Stretched, not aspect-fitted. The point of hanging the sky out here is that it
            // reaches every edge, so on a taller display it gives up a little of its shape rather
            // than leaving a border. This is the one layer allowed to do that - the painted bands
            // are authored at fixed sizes that keep their own proportions.
            backdrop.preserveAspect = false;
            backdrop.raycastTarget = false;

            // The scroll used to live inside the safe area; carry it and its nodes across whole.
            Transform inherited = canvas.Find("Safe Area/Level Menu/Level Scroll");
            if (inherited != null)
            {
                inherited.SetParent(map, false);
            }

            // The parallax rides on the map so it can find the trail among its own children and
            // take the backdrop layers in the order they are drawn. It is added here rather than by
            // hand because the layers themselves are authored per scene, and the component has to
            // be on the prefab for every one of them to get it.
            EnsureComponent<JourneyParallaxView>(map);

            map.gameObject.SetActive(false);
            return map;
        }

        /// <summary>
        /// Where each node sits on the trail. A sine weave rather than a strict zig-zag, so the
        /// trail reads as a wandering road and the dotted connectors bend by varying amounts.
        /// </summary>
        private static List<Vector2> BuildTrailPositions(int levelCount)
        {
            var positions = new List<Vector2>(levelCount);
            for (int index = 0; index < levelCount; index++)
            {
                float x = TrailMargin + (index * NodeSpacing);
                float y = Mathf.Round(Mathf.Sin(index * WeaveRate) * WeaveHeight);
                positions.Add(new Vector2(x, y));
            }

            return positions;
        }

        private static List<LevelButtonView> BuildScroll(
            Transform screen,
            LevelCatalog catalog,
            List<Vector2> nodePositions)
        {
            RectTransform scrollRect = EnsureChild(screen, "Level Scroll");
            StretchToParent(scrollRect);
            var scroll = EnsureComponent<ScrollRect>(scrollRect);

            RectTransform viewport = EnsureChild(scrollRect, "Viewport");
            StretchToParent(viewport);
            EnsureComponent<RectMask2D>(viewport);

            // The drag surface. Without a graphic here the empty stretches of the map are not
            // raycast targets, so a press that misses a node reaches nothing and the trail can only
            // be dragged by grabbing a node. A clear image is still a hit target, so this makes the
            // whole map draggable without painting anything over it.
            Image dragSurface = EnsureComponent<Image>(viewport);
            dragSurface.sprite = null;
            dragSurface.color = Color.clear;
            dragSurface.raycastTarget = true;

            RectTransform content = EnsureChild(viewport, "Level List");
            float contentWidth = (TrailMargin * 2f) + ((catalog.Levels.Count - 1) * NodeSpacing);
            SetRect(
                content,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(contentWidth, 0f));

            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 40f;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontalScrollbar = null;
            scroll.verticalScrollbar = null;

            BuildClouds(content, contentWidth);
            List<LevelButtonView> nodes = BuildNodes(content, catalog, nodePositions);
            BuildRouteDots(content, nodePositions);
            OrderChildren(content, "Journey Clouds", "Journey Route Dots");
            return nodes;
        }

        /// <summary>Soft blobs drifting over the map, purely to give the trail some depth.</summary>
        private static void BuildClouds(RectTransform content, float contentWidth)
        {
            RectTransform clouds = EnsureChild(content, "Journey Clouds");
            SetRect(
                clouds,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            ClearChildren(clouds);

            int cloudCount = Mathf.Max(2, Mathf.RoundToInt(contentWidth / 430f));
            for (int index = 0; index < cloudCount; index++)
            {
                RectTransform cloud = EnsureChild(clouds, "Cloud " + (index + 1));
                float height = index % 2 == 0 ? 350f : -340f;
                SetRect(
                    cloud,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(180f + (index * 430f), height),
                    Vector2.zero);

                Puff(cloud, "Left", new Vector2(-52f, -6f), 78f);
                Puff(cloud, "Middle", new Vector2(0f, 12f), 112f);
                Puff(cloud, "Right", new Vector2(56f, -8f), 86f);
            }
        }

        private static void Puff(RectTransform cloud, string name, Vector2 position, float diameter)
        {
            RectTransform puff = EnsureChild(cloud, name);
            SetRect(
                puff,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                position,
                new Vector2(diameter, diameter));
            Image image = EnsureComponent<Image>(puff);
            image.sprite = circleSprite;
            image.type = Image.Type.Simple;
            image.color = CloudColor;
            image.raycastTarget = false;
        }

        private static List<LevelButtonView> BuildNodes(
            RectTransform content,
            LevelCatalog catalog,
            List<Vector2> nodePositions)
        {
            // Nodes authored by an earlier pass carry hand-picked names, so the rebuild starts from
            // a clean set rather than trying to guess which stale node maps to which level.
            var stale = new List<Transform>();
            for (int index = 0; index < content.childCount; index++)
            {
                Transform child = content.GetChild(index);
                if (child.GetComponent<LevelButtonView>() != null)
                {
                    stale.Add(child);
                }
            }

            for (int index = 0; index < stale.Count; index++)
            {
                Object.DestroyImmediate(stale[index].gameObject);
            }

            var nodes = new List<LevelButtonView>(catalog.Levels.Count);
            for (int index = 0; index < catalog.Levels.Count; index++)
            {
                LevelCatalogEntry entry = catalog.Levels[index];
                nodes.Add(BuildNode(
                    content,
                    entry,
                    nodePositions[index],
                    isFinale: index == catalog.Levels.Count - 1));
            }

            return nodes;
        }

        private static LevelButtonView BuildNode(
            RectTransform content,
            LevelCatalogEntry entry,
            Vector2 position,
            bool isFinale)
        {
            RectTransform node = EnsureChild(content, "Level " + entry.LevelNumber + " Button");
            SetRect(
                node,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                position,
                new Vector2(NodeDiameter, NodeDiameter));

            // A clear image over the node square: it gives the button something to hit test against
            // without covering the ring and body drawn beneath it.
            Image hitArea = EnsureComponent<Image>(node);
            hitArea.sprite = null;
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;

            var button = EnsureComponent<Button>(node);
            button.targetGraphic = hitArea;
            button.transition = Selectable.Transition.None;

            // The last stop sits inside a diamond, so the end of the trail reads as an arrival
            // rather than as one more village.
            RectTransform finale = EnsureChild(node, "Finale");
            Center(finale, Vector2.zero);
            finale.sizeDelta = new Vector2(162f, 162f);
            finale.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image finaleImage = EnsureComponent<Image>(finale);
            StylePanelImage(finaleImage, new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.28f));
            finaleImage.raycastTarget = false;
            finale.gameObject.SetActive(isFinale);

            RectTransform ring = Circle(node, "Ring", NodeDiameter + 34f, CurrentColor);
            RectTransform body = Circle(node, "Body", NodeDiameter, LockedColor);

            Text number = Label(
                node,
                "Label",
                entry.LevelNumber.ToString("00"),
                Vector2.zero,
                new Vector2(NodeDiameter, NodeDiameter),
                40,
                TextColor,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            Center((RectTransform)number.transform, Vector2.zero);

            Text stars = Label(
                node,
                "Stars",
                "☆☆☆",
                Vector2.zero,
                new Vector2(180f, 30f),
                26,
                StarColor,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            Center((RectTransform)stars.transform, new Vector2(0f, 76f));

            RectTransform badge = EnsurePanel(node, "Badge", CurrentColor);
            Center(badge, new Vector2(0f, 104f));
            badge.sizeDelta = new Vector2(164f, 36f);
            Text badgeLabel = Label(
                badge,
                "Label",
                "ĐANG TỚI",
                Vector2.zero,
                Vector2.zero,
                18,
                new Color(0.12f, 0.10f, 0.06f, 1f),
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            StretchToParent((RectTransform)badgeLabel.transform);

            RectTransform tick = EnsureChild(node, "Ready");
            Center(tick, new Vector2(40f, 40f));
            tick.sizeDelta = new Vector2(38f, 38f);
            Image tickBacking = EnsureComponent<Image>(tick);
            tickBacking.sprite = circleSprite;
            tickBacking.type = Image.Type.Simple;
            tickBacking.color = CompletedColor;
            tickBacking.raycastTarget = false;
            Text tickLabel = Label(
                tick,
                "Label",
                "✓",
                Vector2.zero,
                Vector2.zero,
                24,
                TextColor,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            StretchToParent((RectTransform)tickLabel.transform);

            RectTransform padlock = BuildPadlock(node);

            Text title = Label(
                node,
                "Journey Title",
                entry.DisplayName.ToUpperInvariant(),
                Vector2.zero,
                new Vector2(250f, 40f),
                20,
                InkColor,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            Center((RectTransform)title.transform, new Vector2(0f, -84f));

            Text requirement = Label(
                node,
                "Requirement",
                "CHƯA MỞ",
                Vector2.zero,
                new Vector2(250f, 28f),
                16,
                InkMutedColor,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            Center((RectTransform)requirement.transform, new Vector2(0f, -116f));

            // Ring behind the body, body behind everything the node says about itself.
            OrderChildren(
                node,
                "Finale",
                "Ring",
                "Body",
                "Label",
                "Lock",
                "Ready",
                "Stars",
                "Badge",
                "Journey Title",
                "Requirement");

            var view = EnsureComponent<LevelButtonView>(node);
            SetObjectReference(view, "button", button);
            SetObjectReference(view, "label", number);
            SetObjectReference(view, "titleLabel", title);
            SetObjectReference(view, "nodeImage", body.GetComponent<Image>());
            SetObjectReference(view, "lockedIndicator", padlock.gameObject);
            SetObjectReference(view, "unlockedIndicator", tick.gameObject);
            SetObjectReference(view, "ringIndicator", ring.gameObject);
            SetObjectReference(view, "currentBadge", badge.gameObject);
            SetObjectReference(view, "starsLabel", stars);
            SetObjectReference(view, "requirementLabel", requirement);
            SetColor(view, "completedColor", CompletedColor);
            SetColor(view, "currentColor", CurrentColor);
            SetColor(view, "lockedColor", LockedColor);
            SetColor(view, "selectedColor", SelectedColor);
            return view;
        }

        /// <summary>
        /// A padlock built out of a ring and a box, because the builtin UI sprites have no lock and
        /// the runtime font has no glyph for one.
        /// </summary>
        private static RectTransform BuildPadlock(RectTransform node)
        {
            RectTransform padlock = EnsureChild(node, "Lock");
            Center(padlock, Vector2.zero);
            padlock.sizeDelta = new Vector2(46f, 52f);

            RectTransform shackle = EnsureChild(padlock, "Shackle");
            SetRect(
                shackle,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 14f),
                new Vector2(28f, 28f));
            Image shackleImage = EnsureComponent<Image>(shackle);
            shackleImage.sprite = circleSprite;
            shackleImage.type = Image.Type.Simple;
            shackleImage.color = MutedColor;
            shackleImage.raycastTarget = false;

            RectTransform body = EnsureChild(padlock, "Body");
            SetRect(
                body,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -8f),
                new Vector2(40f, 30f));
            Image bodyImage = EnsureComponent<Image>(body);
            StylePanelImage(bodyImage, MutedColor);
            bodyImage.raycastTarget = false;
            return padlock;
        }

        /// <summary>The dotted trail, laid fresh so it follows wherever the nodes were just put.</summary>
        private static void BuildRouteDots(RectTransform content, List<Vector2> nodePositions)
        {
            RectTransform dots = EnsureChild(content, "Journey Route Dots");
            SetRect(
                dots,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            ClearChildren(dots);

            int placed = 0;
            for (int index = 1; index < nodePositions.Count; index++)
            {
                Vector2 from = nodePositions[index - 1];
                Vector2 to = nodePositions[index];
                float span = Vector2.Distance(from, to);
                float travelled = DotClearance;
                while (travelled <= span - DotClearance)
                {
                    Vector2 point = Vector2.Lerp(from, to, travelled / span);
                    placed++;
                    RectTransform dot = EnsureChild(dots, "Route Dot " + placed);
                    SetRect(
                        dot,
                        new Vector2(0f, 0.5f),
                        new Vector2(0f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        point,
                        new Vector2(10f, 10f));
                    Image image = EnsureComponent<Image>(dot);
                    image.sprite = circleSprite;
                    image.type = Image.Type.Simple;
                    image.color = RouteColor;
                    image.raycastTarget = false;
                    travelled += DotSpacing;
                }
            }
        }

        private static void BuildChrome(Transform screen)
        {
            RectTransform chrome = EnsureChild(screen, "Journey Chrome");
            StretchToParent(chrome);
            RemoveGraphic(chrome);

            // Superseded by the header cluster and the loadout buttons below.
            DeleteChild(chrome, "Top Border");
            DeleteChild(chrome, "Bottom Border");
            DeleteChild(chrome, "Hero Select Panel");
            DeleteChild(chrome, "Tower Upgrade Panel");

            BuildHeader(chrome);
            BuildStanding(chrome);
            BuildLoadoutButtons(chrome);
            BuildSelectedLevelPanel(chrome);
            BuildLegend(chrome);

            OrderChildren(
                chrome,
                "Journey Emblem",
                "Journey Header",
                "Journey Subheader",
                "Progress Panel",
                "Star Panel",
                "Coin Panel",
                "Hero Select Button",
                "Tower Upgrade Button",
                "Selected Level Panel",
                "Journey Legend");
        }

        private static void BuildHeader(RectTransform chrome)
        {
            RectTransform emblem = EnsureChild(chrome, "Journey Emblem");
            SetRect(
                emblem,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(48f, -40f),
                new Vector2(76f, 76f));
            Image emblemImage = EnsureComponent<Image>(emblem);
            emblemImage.sprite = circleSprite;
            emblemImage.type = Image.Type.Simple;
            emblemImage.color = PanelColor;
            emblemImage.raycastTarget = false;
            Text emblemLabel = Label(
                emblem,
                "Label",
                "◆",
                Vector2.zero,
                Vector2.zero,
                34,
                GoldColor,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            StretchToParent((RectTransform)emblemLabel.transform);

            Text title = Label(
                chrome,
                "Journey Header",
                "HÀNH TRÌNH",
                new Vector2(146f, -38f),
                new Vector2(560f, 54f),
                44,
                InkColor,
                TextAnchor.UpperLeft,
                FontStyle.Bold);
            title.text = "HÀNH TRÌNH";

            // Filled in from the catalog at runtime; authored so the layout reads in the editor.
            Label(
                chrome,
                "Journey Subheader",
                "10 LĂNG · THE JOURNEY",
                new Vector2(148f, -92f),
                new Vector2(560f, 28f),
                20,
                InkMutedColor,
                TextAnchor.UpperLeft,
                FontStyle.Normal);
        }

        private static void BuildStanding(RectTransform chrome)
        {
            RectTransform progress = TopRightPanel(chrome, "Progress Panel", -452f, 330f);

            // The panel used to hold one centred line; it now holds a caption, a value and a bar.
            DeleteChild(progress, "Label");
            Label(
                progress,
                "Caption",
                "TIẾN ĐỘ",
                new Vector2(18f, -12f),
                new Vector2(160f, 22f),
                18,
                MutedColor,
                TextAnchor.UpperLeft,
                FontStyle.Bold);
            Text progressValue = Label(
                progress,
                "Value",
                "0/10",
                Vector2.zero,
                new Vector2(140f, 28f),
                22,
                TextColor,
                TextAnchor.UpperRight,
                FontStyle.Bold);
            SetRect(
                (RectTransform)progressValue.transform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-18f, -10f),
                new Vector2(140f, 28f));

            RectTransform bar = EnsurePanel(progress, "Bar Background", SunkenColor);
            SetRect(
                bar,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 16f),
                new Vector2(-36f, 14f));
            RectTransform fill = EnsureChild(bar, "Bar Fill");
            StretchToParent(fill);
            ConfigureFillImage(EnsureComponent<Image>(fill), GoldColor);

            RectTransform stars = TopRightPanel(chrome, "Star Panel", -246f, 190f);
            Label(
                stars,
                "Label",
                "★ 0/30",
                Vector2.zero,
                Vector2.zero,
                24,
                GoldColor,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            StretchToParent((RectTransform)stars.Find("Label"));

            RectTransform coins = TopRightPanel(chrome, "Coin Panel", -40f, 190f);
            // TODO: show the gold the run has banked once the save carries it.
            Label(
                coins,
                "Label",
                "◆ —",
                Vector2.zero,
                Vector2.zero,
                24,
                GoldColor,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            StretchToParent((RectTransform)coins.Find("Label"));
        }

        private static RectTransform TopRightPanel(
            RectTransform chrome,
            string name,
            float right,
            float width)
        {
            RectTransform panel = EnsurePanel(chrome, name, PanelColor);
            SetRect(
                panel,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(right, -40f),
                new Vector2(width, 72f));
            return panel;
        }

        /// <summary>
        /// Hero select and tower upgrade. Both are authored dimmed and not interactable: the screens
        /// behind them do not exist yet, and a button that looks live but does nothing reads as a
        /// bug. Wiring one up is a matter of handing it a callback and turning interactable back on.
        /// </summary>
        private static void BuildLoadoutButtons(RectTransform chrome)
        {
            BuildLoadoutButton(chrome, "Hero Select Button", "CHỌN TƯỚNG", "HERO SELECT", 124f);
            BuildLoadoutButton(chrome, "Tower Upgrade Button", "NÂNG CẤP TRỤ", "TOWER UPGRADE", 28f);
        }

        private static void BuildLoadoutButton(
            RectTransform chrome,
            string name,
            string title,
            string subtitle,
            float bottom)
        {
            RectTransform owner = EnsureButton(chrome, name);
            SetRect(
                owner,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(48f, bottom),
                new Vector2(300f, 84f));
            StyleButton(owner, LoadoutColor);

            var button = EnsureComponent<Button>(owner);
            button.targetGraphic = owner.GetComponent<Image>();

            // No transition, because Unity tints a non-interactable Selectable to its disabled
            // colour - half alpha by default - and a half-transparent button over a bright backdrop
            // reads as a rendering fault rather than as something not available yet. The muted label
            // colours say that instead.
            button.transition = Selectable.Transition.None;
            button.interactable = false;

            Text titleLabel = Label(
                owner,
                "Label",
                title,
                new Vector2(0f, -14f),
                new Vector2(300f, 30f),
                21,
                DisabledTextColor,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            titleLabel.text = title;
            SetRect(
                (RectTransform)titleLabel.transform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -14f),
                new Vector2(0f, 30f));

            Text subtitleLabel = Label(
                owner,
                "Sub",
                subtitle,
                Vector2.zero,
                Vector2.zero,
                15,
                MutedColor,
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            subtitleLabel.text = subtitle;
            SetRect(
                (RectTransform)subtitleLabel.transform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 16f),
                new Vector2(0f, 24f));
        }

        private static void BuildSelectedLevelPanel(RectTransform chrome)
        {
            RectTransform panel = EnsurePanel(chrome, "Selected Level Panel", PanelColor);
            SetRect(
                panel,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 28f),
                new Vector2(720f, 172f));

            Label(
                panel,
                "Selected Chapter",
                "HỒI 01 · ĐANG CHỌN",
                new Vector2(28f, -20f),
                new Vector2(410f, 24f),
                18,
                GoldColor,
                TextAnchor.UpperLeft,
                FontStyle.Bold);
            Label(
                panel,
                "Selected Title",
                "LĂNG 01 — LEVEL 1",
                new Vector2(28f, -50f),
                new Vector2(410f, 44f),
                32,
                TextColor,
                TextAnchor.UpperLeft,
                FontStyle.Bold);
            Label(
                panel,
                "Selected Details",
                "ĐỢT — · ĐỘ KHÓ — · THƯỞNG —",
                new Vector2(28f, -102f),
                new Vector2(410f, 28f),
                18,
                MutedColor,
                TextAnchor.UpperLeft,
                FontStyle.Normal);

            RectTransform enterMap = EnsureButton(panel, "Enter Map Button");
            SetRect(
                enterMap,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-24f, 0f),
                new Vector2(240f, 112f));
            StyleButton(enterMap, EnterMapColor);
            EnsureComponent<Button>(enterMap).targetGraphic = enterMap.GetComponent<Image>();

            Text enterTitle = Label(
                enterMap,
                "Label",
                "XUẤT QUÂN",
                Vector2.zero,
                Vector2.zero,
                26,
                TextColor,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            enterTitle.text = "XUẤT QUÂN";
            SetRect(
                (RectTransform)enterTitle.transform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -26f),
                new Vector2(0f, 36f));

            Text enterSub = Label(
                enterMap,
                "Sub",
                "ENTER MAP",
                Vector2.zero,
                Vector2.zero,
                15,
                new Color(0.82f, 0.92f, 0.86f, 1f),
                TextAnchor.MiddleCenter,
                FontStyle.Normal);
            enterSub.text = "ENTER MAP";
            SetRect(
                (RectTransform)enterSub.transform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 24f),
                new Vector2(0f, 24f));
        }

        private static void BuildLegend(RectTransform chrome)
        {
            RectTransform legend = EnsurePanel(chrome, "Journey Legend", PanelColor);
            SetRect(
                legend,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-40f, 34f),
                new Vector2(414f, 56f));
            ClearChildren(legend);

            BuildLegendItem(legend, 0, "ĐÃ QUA", CompletedColor);
            BuildLegendItem(legend, 1, "ĐANG TỚI", CurrentColor);
            BuildLegendItem(legend, 2, "CHƯA MỞ", LockedColor);
        }

        private static void BuildLegendItem(RectTransform legend, int index, string caption, Color color)
        {
            float left = 18f + (index * 134f);
            RectTransform dot = EnsureChild(legend, "Legend Dot " + (index + 1));
            SetRect(
                dot,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(left, 0f),
                new Vector2(18f, 18f));
            Image image = EnsureComponent<Image>(dot);
            image.sprite = circleSprite;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;

            Text label = Label(
                legend,
                "Legend Label " + (index + 1),
                caption,
                Vector2.zero,
                Vector2.zero,
                16,
                MutedColor,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            label.text = caption;
            SetRect(
                (RectTransform)label.transform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(left + 24f, 0f),
                new Vector2(110f, 24f));
        }

        private static void WireMenu(
            LevelMenuView menu,
            Transform screen,
            RectTransform map,
            List<LevelButtonView> nodes)
        {
            Transform chrome = screen.Find("Journey Chrome");
            Transform selected = chrome.Find("Selected Level Panel");

            SetObjectReference(menu, "root", screen.gameObject);
            SetObjectReference(menu, "backdrop", map.gameObject);
            SetObjectReference(
                map.GetComponent<JourneyParallaxView>(),
                "scroll",
                map.GetComponentInChildren<ScrollRect>(true));
            SetObjectReferenceArray(menu, "levelButtons", nodes);
            SetObjectReference(menu, "selectionPanel", selected.gameObject);
            SetObjectReference(
                menu,
                "selectionChapter",
                selected.Find("Selected Chapter").GetComponent<Text>());
            SetObjectReference(
                menu,
                "selectionTitle",
                selected.Find("Selected Title").GetComponent<Text>());
            SetObjectReference(
                menu,
                "selectionDetails",
                selected.Find("Selected Details").GetComponent<Text>());
            SetObjectReference(
                menu,
                "enterMapButton",
                selected.Find("Enter Map Button").GetComponent<Button>());
            SetObjectReference(
                menu,
                "subtitleLabel",
                chrome.Find("Journey Subheader").GetComponent<Text>());
            SetObjectReference(
                menu,
                "progressLabel",
                chrome.Find("Progress Panel/Value").GetComponent<Text>());
            SetObjectReference(
                menu,
                "progressFill",
                chrome.Find("Progress Panel/Bar Background/Bar Fill").GetComponent<Image>());
            SetObjectReference(
                menu,
                "starLabel",
                chrome.Find("Star Panel/Label").GetComponent<Text>());
        }

        private static RectTransform Circle(Transform parent, string name, float diameter, Color color)
        {
            RectTransform circle = EnsureChild(parent, name);
            SetRect(
                circle,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(diameter, diameter));
            Image image = EnsureComponent<Image>(circle);
            image.sprite = circleSprite;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return circle;
        }

        private static void Center(RectTransform rect, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
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
                text = EnsureComponent<Text>(existing);
            }
            else
            {
                var created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                created.transform.SetParent(parent, false);
                text = created.GetComponent<Text>();
            }

            if (string.IsNullOrEmpty(text.text))
            {
                text.text = content;
            }

            text.font = uiFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
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

        private static RectTransform EnsurePanel(Transform parent, string name, Color color)
        {
            RectTransform panel = EnsureChild(parent, name);
            Image image = EnsureComponent<Image>(panel);
            StylePanelImage(image, color);
            image.raycastTarget = false;
            return panel;
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
            image.fillAmount = 0f;
            image.color = color;
            image.raycastTarget = false;
        }

        private static void StyleButton(Transform button, Color color)
        {
            Image image = EnsureComponent<Image>(button);
            StylePanelImage(image, color);
            image.raycastTarget = true;
        }

        private static RectTransform EnsureChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return (RectTransform)existing;
            }

            var created = new GameObject(name, typeof(RectTransform));
            created.transform.SetParent(parent, false);
            return (RectTransform)created.transform;
        }

        private static RectTransform EnsureButton(Transform parent, string name)
        {
            RectTransform owner = EnsureChild(parent, name);
            EnsureComponent<Image>(owner);
            EnsureComponent<Button>(owner).targetGraphic = owner.GetComponent<Image>();
            return owner;
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

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(parent.GetChild(index).gameObject);
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

        private static void SetColor(Object owner, string fieldName, Color value)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError(owner.GetType().Name + " has no serialized field " + fieldName);
                return;
            }

            property.colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
