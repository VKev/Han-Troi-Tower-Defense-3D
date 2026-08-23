using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    public readonly struct TowerLinkViewItem
    {
        public TowerLinkViewItem(
            TowerNodeId sourceId,
            Vector3 sourcePosition,
            Vector3 targetPosition,
            bool isValidChain)
        {
            SourceId = sourceId;
            SourcePosition = sourcePosition;
            TargetPosition = targetPosition;
            IsValidChain = isValidChain;
        }

        public TowerNodeId SourceId { get; }
        public Vector3 SourcePosition { get; }
        public Vector3 TargetPosition { get; }
        public bool IsValidChain { get; }
    }

    public interface ITowerLinkView
    {
        void Initialize();
        void RenderLinks(IReadOnlyList<TowerLinkViewItem> links);
        void ShowSelection(Vector3 center, float radius);
        void HideSelection();
        void ShowPreview(Vector3 source, Vector3 target, bool hasValidTarget);
        void HidePreview();
        void Clear();
    }
}
