using System;
using System.Collections.Generic;
using TowerDefense3D.Waves;

namespace TowerDefense3D.Towers
{
    /// <summary>
    /// Projects tower topology and interaction state into the authored link view on late tick.
    /// </summary>
    public sealed class TowerLinkPresentationSystem : IDisposable
    {
        /// <summary>
        /// How far outside the tower's own silhouette the selection bracket is drawn.
        /// </summary>
        /// <remarks>
        /// Added to the tower's measured radius rather than replacing it. A single fixed radius
        /// was the bug this replaced: at 0.75m it fell inside every tower in the catalog - each
        /// is between 1.7m and 2.3m across - so the board's depth buffer hid the whole ring behind
        /// the tower it was drawn for, leaving only a sliver showing past the near edge.
        ///
        /// A clearance in metres, not a multiplier: the gap between tower and bracket should look
        /// the same on a small tower and a large one, and a multiplier would open it up on the
        /// large one for no reason.
        /// </remarks>
        private const float SelectionClearanceMeters = 0.22f;

        private readonly TowerNetworkManager manager;
        private readonly TowerNetworkSystem towerNetworkSystem;
        private readonly TowerInteractionSystem interactionSystem;
        private readonly IWaveSystem waveSystem;
        private readonly ITowerLinkView view;
        private readonly ILinkRangeView rangeRing;
        private readonly List<TowerLinkViewItem> visibleLinks = new List<TowerLinkViewItem>();
        private readonly Dictionary<TowerNodeId, TowerNodeId> previewValidLinks =
            new Dictionary<TowerNodeId, TowerNodeId>();
        private TowerNodeId previewSourceId;
        private TowerNodeId previewTargetId;
        private bool previewTopologyDirty = true;
        private bool hasPreviewTopology;

