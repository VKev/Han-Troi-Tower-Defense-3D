using System;
using System.Collections.Generic;

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
        private readonly ITowerLinkView view;
        private readonly List<TowerLinkViewItem> visibleLinks = new List<TowerLinkViewItem>();

        public TowerLinkPresentationSystem(
            TowerNetworkManager manager,
            TowerNetworkSystem towerNetworkSystem,
            TowerInteractionSystem interactionSystem,
            ITowerLinkView view)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.towerNetworkSystem = towerNetworkSystem
                ?? throw new ArgumentNullException(nameof(towerNetworkSystem));
            this.interactionSystem = interactionSystem
                ?? throw new ArgumentNullException(nameof(interactionSystem));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public void Start()
        {
            view.Initialize();
        }

        public void LateTick()
        {
            RefreshLinks();
            RefreshSelection();
            RefreshPreview();
        }

        public void Dispose()
        {
            visibleLinks.Clear();
            view.Clear();
        }

        private void RefreshLinks()
        {
            IReadOnlyList<TowerLinkSnapshot> links = manager.CreateLinkSnapshot();
            visibleLinks.Clear();

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
            ITowerRuntimeView selectedTower = towerNetworkSystem.SelectedTower;
            if (!interactionSystem.IsDraggingLink || selectedTower == null)
            {
                view.HidePreview();
                return;
            }

            view.ShowPreview(
                selectedTower.PresentationAnchor,
                interactionSystem.PreviewWorldPosition,
                interactionSystem.PreviewTarget != null);
        }
    }
}
