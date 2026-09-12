using System;
using System.Collections.Generic;

namespace TowerDefense3D.Towers
{
    internal sealed class TowerRuntimeViewRegistry
    {
        private readonly Dictionary<TowerNodeId, ITowerRuntimeView> viewsByNode =
            new Dictionary<TowerNodeId, ITowerRuntimeView>();
        private readonly Dictionary<ITowerRuntimeView, TowerNodeId> nodesByView =
            new Dictionary<ITowerRuntimeView, TowerNodeId>();
        private readonly Action<TowerNodeId> viewDestroyed;

        public TowerRuntimeViewRegistry(Action<TowerNodeId> viewDestroyed)
        {
            this.viewDestroyed = viewDestroyed;
        }

        public int Count => viewsByNode.Count;

        public void Register(TowerNodeId nodeId, ITowerRuntimeView view)
        {
            view.BindNode(nodeId);
            try
            {
                viewsByNode.Add(nodeId, view);
                nodesByView.Add(view, nodeId);
                view.Destroyed += HandleViewDestroyed;
            }
            catch
            {
                viewsByNode.Remove(nodeId);
                nodesByView.Remove(view);
                view.ClearNodeBinding();
                throw;
            }
        }

        public IReadOnlyList<ITowerRuntimeView> CreateSnapshot(IReadOnlyList<TowerNodeId> orderedNodeIds)
        {
            var snapshot = new List<ITowerRuntimeView>(orderedNodeIds.Count);
            for (int index = 0; index < orderedNodeIds.Count; index++)
            {
                if (viewsByNode.TryGetValue(orderedNodeIds[index], out ITowerRuntimeView view))
                {
                    snapshot.Add(view);
                }
            }

            return snapshot;
        }

        public bool TryGetView(TowerNodeId nodeId, out ITowerRuntimeView view)
        {
            return viewsByNode.TryGetValue(nodeId, out view);
        }

        public bool TryGetNodeId(ITowerRuntimeView view, out TowerNodeId nodeId)
        {
            return nodesByView.TryGetValue(view, out nodeId);
        }

        public TowerNodeId GetNodeId(ITowerRuntimeView view)
        {
            return nodesByView[view];
        }

        /// <summary>
        /// Drops a view the game is retiring on purpose, and stops listening for its destruction.
        /// </summary>
        /// <remarks>
        /// A sold tower has to leave the network the moment it is sold. Left to the view's own
        /// <c>OnDestroy</c>, it leaves whenever Unity gets round to destroying the object - the
        /// end of the frame in play mode, and never at all for an object whose callbacks the
        /// editor does not run. Unsubscribing here is what keeps that later destruction from
        /// reporting the same tower a second time.
        /// </remarks>
        public bool Unregister(ITowerRuntimeView view)
        {
            if (view == null || !nodesByView.TryGetValue(view, out TowerNodeId nodeId))
            {
                return false;
            }

            view.Destroyed -= HandleViewDestroyed;
            nodesByView.Remove(view);
            viewsByNode.Remove(nodeId);
            view.ClearNodeBinding();
            return true;
        }

        public void Clear()
        {
            foreach (ITowerRuntimeView view in nodesByView.Keys)
            {
                view.Destroyed -= HandleViewDestroyed;
                view.ClearNodeBinding();
            }

            nodesByView.Clear();
            viewsByNode.Clear();
        }

        private void HandleViewDestroyed(ITowerRuntimeView view)
        {
            TowerNodeId nodeId = nodesByView[view];
            view.Destroyed -= HandleViewDestroyed;
            nodesByView.Remove(view);
            viewsByNode.Remove(nodeId);
            viewDestroyed(nodeId);
        }
    }
}