        /// <summary>
        /// <paramref name="rangeRing"/> is the reach circle drawn flat on the board. This system
        /// owns it outright, and it is the only thing that writes to it.
        /// </summary>
        /// <remarks>
        /// It used to be driven from the HUD presenter, which refreshes only when the model says
        /// something changed. Dragging a link changes nothing in the model, so a ring driven from
        /// there could not follow a drag; and had both driven it, the one that ran every frame
        /// would simply win, leaving the other's circle stranded when the drag ended. One owner,
        /// ticked every frame, decides the whole question here instead.
        ///
        /// It may be null, and everything else then behaves exactly as it did before there was a
        /// ring.
        /// </remarks>
        public TowerLinkPresentationSystem(
            TowerNetworkManager manager,
            TowerNetworkSystem towerNetworkSystem,
            TowerInteractionSystem interactionSystem,
            IWaveSystem waveSystem,
            ITowerLinkView view,
            ILinkRangeView rangeRing)
        {
            this.rangeRing = rangeRing;
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.towerNetworkSystem = towerNetworkSystem
                ?? throw new ArgumentNullException(nameof(towerNetworkSystem));
            this.interactionSystem = interactionSystem
                ?? throw new ArgumentNullException(nameof(interactionSystem));
            this.waveSystem = waveSystem ?? throw new ArgumentNullException(nameof(waveSystem));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public void Start()
        {
            manager.StateChanged += InvalidatePreviewTopology;
            view.Initialize();
        }

        public void LateTick()
        {
            RefreshPreviewTopology();
            IReadOnlyList<TowerLinkSnapshot> links = manager.CreateLinkSnapshot();
            RefreshFacing(links);
            RefreshLinks(links);
            RefreshSelection();
            RefreshPreview();
            RefreshRangeRing();
        }

        /// <summary>
        /// Puts the reach circle around whichever tower the player is currently asking about: the
        /// one a link is being dragged from, or failing that the one that is selected.
        /// </summary>
        /// <remarks>
        /// The drag wins over the selection because it is the more recent question, and because a
        /// selection can still be standing from an earlier tap while the finger is on another
        /// tower entirely - two rings would be one too many, and the stale one would be the
        /// misleading one.
        ///
        /// A drag is rung at the link distance rather than at the source tower's own reach. The
        /// circle and the colour of the line have to be the same rule, or the line would turn red
        /// somewhere other than at the edge of the circle drawn to explain it.
        ///
        /// The radius is re-asked every frame, so a tower upgraded mid-level gets its new circle
        /// the moment the upgrade lands rather than the size it had when it was picked. It is
        /// centred on the tower's ground centre, which is the point the network registered the
        /// tower at and therefore the point it measures link range from - a circle drawn on the
        /// prefab's pivot instead would sit off to one side on every tower whose pivot is not
        /// under its middle, and would be lying about the reach.
        /// </remarks>
        private void RefreshRangeRing()
        {
            if (rangeRing == null)
            {
                return;
            }

            ITowerRuntimeView linkSource = interactionSystem.LinkSource;
            if (interactionSystem.IsDraggingLink && linkSource != null)
            {
                rangeRing.Show(
                    linkSource.GroundCentre,
                    towerNetworkSystem.DescribeRangeMeters(linkSource));
                return;
            }

            ITowerRuntimeView selectedTower = towerNetworkSystem.SelectedTower;
            if (selectedTower == null)
            {
                rangeRing.Hide();
                return;
            }

            rangeRing.Show(
                selectedTower.GroundCentre,
                towerNetworkSystem.DescribeRangeMeters(selectedTower));
        }

        /// <summary>
        /// A tower keeps looking down its link even while the wave runs and the link lines are
        /// hidden, which is exactly when the player reads where a tower is pointing.
        /// </summary>
        private void RefreshFacing(IReadOnlyList<TowerLinkSnapshot> links)
        {
            for (int index = 0; index < links.Count; index++)
            {
                TowerLinkSnapshot link = links[index];
                if (towerNetworkSystem.TryGetTowerView(link.Source, out ITowerRuntimeView source)
                    && towerNetworkSystem.TryGetTowerView(link.Target, out ITowerRuntimeView target))
                {
                    source.FaceTowards(target.PresentationAnchor);
                }
            }
        }

        public void Dispose()
        {
            manager.StateChanged -= InvalidatePreviewTopology;
            previewValidLinks.Clear();
            visibleLinks.Clear();
            view.Clear();
            rangeRing?.Hide();
        }

        private void RefreshLinks(IReadOnlyList<TowerLinkSnapshot> links)
        {
            visibleLinks.Clear();
            if (waveSystem.IsRunning)
            {
                view.RenderLinks(visibleLinks);
                return;
            }

            for (int index = 0; index < links.Count; index++)
            {
                TowerLinkSnapshot link = links[index];
                if (!towerNetworkSystem.TryGetTowerView(link.Source, out ITowerRuntimeView source)
                    || !towerNetworkSystem.TryGetTowerView(link.Target, out ITowerRuntimeView target))
                {
                    continue;
                }

                visibleLinks.Add(new TowerLinkViewItem(
                    link.Source,
                    source.PresentationAnchor,
                    target.PresentationAnchor,
                    hasPreviewTopology
                        ? previewValidLinks.TryGetValue(link.Source, out TowerNodeId predictedTarget)
                            && predictedTarget.Equals(link.Target)
                        : manager.IsNodeInValidChain(link.Source)));
            }

            view.RenderLinks(visibleLinks);
        }

        private void RefreshSelection()
        {
            ITowerRuntimeView selectedTower = towerNetworkSystem.SelectedTower;
            if (selectedTower == null)
            {
                view.HideSelection();
                return;
            }

            // Centred on the tower's silhouette rather than on its pivot, and sized from that
            // silhouette: the bracket has to sit outside the tower to be seen at all.
            view.ShowSelection(
                selectedTower.GroundCentre,
                selectedTower.GroundRadiusMeters + SelectionClearanceMeters);
        }

        private void RefreshPreview()
        {
            ITowerRuntimeView linkSource = interactionSystem.LinkSource;
            if (!interactionSystem.IsDraggingLink || linkSource == null)
            {
                view.HidePreview();
                return;
            }

            // Preview and committed links use the same complete-chain rule.
            view.ShowPreview(
                linkSource.PresentationAnchor,
                interactionSystem.PreviewWorldPosition,
                hasPreviewTopology && previewValidLinks.TryGetValue(previewSourceId, out TowerNodeId target)
                    && target.Equals(previewTargetId));
        }

        private void InvalidatePreviewTopology()
        {
            previewTopologyDirty = true;
        }

        private void RefreshPreviewTopology()
        {
            TowerNodeId sourceId = interactionSystem.IsDraggingLink && interactionSystem.LinkSource != null
                ? interactionSystem.LinkSource.NodeId : default;
            TowerNodeId targetId = interactionSystem.IsDraggingLink && interactionSystem.PreviewTarget != null
                ? interactionSystem.PreviewTarget.NodeId : default;
            if (!previewTopologyDirty && sourceId.Equals(previewSourceId) && targetId.Equals(previewTargetId))
            {
                return;
            }

            previewSourceId = sourceId;
            previewTargetId = targetId;
            previewTopologyDirty = false;
            previewValidLinks.Clear();
            hasPreviewTopology = sourceId.IsValid && targetId.IsValid
                && manager.TryCollectPreviewValidLinks(sourceId, targetId, previewValidLinks);
        }
    }
}
