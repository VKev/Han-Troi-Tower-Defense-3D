using System;
using System.Collections.Generic;
using TowerDefense3D.Components.Core;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [DisallowMultipleComponent]
    public sealed class TowerLinkView : MonoBehaviour, ITowerLinkView
    {
        [SerializeField, Min(0.02f)] private float linkWidth = 0.1f;
        [SerializeField, Min(0.02f)] private float previewWidth = 0.08f;

        private readonly Dictionary<TowerNodeId, TowerLinkLineView> linkLines =
            new Dictionary<TowerNodeId, TowerLinkLineView>();
        private readonly HashSet<TowerNodeId> visibleSources = new HashSet<TowerNodeId>();
        private readonly List<TowerNodeId> removedSources = new List<TowerNodeId>();

        private Transform presentationRoot;
        private Material lineMaterial;
        private TowerLinkLineView previewLine;
        private TowerLinkLineView selectionRing;

        public void Initialize()
        {
            if (presentationRoot != null)
            {
                return;
            }

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
            previewLine = CreateLine("Link Preview", previewWidth, false, 2);
            selectionRing = CreateLine("Tower Selection", linkWidth, true, 32);
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
                    line = CreateLine($"Link {link.SourceId}", linkWidth, false, 2);
                    linkLines.Add(link.SourceId, line);
                }

                line.ShowLine(
                    link.SourcePosition,
                    link.TargetPosition,
                    link.IsValidChain
                        ? new Color(0.25f, 1f, 0.35f, 0.95f)
                        : new Color(1f, 0.55f, 0.1f, 0.9f));
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
            center.y += 0.08f;
            selectionRing.ShowRing(center, radius, new Color(0.25f, 1f, 0.35f, 1f));
        }

        public void HideSelection()
        {
            selectionRing.Hide();
        }

        public void ShowPreview(Vector3 source, Vector3 target, bool hasValidTarget)
        {
            previewLine.ShowLine(
                source,
                target,
                hasValidTarget
                    ? new Color(0.25f, 1f, 0.35f, 0.95f)
                    : new Color(1f, 0.2f, 0.2f, 0.95f));
        }

        public void HidePreview()
        {
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
            if (previewLine != null)
            {
                previewLine.Hide();
            }

            if (selectionRing != null)
            {
                selectionRing.Hide();
            }
        }

        private TowerLinkLineView CreateLine(string objectName, float width, bool loop, int positionCount)
        {
            GameObject lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(presentationRoot, false);
            TowerLinkLineView line = lineObject.AddComponent<TowerLinkLineView>();
            line.Initialize(lineMaterial, width, loop, positionCount);
            return line;
        }

        private void OnDestroy()
        {
            Clear();
            RuntimeObjectDestroyer.Destroy(lineMaterial);
        }
    }
}
