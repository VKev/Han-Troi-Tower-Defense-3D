using System;
using System.Collections.Generic;
using TowerDefense3D.Economy;
using TowerDefense3D.Tutorials;
using TowerDefense3D.Towers;
using TowerDefense3D.Waves;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Converts tower-network state and HUD commands between the model and its view.
    /// </summary>
    public sealed class TowerNetworkHudPresenter
    {
        /// <summary>
        /// Lifts the action panel clear of the tower's own silhouette.
        /// </summary>
        private const float TowerActionsHeightMeters = 0.35f;

        private readonly TowerNetworkSystem towerNetworkSystem;
        private readonly ITowerNetworkHudView towerNetworkHud;
        private readonly Camera worldCamera;
        private readonly TowerCatalog towerCatalog;
        private readonly SaveSystem saveSystem;
        private readonly TutorialProgress tutorialProgress;
        private readonly int levelNumber;
        private readonly IWaveSystem waveSystem;
        private readonly LevelGoldSystem goldSystem;
        private Action requestReturnToMenu;

        public TowerNetworkHudPresenter(
            TowerNetworkSystem towerNetworkSystem,
            ITowerNetworkHudView towerNetworkHud,
            Camera worldCamera,
            TowerCatalog towerCatalog,
            SaveSystem saveSystem,
            TutorialProgress tutorialProgress = null,
            int levelNumber = 0,
            IWaveSystem waveSystem = null,
            LevelGoldSystem goldSystem = null)
        {
            this.worldCamera = worldCamera;
            this.towerCatalog = towerCatalog ?? throw new ArgumentNullException(nameof(towerCatalog));
            this.saveSystem = saveSystem ?? throw new ArgumentNullException(nameof(saveSystem));
            this.tutorialProgress = tutorialProgress;
            this.levelNumber = levelNumber;
            this.waveSystem = waveSystem;
            this.goldSystem = goldSystem;
            this.towerNetworkSystem = towerNetworkSystem
                ?? throw new ArgumentNullException(nameof(towerNetworkSystem));
            this.towerNetworkHud = towerNetworkHud
                ?? throw new ArgumentNullException(nameof(towerNetworkHud));
        }

        public void BindReturnToMenu(Action request)
        {
            requestReturnToMenu = request ?? throw new ArgumentNullException(nameof(request));
        }

        public void Connect()
        {
            towerNetworkHud.Initialize();
            towerNetworkHud.ApplyTowerLocks(CollectLockedDefinitions());
            towerNetworkHud.ApplyTowerAffordability(CollectUnaffordableDefinitions());
            towerNetworkHud.TowerDragBegan += HandleTowerDragBegan;
            towerNetworkHud.TowerDragMoved += HandleTowerDragMoved;
            towerNetworkHud.TowerDragEnded += HandleTowerDragEnded;
            towerNetworkHud.TowerDragCanceled += HandleTowerDragCanceled;
            towerNetworkHud.UnlinkRequested += HandleUnlinkRequested;
            towerNetworkHud.SellRequested += HandleSellRequested;
            towerNetworkHud.UpgradeRequested += HandleUpgradeRequested;
            towerNetworkHud.ReturnToMenuRequested += HandleReturnToMenu;
            towerNetworkHud.Show();
        }

        public void Disconnect()
        {
            towerNetworkHud.TowerDragBegan -= HandleTowerDragBegan;
            towerNetworkHud.TowerDragMoved -= HandleTowerDragMoved;
            towerNetworkHud.TowerDragEnded -= HandleTowerDragEnded;
            towerNetworkHud.TowerDragCanceled -= HandleTowerDragCanceled;
            towerNetworkHud.UnlinkRequested -= HandleUnlinkRequested;
            towerNetworkHud.SellRequested -= HandleSellRequested;
            towerNetworkHud.UpgradeRequested -= HandleUpgradeRequested;
            towerNetworkHud.ReturnToMenuRequested -= HandleReturnToMenu;
        }

        public void Refresh()
        {
            towerNetworkHud.ApplyTowerLocks(CollectLockedDefinitions());
            towerNetworkHud.ApplyTowerAffordability(CollectUnaffordableDefinitions());
            towerNetworkHud.SetTowerActionsAvailable(AreTowerActionsAvailable());
            ITowerRuntimeView selectedTower = towerNetworkSystem.SelectedTower;
            string selectedText = selectedTower == null
                ? "Đã chọn: Chưa có"
                : $"Đã chọn: {selectedTower.CombatDefinition.Core.DisplayName} "
                    + $"({LocalizeRole(selectedTower.CombatDefinition.NetworkRole)})";
            string feedbackText = string.IsNullOrWhiteSpace(towerNetworkSystem.LastFeedback)
                ? "Đặt trụ, sau đó kéo từ trụ này tới trụ khác."
                : towerNetworkSystem.LastFeedback;
            bool simulationRunning = towerNetworkSystem.IsRunning;
            bool towerActionsVisible = TryGetTowerActionsPosition(
                selectedTower,
                out Vector2 towerActionsScreenPosition);
            bool canEdit = towerNetworkSystem.CanEditTopology && AreTowerToolsUnlocked();
            bool hasUpgrade = towerNetworkSystem.TryDescribeSelectedUpgrade(
                out int upgradeCost,
                out bool affordable,
                out bool atMaxLevel);

            towerNetworkHud.Render(new TowerNetworkHudState(
                selectedText,
                feedbackText,
                !simulationRunning,
                selectedTower != null && canEdit,
                canEdit && towerNetworkSystem.CanSellSelected,
                towerActionsVisible,
                towerActionsScreenPosition,
                canEdit && hasUpgrade && !atMaxLevel && affordable,
                CreateUpgradeCostText(hasUpgrade, atMaxLevel, upgradeCost),
                canEdit && towerNetworkSystem.CanSellSelected
                    ? towerNetworkSystem.DescribeSelectedSellRefund().ToString()
                    : string.Empty,
                hasUpgrade && !atMaxLevel,
                selectedTower?.CombatDefinition?.Family));
        }

        private static string LocalizeRole(TowerNetworkRole role)
        {
            switch (role)
            {
                case TowerNetworkRole.Source: return "Nguồn";
                case TowerNetworkRole.Sink: return "Đích";
                case TowerNetworkRole.Processor: return "Xử lý";
                default: return role.ToString();
            }
        }

        /// <summary>
        /// The towers whose build cost is past the current balance.
        /// </summary>
        /// <remarks>
        /// Empty while a tutorial step is paying, so a card being handed over for free is never
        /// dimmed as though it were out of reach.
        /// </remarks>
        private IReadOnlyList<TowerCombatDefinition> CollectUnaffordableDefinitions()
        {
            var unaffordable = new List<TowerCombatDefinition>();
            if (goldSystem == null || towerNetworkSystem.IsPlacementFree)
            {
                return unaffordable;
            }

            IReadOnlyList<TowerCombatDefinition> definitions = towerCatalog.Definitions;
            for (int index = 0; index < definitions.Count; index++)
            {
                TowerCombatDefinition definition = definitions[index];
                TowerEconomyProfile economy = definition?.Core?.Economy;
                if (economy != null && !goldSystem.CanAfford(economy.BuildCost))
                {
                    unaffordable.Add(definition);
                }
            }

            return unaffordable;
        }

        /// <summary>
        /// A tower stays locked until the player has actually beaten the level it is gated on.
        /// Unlocking is evaluated once per level entry: progress cannot change mid-level, and a
        /// tower unlocked here would otherwise appear the instant its own level is won.
        /// </summary>
        private IReadOnlyList<TowerCombatDefinition> CollectLockedDefinitions()
        {
            var locked = new List<TowerCombatDefinition>();
            IReadOnlyList<TowerCombatDefinition> definitions = towerCatalog.Definitions;
            for (int index = 0; index < definitions.Count; index++)
            {
                TowerCombatDefinition definition = definitions[index];
                int requiredLevel = definition == null ? 0 : definition.UnlockAfterClearingLevelNumber;
                bool tutorialUnlocked = levelNumber == 1
                    && definition != null
                    && IsTutorialTowerUnlocked(definition.Family);
                if (requiredLevel > 0
                    && !saveSystem.Progress.IsCleared(requiredLevel)
                    && !tutorialUnlocked)
                {
                    locked.Add(definition);
                }

                if (levelNumber == 1 && definition != null && !tutorialUnlocked)
                {
                    locked.Add(definition);
                }

                if (definition != null
                    && ShouldReserveFireGold()
                    && (definition.Family == TowerFamily.Generator
                        || definition.Family == TowerFamily.SoulNexus))
                {
                    locked.Add(definition);
                }

                if (definition?.Family == TowerFamily.Wind && levelNumber == 2)
                {
                    locked.Add(definition);
                }
            }

            return locked;
        }

        private bool AreTowerToolsUnlocked()
        {
            return levelNumber != 1 || tutorialProgress?.HasCompletedLevelOneTutorial == true;
        }

        private bool AreTowerActionsAvailable()
        {
            return AreTowerToolsUnlocked() && (levelNumber != 1 || CurrentWaveNumber >= 6);
        }

        // Water unlocks one wave ahead of the Stealth enemies it exists to reveal, which first
        // walk in on wave 6 of Level_001_Waves. The player therefore has it in hand, and has been
        // told what it is for, before the wave that needs it. WaterTowerHintTutorial is gated on
        // this same wave; the two have to move together.
        private const int WaterUnlockWaveNumber = 5;

        private bool IsTutorialTowerUnlocked(TowerFamily family)
        {
            if (tutorialProgress?.HasCompletedLevelOneTutorial == true)
            {
                return family == TowerFamily.Generator
                    || family == TowerFamily.SoulNexus
                    || family == TowerFamily.Fire
                    || family == TowerFamily.Water && CurrentWaveNumber >= WaterUnlockWaveNumber;
            }

            return family == TowerFamily.Generator && CurrentWaveNumber >= 1
                || family == TowerFamily.SoulNexus && CurrentWaveNumber >= 3
                || family == TowerFamily.Fire && CurrentWaveNumber >= 4
                || family == TowerFamily.Water && CurrentWaveNumber >= WaterUnlockWaveNumber;
        }

        private int CurrentWaveNumber => waveSystem?.CreateState().CurrentWaveNumber ?? 0;

        private bool ShouldReserveFireGold()
        {
            // A retried wave 3 has already been through this nudge, and the player is being told to
            // build more towers - locking the cards again would refuse the instruction on screen.
            if (levelNumber != 1
                || tutorialProgress?.HasCompletedLevelOneTutorial == true
                || tutorialProgress?.IsCompleted("second_wave_expansion_v1") != true
                || CurrentWaveNumber != 3
                || waveSystem?.CreateState().Phase != WavePhase.Preparation
                || waveSystem?.HasRetriedCurrentWave == true
                || goldSystem == null)
            {
                return false;
            }

            TowerCombatDefinition generator = FindDefinition(TowerFamily.Generator);
            return generator != null && goldSystem.Balance - generator.Core.Economy.BuildCost < 200;
        }

        private TowerCombatDefinition FindDefinition(TowerFamily family)
        {
            IReadOnlyList<TowerCombatDefinition> definitions = towerCatalog.Definitions;
            for (int index = 0; index < definitions.Count; index++)
            {
                if (definitions[index]?.Family == family) return definitions[index];
            }

            return null;
        }

        private void HandleTowerDragBegan(
            TowerCombatDefinition definition,
            TowerPlacementPointerEvent pointerEvent)
        {
            towerNetworkSystem.BeginTowerPlacementDrag(
                definition,
                pointerEvent.PointerId);
        }

        private void HandleTowerDragMoved(TowerPlacementPointerEvent pointerEvent)
        {
            towerNetworkSystem.UpdateTowerPlacementDrag(
                pointerEvent.PointerId,
                pointerEvent.ScreenPosition,
                pointerEvent.IsOverUi);
        }

        private void HandleTowerDragEnded(TowerPlacementPointerEvent pointerEvent)
        {
            towerNetworkSystem.EndTowerPlacementDrag(
                pointerEvent.PointerId,
                pointerEvent.ScreenPosition,
                pointerEvent.IsOverUi);
        }

        private void HandleTowerDragCanceled(int pointerId)
        {
            towerNetworkSystem.CancelTowerPlacementDrag(pointerId);
        }

        /// <summary>
        /// Projects the selected tower's anchor to screen space. A tower behind the camera
        /// projects to a mirrored point, so the negative depth case hides the panel instead.
        /// </summary>
        private bool TryGetTowerActionsPosition(
            ITowerRuntimeView selectedTower,
            out Vector2 screenPosition)
        {
            screenPosition = default;
            if (selectedTower == null || worldCamera == null)
            {
                return false;
            }

            Vector3 anchor = selectedTower.PresentationAnchor
                + Vector3.up * TowerActionsHeightMeters;
            Vector3 projected = worldCamera.WorldToScreenPoint(anchor);
            if (projected.z <= 0f)
            {
                return false;
            }

            screenPosition = new Vector2(projected.x, projected.y);
            return true;
        }

        /// <summary>
        /// Sells the selected tower, and says so when it refuses.
        /// </summary>
        /// <remarks>
        /// The HUD lost its feedback line, so a refusal used to be dropped on the floor: the
        /// button looked dead rather than declined, which is indistinguishable from a broken
        /// button. Until there is somewhere on screen to print it, the reason goes to the log
        /// so a refusal is at least diagnosable instead of invisible.
        /// </remarks>
        /// <summary>
        /// What the upgrade button says: a price, or MAX once the tower has no level left to buy.
        /// </summary>
        /// <remarks>
        /// Printing the price of a level that cannot be bought would read as a purchase the
        /// player merely cannot afford, which is a different problem with a different fix.
        /// </remarks>
        private static string CreateUpgradeCostText(bool hasUpgrade, bool atMaxLevel, int cost)
        {
            if (!hasUpgrade)
            {
                return string.Empty;
            }

            return atMaxLevel ? "TỐI ĐA" : cost.ToString();
        }

        private void HandleUpgradeRequested()
        {
            if (!towerNetworkSystem.TryUpgradeSelected(out string error))
            {
                Debug.LogWarning("Upgrade refused: " + error);
            }
        }

        private void HandleSellRequested()
        {
            if (!towerNetworkSystem.TrySellSelected(out string error))
            {
                Debug.LogWarning("Sell refused: " + error);
            }
        }

        private void HandleUnlinkRequested()
        {
            if (!towerNetworkSystem.TryUnlinkSelected(out string error))
            {
                Debug.LogWarning("Unlink refused: " + error);
            }
        }

        private void HandleReturnToMenu()
        {
            towerNetworkSystem.CancelPlacement();
            requestReturnToMenu();
        }

    }
}
