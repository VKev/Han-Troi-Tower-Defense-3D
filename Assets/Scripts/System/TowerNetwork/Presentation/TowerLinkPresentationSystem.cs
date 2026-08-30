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
        private const float SelectionRadius = 0.75f;

        private readonly TowerNetworkManager manager;
        private readonly TowerNetworkSystem towerNetworkSystem;
        private readonly TowerInteractionSystem interactionSystem;
        private readonly IWaveSystem waveSystem;
        private readonly ITowerLinkView view;
        private readonly List<TowerLinkViewItem> visibleLinks = new List<TowerLinkViewItem>();

        public TowerLinkPresentationSystem(
            TowerNetworkManager manager,
            TowerNetworkSystem towerNetworkSystem,
            TowerInteractionSystem interactionSystem,
            IWaveSystem waveSystem,
            ITowerLinkView view)
        {
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
            view.Initialize();
        }

        public void LateTick()
        {
            IReadOnlyList<TowerLinkSnapshot> links = manager.CreateLinkSnapshot();
            RefreshFacing(links);
            RefreshLinks(links);
            RefreshSelection();
            RefreshPreview();
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
            visibleLinks.Clear();
            view.Clear();
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
                    manager.IsNodeInValidChain(link.Source)));
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

            view.ShowSelection(selectedTower.PresentationAnchor, SelectionRadius);
        }

        private void RefreshPreview()
        {
            ITowerRuntimeView linkSource = interactionSystem.LinkSource;
            if (!interactionSystem.IsDraggingLink || linkSource == null)
            {
                view.HidePreview();
                return;
            }

            view.ShowPreview(
                linkSource.PresentationAnchor,
                interactionSystem.PreviewWorldPosition,
                interactionSystem.PreviewTarget != null);
        }
    }
}
