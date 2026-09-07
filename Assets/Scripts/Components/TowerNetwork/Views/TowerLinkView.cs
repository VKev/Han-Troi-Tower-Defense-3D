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
        private const string SelectionRingResourcePath = "Prefabs/TowerSelectionRing";

        [SerializeField, Min(0.02f)] private float linkWidth = 0.1f;
        [SerializeField, Min(0.02f)] private float previewWidth = 0.08f;

        private readonly Dictionary<TowerNodeId, TowerLinkLineView> linkLines =
            new Dictionary<TowerNodeId, TowerLinkLineView>();
        private readonly HashSet<TowerNodeId> visibleSources = new HashSet<TowerNodeId>();
        private readonly List<TowerNodeId> removedSources = new List<TowerNodeId>();

        private Transform presentationRoot;
        private Material lineMaterial;
        private TowerLinkLineView previewLine;
        private TowerSelectionRingView selectionRing;

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
            previewLine = CreateLine("Link Preview", previewWidth, 2);
            selectionRing = CreateSelectionRing();
        }

        /// <summary>
        /// Instantiates the selection bracket under the presentation root.
        /// </summary>
        /// <remarks>
        /// A missing prefab leaves the field null and costs the game a bracket, not a level: every
        /// use of it is guarded. It is worth a warning rather than a throw, because the selection
        /// still works - only its outline is gone.
        /// </remarks>
        private TowerSelectionRingView CreateSelectionRing()
        {
            var prefab = Resources.Load<GameObject>(SelectionRingResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning(
                    "Tower selection ring prefab missing at Resources/" + SelectionRingResourcePath);
                return null;
            }

            GameObject instance = Instantiate(prefab, presentationRoot);
            instance.name = "Tower Selection";
            return instance.GetComponent<TowerSelectionRingView>();
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
                    line = CreateLine($"Link {link.SourceId}", linkWidth, 2);
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

        private TowerLinkLineView CreateLine(string objectName, float width, int positionCount)
        {
            GameObject lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(presentationRoot, false);
            TowerLinkLineView line = lineObject.AddComponent<TowerLinkLineView>();
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
