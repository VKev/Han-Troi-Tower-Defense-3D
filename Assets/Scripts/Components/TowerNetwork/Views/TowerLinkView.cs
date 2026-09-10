using System;
using System.Collections.Generic;
using TowerDefense3D.Components.Core;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [DisallowMultipleComponent]
    public sealed class TowerLinkView : MonoBehaviour, ITowerLinkView
    {
        /// <summary>
        /// The selection bracket, loaded rather than authored per scene so its look - stroke,
        /// gaps, colour - is tuned once in the prefab instead of ten times over.
        /// </summary>
        private static readonly Color ValidLinkColor = new Color(0.1f, 1f, 0.1f, 0.9f);
        private static readonly Color InvalidLinkColor = new Color(1f, 0.25f, 0.015f, 0.9f);

        [SerializeField, Min(0.02f)] private float linkWidth = 0.1f;
        [SerializeField, Min(0.02f)] private float previewWidth = 0.08f;
        [SerializeField] private Transform presentationRoot;
        [SerializeField] private TowerLinkLineView linePrefab;
        [SerializeField] private TowerLinkLineView previewLine;
        [SerializeField] private TowerSelectionRingView selectionRing;

        private readonly Dictionary<TowerNodeId, TowerLinkLineView> linkLines =
            new Dictionary<TowerNodeId, TowerLinkLineView>();
        private readonly HashSet<TowerNodeId> visibleSources = new HashSet<TowerNodeId>();
        private readonly List<TowerNodeId> removedSources = new List<TowerNodeId>();

        private Material lineMaterial;
        private Vector3 previewSource;
        private bool isPreviewVisible;

        public void Initialize()
        {
            if (lineMaterial != null)
            {
                return;
            }

            if (presentationRoot == null || linePrefab == null || previewLine == null || selectionRing == null)
            {
                throw new MissingReferenceException("TowerLinkView requires authored presentation references.");
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                throw new InvalidOperationException("Tower link presentation requires the Sprites/Default shader.");
            }

            lineMaterial = new Material(shader)
            {
                name = "Tower Link Runtime Material"
            };
            previewLine.Initialize(lineMaterial, previewWidth, 2);
        }

        public void RenderLinks(IReadOnlyList<TowerLinkViewItem> links)
        {
            visibleSources.Clear();
            for (int index = 0; index < links.Count; index++)
            {
                TowerLinkViewItem link = links[index];
                visibleSources.Add(link.SourceId);
                if (!linkLines.TryGetValue(link.SourceId, out TowerLinkLineView line))
                {
                    if (isPreviewVisible
                        && (previewSource - link.SourcePosition).sqrMagnitude <= 0.0001f)
                    {
                        line = previewLine;
                        line.gameObject.name = $"Link {link.SourceId}";
                        previewLine = CreateLine("Link Preview", previewWidth, 2);
                        isPreviewVisible = false;
                    }
                    else
                    {
                        line = CreateLine($"Link {link.SourceId}", linkWidth, 2);
                    }

                    linkLines.Add(link.SourceId, line);
                }

                line.ShowLine(
                    link.SourcePosition,
                    link.TargetPosition,
                    link.IsValidChain ? ValidLinkColor : InvalidLinkColor);
            }

            removedSources.Clear();
            foreach (KeyValuePair<TowerNodeId, TowerLinkLineView> pair in linkLines)
            {
                if (!visibleSources.Contains(pair.Key))
                {
                    RuntimeObjectDestroyer.Destroy(pair.Value.gameObject);
                    removedSources.Add(pair.Key);
                }
            }

            for (int index = 0; index < removedSources.Count; index++)
            {
                linkLines.Remove(removedSources[index]);
            }
        }

        public void ShowSelection(Vector3 center, float radius)
        {
            // No lift here. The ring is drawn on the board at the tower's foot and carries its own
            // clearance off the ground; raising it again from out here would only start it
            // clipping through whatever the tower stands on.
            selectionRing?.Show(center, radius);
        }

        public void HideSelection()
        {
            selectionRing?.Hide();
        }

        public void ShowPreview(Vector3 source, Vector3 target, bool hasValidTarget)
        {
            previewSource = source;
            isPreviewVisible = true;
            previewLine.ShowLine(
                source,
                target,
                hasValidTarget ? ValidLinkColor : InvalidLinkColor);
        }

        public void HidePreview()
        {
            isPreviewVisible = false;
            previewLine.Hide();
        }

        public void Clear()
        {
            foreach (TowerLinkLineView line in linkLines.Values)
            {
                if (line != null)
                {
                    RuntimeObjectDestroyer.Destroy(line.gameObject);
                }
            }

            linkLines.Clear();
            visibleSources.Clear();
            removedSources.Clear();
            isPreviewVisible = false;
            if (previewLine != null)
            {
                previewLine.Hide();
            }

            if (selectionRing != null)
            {
                selectionRing.Hide();
            }
        }

        private TowerLinkLineView CreateLine(string objectName, float width, int positionCount)
        {
            TowerLinkLineView line = Instantiate(linePrefab, presentationRoot);
            line.gameObject.name = objectName;
            line.Initialize(lineMaterial, width, positionCount);
            return line;
        }

        private void OnDestroy()
        {
            Clear();
            RuntimeObjectDestroyer.Destroy(lineMaterial);
        }
    }
}
