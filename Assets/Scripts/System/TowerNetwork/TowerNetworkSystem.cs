using System;
using System.Collections.Generic;
using TowerDefense3D.Economy;
using TowerDefense3D.GridPlacement;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    /// <summary>
    /// Level-scoped facade for tower registration, topology, simulation commands, and HUD state.
    /// </summary>
    public sealed class TowerNetworkSystem : IDisposable
    {
        private readonly TowerNetworkManager manager;
        private readonly GridPlacementSystem placementSystem;
        private readonly LevelGoldSystem goldSystem;
        private readonly TowerRuntimeViewRegistry viewRegistry;
        private readonly Dictionary<ITowerRuntimeView, int> placementOwnerByView =
            new Dictionary<ITowerRuntimeView, int>();
        private readonly HashSet<TowerNodeId> authoredTowerIds = new HashSet<TowerNodeId>();
        private readonly int levelNumber;

        private TowerCombatDefinition placementCombatDefinition;
        private ITowerRuntimeView selectedTower;
        private string lastFeedback = string.Empty;
        private bool tutorialPlacementIsFree;
        private bool isStarted;

        public TowerNetworkSystem(
            TowerNetworkManager manager,
            GridPlacementSystem placementSystem,
            int levelNumber,
            LevelGoldSystem goldSystem)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.placementSystem = placementSystem ?? throw new ArgumentNullException(nameof(placementSystem));
            this.goldSystem = goldSystem ?? throw new ArgumentNullException(nameof(goldSystem));
            if (levelNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(levelNumber), "Level number must be positive.");
            }

            this.levelNumber = levelNumber;
            viewRegistry = new TowerRuntimeViewRegistry(HandleTowerDestroyed);
        }

        public event Action StateChanged;
        public event Action<TowerFamily> ProjectileCreated;
        public event Action<ITowerRuntimeView> TowerUpgraded;

        /// <summary>
        /// Raised once a sale has gone through, after the refund has landed.
        /// </summary>
        /// <remarks>
        /// Carries no view: by the time a sale is final the tower has been despawned, so there is
        /// nothing left for a listener to read off it. Subscribers that want the tower's details
        /// have to take them before this, not from it.
        /// </remarks>
        public event Action TowerSold;

        /// <summary>
        /// Raised when a build or upgrade is turned away purely for want of gold.
        /// </summary>
        /// <remarks>
        /// Only that reason. A wave in progress, a full board or a maxed tower are refusals too,
        /// but pointing at the purse for those would be a lie, and the HUD reacts by drawing
        /// attention to the balance.
        /// </remarks>
        public event Action PurchaseRefusedForGold;

        /// <summary>
        /// Whether a tutorial beat is currently paying for placements.
        /// </summary>
        /// <remarks>
        /// The HUD needs it to keep from dimming a card as unaffordable during a step that is
        /// handing the tower over for nothing.
        /// </remarks>
        public bool IsPlacementFree => tutorialPlacementIsFree;

        public TowerNetworkManager Manager => manager;
        public ITowerRuntimeView SelectedTower => selectedTower;
        public string LastFeedback => lastFeedback;
        public bool HasValidChain => manager.HasValidChain;
        public int ValidChainCount => manager.ValidChainCount;
        public bool IsRunning => manager.IsRunning;
        public bool CanEditTopology => manager.HasLevelSession && !manager.IsRunning;
        public int RegisteredTowerCount => viewRegistry.Count;
        public bool CanSellSelected => selectedTower != null
            && CanEditTopology
            && !authoredTowerIds.Contains(viewRegistry.GetNodeId(selectedTower))
            && selectedTower.CombatDefinition?.Core?.Economy?.Sellable == true;

        public void SetTutorialPlacementFree(bool isFree)
        {
            tutorialPlacementIsFree = isFree;
        }

        public int GetUpgradeLevel(ITowerRuntimeView tower)
        {
            return tower != null && viewRegistry.TryGetNodeId(tower, out TowerNodeId nodeId)
                ? manager.GetUpgradeLevel(nodeId)
                : 0;
        }

        public void Start()
        {
            manager.BeginLevelSession(levelNumber);
            manager.StateChanged += HandleManagerStateChanged;
            manager.ProjectileCreated += HandleProjectileCreated;
            placementSystem.TowerPlaced += HandleTowerPlaced;
            isStarted = true;
            PublishStateChanged();
        }

        public void Dispose()
        {
            if (!isStarted)
            {
                return;
            }

            isStarted = false;
            placementSystem.TowerPlaced -= HandleTowerPlaced;
            manager.StateChanged -= HandleManagerStateChanged;
            manager.ProjectileCreated -= HandleProjectileCreated;
            placementSystem.CancelPlacement();
            placementCombatDefinition = null;
            selectedTower = null;
            lastFeedback = string.Empty;
            authoredTowerIds.Clear();
            viewRegistry.Clear();
            manager.EndLevelSession();
        }

        /// <summary>
        /// Adopts a tower the level authored straight into its scene - a hero standing on the
        /// board before the first wave, for instance. It costs no Gold, but otherwise becomes
        /// an ordinary node: selectable, linkable, and holding the cells under its footprint.
        /// A Hero authored beside a road may be registered without grid occupancy when no valid
        /// footprint exists there, so its direct combat still participates in precomputation.
        /// </summary>
        public bool TryRegisterAuthoredTower(
            ITowerRuntimeView runtimeView,
            TowerCombatDefinition definition,
            out string error)
        {
            return TryRegisterAuthoredTower(runtimeView, definition, null, out error);
        }

        public bool TryRegisterAuthoredTower(
            ITowerRuntimeView runtimeView,
            TowerCombatDefinition definition,
            GridCell? authoredAnchor,
            out string error)
        {
            if (runtimeView == null)
            {
                throw new ArgumentNullException(nameof(runtimeView));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            TowerDefinition placementDefinition = definition.Core.PlacementDefinition;
            if (placementDefinition == null)
            {
                throw new InvalidOperationException(definition.name + " requires a placement definition.");
            }

            runtimeView.Configure(definition);
            Vector3 snappedPosition;
            int ownerId;
            bool hasPlacement = authoredAnchor.HasValue
                ? placementSystem.TryOccupyAuthoredTower(
                    authoredAnchor.Value,
                    placementDefinition.Footprint,
                    out snappedPosition,
                    out ownerId)
                : placementSystem.TryOccupyAuthoredTower(
                    runtimeView.FootprintOrigin,
                    placementDefinition.Footprint,
                    out snappedPosition,
                    out ownerId);
            if (!hasPlacement && !(definition is HeroTowerDefinition))
            {
                error = $"{definition.Core.DisplayName} does not fit the board where the level placed it.";
                return false;
            }

            if (hasPlacement)
            {
                runtimeView.SetFootprintOrigin(snappedPosition);
            }

            Vector3 origin = runtimeView.ProjectileOrigin;
            TowerNodeId nodeId = manager.RegisterTower(
                definition,
                new TowerWorldPosition(origin.x, origin.y, origin.z));

            try
            {
                viewRegistry.Register(nodeId, runtimeView);
                if (hasPlacement)
                {
                    placementOwnerByView[runtimeView] = ownerId;
                }

                authoredTowerIds.Add(nodeId);
            }
            catch
            {
                manager.UnregisterTower(nodeId);
                if (hasPlacement)
                {
                    placementSystem.Occupancy.ReleaseOwner(ownerId);
                }

                throw;
            }

            error = string.Empty;
            return true;
        }

        public IReadOnlyList<ITowerRuntimeView> CreateTowerViewSnapshot()
        {
            return viewRegistry.CreateSnapshot(manager.CreateNodeIdSnapshot());
        }

        public bool TryGetTowerView(TowerNodeId nodeId, out ITowerRuntimeView view)
        {
            return viewRegistry.TryGetView(nodeId, out view);
        }

        private void HandleProjectileCreated(TowerProjectileSnapshot projectile)
        {
            if (viewRegistry.TryGetView(projectile.Source, out ITowerRuntimeView source)
                && source.CombatDefinition != null)
            {
                ProjectileCreated?.Invoke(source.CombatDefinition.Family);
            }
        }

        public bool TryRewire(ITowerRuntimeView source, ITowerRuntimeView target, out string error)
        {
            if (source == null || target == null
                || !viewRegistry.TryGetNodeId(source, out TowerNodeId sourceId)
                || !viewRegistry.TryGetNodeId(target, out TowerNodeId targetId))
            {
                error = "Both link endpoints must be registered towers.";
                return false;
            }

            return manager.TryRewire(sourceId, targetId, out error);
        }

        /// <summary>
        /// Whether a link gesture may begin on <paramref name="source"/> at all.
        /// </summary>
        /// <remarks>
        /// Read before the drag starts, so a link that could never be made is never offered. It
        /// answers the part that does not depend on where the finger ends up.
        /// </remarks>
        public bool CanStartLinkFrom(ITowerRuntimeView source)
        {
            return source != null
                && viewRegistry.TryGetNodeId(source, out TowerNodeId sourceId)
                && manager.CanStartLink(sourceId);
        }

        /// <summary>
        /// Whether linking <paramref name="source"/> to <paramref name="target"/> would be
        /// accepted right now, asked without linking anything.
        /// </summary>
        /// <remarks>
        /// This is what the drag preview colours itself by. It runs the same gate
        /// <see cref="TryRewire"/> runs, so the line the player is dragging tells the truth
        /// before they let go rather than after.
        /// </remarks>
        public bool CanLink(ITowerRuntimeView source, ITowerRuntimeView target)
        {
            return source != null
                && target != null
                && viewRegistry.TryGetNodeId(source, out TowerNodeId sourceId)
                && viewRegistry.TryGetNodeId(target, out TowerNodeId targetId)
                && manager.CanLink(sourceId, targetId);
        }

        public bool IsInValidChain(ITowerRuntimeView tower)
        {
            return tower != null
                && viewRegistry.TryGetNodeId(tower, out TowerNodeId nodeId)
                && manager.IsNodeInValidChain(nodeId);
        }

        public bool HasDirectLink(ITowerRuntimeView source, ITowerRuntimeView target)
        {
            return source != null
                && target != null
                && viewRegistry.TryGetNodeId(source, out TowerNodeId sourceId)
                && viewRegistry.TryGetNodeId(target, out TowerNodeId targetId)
                && manager.TryGetOutgoingLink(sourceId, out TowerLinkSnapshot link)
                && link.Target.Equals(targetId);
        }

        public bool BeginTowerPlacementDrag(TowerCombatDefinition definition, int pointerId)
        {
            if (!CanEditTopology)
            {
                ReportFeedback("Không thể đặt trụ khi đợt đang diễn ra.");
                return false;
            }

            if (definition == null)
            {
                return false;
            }

            if (!tutorialPlacementIsFree && !goldSystem.CanAfford(GetBuildCost(definition)))
            {
                PurchaseRefusedForGold?.Invoke();
                ReportFeedback("Không đủ vàng.");
                return false;
            }

            TowerDefinition placementDefinition = definition.Core.PlacementDefinition;
            if (placementDefinition == null)
            {
                throw new InvalidOperationException(definition.name + " requires a placement definition.");
            }

            ClearSelection();
            placementCombatDefinition = definition;

            // The same answer selection uses, so the ring a tower shows while being dragged onto
            // the board is the ring it shows once it stands there. It used to read the network's
            // link rule directly, on the assumption that every tower reaches the same distance -
            // which stopped being true once a hero fought on its own instead of linking.
            placementSystem.BeginPlacementDrag(
                placementDefinition,
                DescribeRangeMeters(definition),
                pointerId);
            ReportFeedback($"Kéo {definition.Core.DisplayName} vào bản đồ.");
            return true;
        }

        public void UpdateTowerPlacementDrag(int pointerId, Vector2 screenPosition, bool pointerOverUi)
        {
            if (CanEditTopology)
            {
                placementSystem.UpdatePlacementDrag(pointerId, screenPosition, pointerOverUi);
            }
        }

        public bool EndTowerPlacementDrag(int pointerId, Vector2 screenPosition, bool pointerOverUi)
        {
            if (!CanEditTopology)
            {
                placementSystem.CancelPlacementDrag(pointerId);
                placementCombatDefinition = null;
                return false;
            }

            TowerCombatDefinition definition = placementCombatDefinition;
            if (definition == null
                || !tutorialPlacementIsFree && !goldSystem.TrySpend(GetBuildCost(definition)))
            {
                placementSystem.CancelPlacementDrag(pointerId);
                placementCombatDefinition = null;
                if (definition != null)
                {
                    // Guarded on the definition: the other way into this branch is a drag that was
                    // never carrying a tower, which is not a refused purchase.
                    PurchaseRefusedForGold?.Invoke();
                }

                ReportFeedback("Không đủ vàng.");
                return false;
            }

            bool placed = placementSystem.EndPlacementDrag(pointerId, screenPosition, pointerOverUi);
            placementCombatDefinition = null;
            if (!placed)
            {
                if (!tutorialPlacementIsFree)
                {
                    goldSystem.Add(GetBuildCost(definition));
                }
                ReportFeedback("Đã hủy đặt trụ.");
            }

            return placed;
        }

        public void CancelTowerPlacementDrag(int pointerId)
        {
            if (placementSystem.CancelPlacementDrag(pointerId))
            {
                placementCombatDefinition = null;
                ReportFeedback("Đã hủy đặt trụ.");
            }
        }

        public void CancelPlacement()
        {
            placementCombatDefinition = null;
            placementSystem.CancelPlacement();
        }

        /// <summary>
        /// Selects a tower, which is what raises its sell and unlink actions.
        /// </summary>
        /// <remarks>
        /// Only while the board can still be edited. A running wave is committed to a combat
        /// timeline computed before it started, so both actions are refused anyway; letting the
        /// selection through would only float a panel of two dead buttons over the tower. Tapping
        /// a tower mid-wave therefore clears the selection rather than making one.
        /// </remarks>
        public void Select(ITowerRuntimeView tower)
        {
            ITowerRuntimeView nextSelection = tower != null && tower.IsRegistered && CanEditTopology
                ? tower
                : null;
            if (ReferenceEquals(selectedTower, nextSelection))
            {
                return;
            }

            selectedTower = nextSelection;
            PublishStateChanged();
        }

        /// <summary>
        /// How far the selected tower reaches, in metres, or zero when nothing is selected.
        /// </summary>
        /// <remarks>
        /// Two different answers, because there are two kinds of reach on this board. A hero
        /// strikes on its own and carries its own attack radius; every other tower reaches by
        /// linking, and that distance is a rule of the network rather than a property of the
        /// tower - one number shared by all of them.
        ///
        /// Read fresh on every call rather than cached, so a radius edited in the catalog or
        /// changed by an upgrade shows up the next time the ring is drawn.
        /// </remarks>
        public float DescribeSelectedRangeMeters()
        {
            return DescribeRangeMeters(selectedTower);
        }

        /// <summary>How far <paramref name="tower"/> reaches, or zero for no tower.</summary>
        public float DescribeRangeMeters(ITowerRuntimeView tower)
        {
            if (tower == null)
            {
                return 0f;
            }

            if (viewRegistry.TryGetNodeId(tower, out TowerNodeId nodeId)
                && manager.TryGetNodeSpec(nodeId, out TowerRuntimeSpec spec))
            {
                return spec.RangeMeters;
            }

            return DescribeRangeMeters(tower.CombatDefinition);
        }

        /// <summary>
        /// How far a tower of this kind reaches, in metres.
        /// </summary>
        /// <remarks>
        /// One function for every ring the game draws around a tower - while it is being dragged
        /// onto the board, and once it is standing there and selected. Two copies of this answer
        /// is how the placement preview came to promise a hero a twelve metre reach that it never
        /// had.
        ///
        /// A hero carries its own attack radius; everything else reaches by linking, and that
        /// distance is a rule of the network rather than a property of the tower.
        /// </remarks>
        public float DescribeRangeMeters(TowerCombatDefinition definition)
        {
            if (definition == null)
            {
                return 0f;
            }

            return definition is HeroTowerDefinition hero
                ? hero.AttackRangeMeters
                : manager.MaximumLinkRangeMeters;
        }

        /// <summary>
        /// How far any tower may link, which is a rule of the network rather than a property of a
        /// tower.
        /// </summary>
        /// <remarks>
        /// Drawn around the tower a link is being dragged from, where
        /// <see cref="DescribeRangeMeters"/> would be the wrong circle: a hero's own attack radius
        /// says nothing about how far it can link, so a ring measured that way would disagree with
        /// the red the line turns at the edge of it.
        /// </remarks>
        public float MaximumLinkRangeMeters => manager.MaximumLinkRangeMeters;

        public void ClearSelection()
        {
            Select(null);
        }

        public void ReportFeedback(string message)
        {
            string normalized = message ?? string.Empty;
            if (string.Equals(lastFeedback, normalized, StringComparison.Ordinal))
            {
                return;
            }

            lastFeedback = normalized;
            PublishStateChanged();
        }

        public bool TryUnlinkSelected(out string error)
        {
            if (selectedTower == null)
            {
                error = "Select a registered tower before unlinking.";
                ReportFeedback(error);
                return false;
            }

            TowerNodeId nodeId = viewRegistry.GetNodeId(selectedTower);
            bool succeeded = manager.TryUnlinkAll(nodeId, out error);
            ReportFeedback(succeeded ? $"Đã tháo link {GetDisplayName(selectedTower)}." : error);
            return succeeded;
        }

        /// <summary>
        /// Sells the selected tower: drops its links, hands back part of what it cost, frees the
        /// cells it occupied and removes it from the scene. Refusing while a wave runs keeps the
        /// board stable for the precomputed combat timeline.
        /// </summary>
        public bool TrySellSelected(out string error)
        {
            if (selectedTower == null)
            {
                error = "Select a tower before selling.";
                ReportFeedback(error);
                return false;
            }

            if (!CanEditTopology)
            {
                error = "Stop the wave before selling a tower.";
                ReportFeedback(error);
                return false;
            }

            ITowerRuntimeView tower = selectedTower;
            TowerNodeId nodeId = viewRegistry.GetNodeId(tower);
            if (authoredTowerIds.Contains(nodeId))
            {
                error = $"{GetDisplayName(tower)} là trụ có sẵn và không thể bán.";
                ReportFeedback(error);
                return false;
            }

            TowerEconomyProfile economy = tower.CombatDefinition?.Core?.Economy;
            if (economy == null || !economy.Sellable)
            {
                error = $"Không thể bán {GetDisplayName(tower)}.";
                ReportFeedback(error);
                return false;
            }

            if (!manager.TryUnlinkAll(nodeId, out error))
            {
                ReportFeedback(error);
                return false;
            }

            string displayName = GetDisplayName(tower);
            int refund = Mathf.RoundToInt(
                economy.BuildCost * manager.Catalog.CombatRules.SellRefundFraction);
            if (placementOwnerByView.TryGetValue(tower, out int ownerId))
            {
                placementSystem.Occupancy.ReleaseOwner(ownerId);
                placementOwnerByView.Remove(tower);
            }

            ClearSelection();
            tower.Despawn();
            goldSystem.Add(refund);
            TowerSold?.Invoke();
            ReportFeedback($"Đã bán {displayName} với giá {refund} vàng.");
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Describes the selected tower's next upgrade for the HUD.
        /// </summary>
        /// <remarks>
        /// The HUD needs three separate answers, not one boolean: what it costs, whether the
        /// player can afford it, and whether there is anything left to buy. A button that is dark
        /// because the tower is maxed out and one that is dark because the purse is short are the
        /// same pixel but not the same message.
        /// </remarks>
        public bool TryDescribeSelectedUpgrade(out int cost, out bool affordable, out bool atMaxLevel)
        {
            cost = 0;
            affordable = false;
            atMaxLevel = false;
            if (selectedTower == null)
            {
                return false;
            }

            TowerUpgradeCostProfile upgrade = selectedTower.CombatDefinition?.UpgradeCosts;
            if (upgrade == null || !upgrade.IsUpgradable)
            {
                return false;
            }

            int level = manager.GetUpgradeLevel(viewRegistry.GetNodeId(selectedTower));
            atMaxLevel = level >= upgrade.MaxLevel;
            cost = upgrade.CostToReach(level);
            affordable = goldSystem.Balance >= cost;
            return true;
        }

        /// <summary>What selling the selected tower would hand back, for the button to advertise.</summary>
        public int DescribeSelectedSellRefund()
        {
            TowerEconomyProfile economy = selectedTower?.CombatDefinition?.Core?.Economy;
            if (selectedTower == null
                || authoredTowerIds.Contains(viewRegistry.GetNodeId(selectedTower))
                || economy == null
                || !economy.Sellable)
            {
                return 0;
            }

            return Mathf.RoundToInt(economy.BuildCost * manager.Catalog.CombatRules.SellRefundFraction);
        }

        /// <summary>
        /// Buys one upgrade for the selected tower, charging its cost.
        /// </summary>
        /// <remarks>
        /// Gold is spent only after the network accepts the upgrade, so a refusal cannot leave the
        /// player paying for a level they did not get.
        /// </remarks>
        public bool TryUpgradeSelected(out string error)
        {
            if (selectedTower == null)
            {
                error = "Select a tower before upgrading.";
                ReportFeedback(error);
                return false;
            }

            if (!CanEditTopology)
            {
                error = "Stop the wave before upgrading a tower.";
                ReportFeedback(error);
                return false;
            }

            if (!TryDescribeSelectedUpgrade(out int cost, out bool affordable, out bool atMaxLevel))
            {
                error = $"Không thể nâng cấp {GetDisplayName(selectedTower)}.";
                ReportFeedback(error);
                return false;
            }

            if (atMaxLevel)
            {
                error = $"{GetDisplayName(selectedTower)} đã đạt cấp tối đa.";
                ReportFeedback(error);
                return false;
            }

            if (!affordable)
            {
                PurchaseRefusedForGold?.Invoke();
                error = $"Nâng cấp {GetDisplayName(selectedTower)} cần {cost} vàng.";
                ReportFeedback(error);
                return false;
            }

            TowerNodeId nodeId = viewRegistry.GetNodeId(selectedTower);
            if (!manager.TryUpgradeTower(nodeId, out error))
            {
                ReportFeedback(error);
                return false;
            }

            goldSystem.TrySpend(cost);
            TowerUpgraded?.Invoke(selectedTower);
            ReportFeedback(
                $"Đã nâng cấp {GetDisplayName(selectedTower)} lên cấp {manager.GetUpgradeLevel(nodeId)}.");
            error = string.Empty;
            return true;
        }

        public bool TryStartSimulation(out string error)
        {
            CancelPlacement();
            ClearSelection();
            bool succeeded = manager.TryStartSimulation(out error);
            ReportFeedback(succeeded ? "Đã bắt đầu mô phỏng trụ." : error);
            return succeeded;
        }

        public void StopSimulation()
        {
            manager.StopSimulation();
            ReportFeedback("Đã dừng mô phỏng trụ.");
        }

        public bool TryCreateSelectedQueueSummary(out TowerQueueSummary summary)
        {
            if (selectedTower == null)
            {
                summary = default;
                return false;
            }

            return manager.TryCreateQueueSummary(selectedTower.NodeId, out summary);
        }

        private void HandleTowerPlaced(GridPlacementCommit placement)
        {
            TowerCombatDefinition combatDefinition = placementCombatDefinition;
            if (combatDefinition == null)
            {
                return;
            }

            var runtimeView = placement.Instance.GetComponent(typeof(ITowerRuntimeView)) as ITowerRuntimeView;
            if (runtimeView == null)
            {
                throw new InvalidOperationException(
                    $"Tower prefab '{placement.Instance.name}' must author a TowerRuntimeView component.");
            }

            runtimeView.Configure(combatDefinition);
            Vector3 position = runtimeView.ProjectileOrigin;
            TowerNodeId nodeId = manager.RegisterTower(
                combatDefinition,
                new TowerWorldPosition(position.x, position.y, position.z));

            try
            {
                viewRegistry.Register(nodeId, runtimeView);
                placementOwnerByView[runtimeView] = placement.OwnerId;
                ReportFeedback($"Đã đặt {GetDisplayName(runtimeView)}.");
            }
            catch
            {
                manager.UnregisterTower(nodeId);
                throw;
            }
        }

        private void HandleTowerDestroyed(TowerNodeId nodeId)
        {
            authoredTowerIds.Remove(nodeId);
            ClearSelection();
            manager.StopSimulation();
            manager.UnregisterTower(nodeId);
            PublishStateChanged();
        }

        private void HandleManagerStateChanged()
        {
            PublishStateChanged();
        }

        private void PublishStateChanged()
        {
            StateChanged?.Invoke();
        }

        private static string GetDisplayName(ITowerRuntimeView view)
        {
            return view.CombatDefinition.Core.DisplayName;
        }

        private static int GetBuildCost(TowerCombatDefinition definition)
        {
            TowerEconomyProfile economy = definition.Core.Economy;
            if (economy == null)
            {
                throw new InvalidOperationException(definition.name + " requires an economy profile.");
            }

            return economy.BuildCost;
        }
    }
}
