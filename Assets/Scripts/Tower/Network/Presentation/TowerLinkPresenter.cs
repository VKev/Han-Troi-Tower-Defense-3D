using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [DisallowMultipleComponent]
    public sealed class TowerLinkPresenter : MonoBehaviour
    {
        [SerializeField, Min(0.02f)] private float linkWidth = 0.1f;
        [SerializeField, Min(0.02f)] private float previewWidth = 0.08f;
        [SerializeField, Min(0.1f)] private float selectionRadius = 0.75f;

        private readonly Dictionary<TowerNodeId, LineRenderer> linkLines =
            new Dictionary<TowerNodeId, LineRenderer>();
        private readonly HashSet<TowerNodeId> visibleSources = new HashSet<TowerNodeId>();

        private TowerNetworkManager manager;
        private ITowerNetworkSceneRegistry registry;
        private TowerNetworkInputController inputController;
        private Transform presentationRoot;
        private Material lineMaterial;
        private LineRenderer previewLine;
        private LineRenderer selectionRing;

        public bool IsInitialized => manager != null;
        public int VisibleLinkCount => linkLines.Count;

        public void Initialize(
            TowerNetworkManager towerNetworkManager,
            ITowerNetworkSceneRegistry sceneRegistry,
            TowerNetworkInputController input)
        {
            if (manager != null)
            {
                throw new InvalidOperationException("TowerLinkPresenter is already initialized.");
            }

            manager = towerNetworkManager ?? throw new ArgumentNullException(nameof(towerNetworkManager));
            registry = sceneRegistry ?? throw new ArgumentNullException(nameof(sceneRegistry));
            inputController = input != null ? input : throw new ArgumentNullException(nameof(input));

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                throw new InvalidOperationException("Tower link presentation requires the Sprites/Default shader.");
            }

            presentationRoot = new GameObject("Tower Link Visuals").transform;
            presentationRoot.SetParent(transform, false);
            lineMaterial = new Material(shader)
            {
                name = "Tower Link Runtime Material"
            };
            previewLine = CreateLine("Link Preview", previewWidth, false);
            previewLine.positionCount = 2;
            previewLine.gameObject.SetActive(false);
            selectionRing = CreateLine("Tower Selection", linkWidth, true);
            selectionRing.positionCount = 32;
            selectionRing.gameObject.SetActive(false);
        }

        public void Shutdown()
        {
            manager = null;
            registry = null;
            inputController = null;
            linkLines.Clear();
            visibleSources.Clear();
            previewLine = null;
            selectionRing = null;

            if (presentationRoot != null)
            {
                DestroyRuntimeObject(presentationRoot.gameObject);
                presentationRoot = null;
            }

            if (lineMaterial != null)
            {
                DestroyRuntimeObject(lineMaterial);
                lineMaterial = null;
            }
        }

        public void RefreshPresentation()
        {
            if (manager == null)
            {
                return;
            }

            RefreshLinks();
            RefreshSelection();
            RefreshPreview();
        }

        private void LateUpdate()
        {
            RefreshPresentation();
        }

        private void RefreshLinks()
        {
            IReadOnlyList<TowerLinkSnapshot> links = manager.CreateLinkSnapshot();
            visibleSources.Clear();

            for (int index = 0; index < links.Count; index++)
            {
                TowerLinkSnapshot link = links[index];
                if (!registry.TryGetTowerView(link.Source, out TowerRuntimeView source)
                    || !registry.TryGetTowerView(link.Target, out TowerRuntimeView target))
                {
                    continue;
                }

                visibleSources.Add(link.Source);
                if (!linkLines.TryGetValue(link.Source, out LineRenderer line))
                {
                    line = CreateLine($"Link {link.Source}", linkWidth, false);
                    line.positionCount = 2;
                    linkLines.Add(link.Source, line);
                }

                line.SetPosition(0, source.PresentationAnchor);
                line.SetPosition(1, target.PresentationAnchor);
                SetLineColor(line, manager.IsNodeInValidChain(link.Source)
                    ? new Color(0.25f, 1f, 0.35f, 0.95f)
                    : new Color(1f, 0.55f, 0.1f, 0.9f));
                line.gameObject.SetActive(true);
            }

            List<TowerNodeId> removedSources = new List<TowerNodeId>();
            foreach (KeyValuePair<TowerNodeId, LineRenderer> pair in linkLines)
            {
                if (!visibleSources.Contains(pair.Key))
                {
                    DestroyRuntimeObject(pair.Value.gameObject);
                    removedSources.Add(pair.Key);
                }
            }

            for (int index = 0; index < removedSources.Count; index++)
            {
                linkLines.Remove(removedSources[index]);
            }
        }

        private void RefreshSelection()
        {
            TowerRuntimeView selectedTower = inputController.SelectedTower;
            if (selectedTower == null)
            {
                selectionRing.gameObject.SetActive(false);
                return;
            }

            Vector3 center = selectedTower.PresentationAnchor;
            center.y += 0.08f;
            for (int index = 0; index < selectionRing.positionCount; index++)
            {
                float angle = (Mathf.PI * 2f * index) / selectionRing.positionCount;
                selectionRing.SetPosition(index, center + new Vector3(
                    Mathf.Cos(angle) * selectionRadius,
                    0f,
                    Mathf.Sin(angle) * selectionRadius));
            }

            SetLineColor(selectionRing, new Color(0.2f, 0.85f, 1f, 1f));
            selectionRing.gameObject.SetActive(true);
        }

        private void RefreshPreview()
        {
            TowerRuntimeView selectedTower = inputController.SelectedTower;
            if (!inputController.IsDraggingLink || selectedTower == null)
            {
                previewLine.gameObject.SetActive(false);
                return;
            }

            previewLine.SetPosition(0, selectedTower.PresentationAnchor);
            previewLine.SetPosition(1, inputController.PreviewWorldPosition);
            SetLineColor(previewLine, inputController.PreviewTarget != null
                ? new Color(0.25f, 1f, 0.35f, 0.95f)
                : new Color(1f, 0.2f, 0.2f, 0.95f));
            previewLine.gameObject.SetActive(true);
        }

        private LineRenderer CreateLine(string objectName, float width, bool loop)
        {
            GameObject lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(presentationRoot, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.useWorldSpace = true;
            line.loop = loop;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static void SetLineColor(LineRenderer line, Color color)
        {
            line.startColor = color;
            line.endColor = color;
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
