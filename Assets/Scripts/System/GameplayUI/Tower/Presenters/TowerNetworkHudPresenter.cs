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
            GrantEarnedTowers();
            towerNetworkHud.ApplyTowerLocks(CollectLockedDefinitions());
            towerNetworkHud.ApplyTowerAffordability(CollectUnbuildableDefinitions());
            towerNetworkHud.ApplyTowerBuildLimits(CollectMaxedDefinitions());
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
            GrantEarnedTowers();
            towerNetworkHud.ApplyTowerLocks(CollectLockedDefinitions());
            towerNetworkHud.ApplyTowerAffordability(CollectUnbuildableDefinitions());
            towerNetworkHud.ApplyTowerBuildLimits(CollectMaxedDefinitions());
            towerNetworkHud.SetTowerActionsAvailable(AreTowerToolsUnlocked());
            towerNetworkHud.SetUpgradeAvailable(IsUpgradeUnlocked());
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
            int currentUpgradeLevel = towerNetworkSystem.GetUpgradeLevel(selectedTower);

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
                selectedTower?.CombatDefinition?.Family,
                CreateUpgradeTierText(hasUpgrade, atMaxLevel, currentUpgradeLevel)));
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
        /// The towers the bar dims: the ones whose build cost is past the current balance, plus
        /// the ones the level already has as many of as it allows.
        /// </summary>
        /// <remarks>
        /// The price half is empty while a tutorial step is paying, so a card being handed over
        /// for free is never dimmed as though it were out of reach. The limit half still counts,
        /// because a hero already standing leaves no room whoever is paying.
        ///
        /// Dimmed rather than locked, and so still pressable: the tap is what tells the player
        /// why - either that they are short of gold, or that they have to sell the hero they
        /// already placed. A card that went dead would swallow the tap and say nothing.
        /// </remarks>
        /// <summary>
        /// The towers the level already has its fill of, whose cards quote "Tối đa" in place of a
        /// price until one of them is sold.
        /// </summary>
        private IReadOnlyList<TowerCombatDefinition> CollectMaxedDefinitions()
        {
            var maxed = new List<TowerCombatDefinition>();
            IReadOnlyList<TowerCombatDefinition> definitions = towerCatalog.Definitions;
            for (int index = 0; index < definitions.Count; index++)
            {
                TowerCombatDefinition definition = definitions[index];
                if (towerNetworkSystem.IsFamilyBuildLimitReached(definition))
                {
                    maxed.Add(definition);
                }
            }

            return maxed;
        }

        private IReadOnlyList<TowerCombatDefinition> CollectUnbuildableDefinitions()
        {
            var unbuildable = new List<TowerCombatDefinition>();
            bool priceApplies = goldSystem != null && !towerNetworkSystem.IsPlacementFree;

            IReadOnlyList<TowerCombatDefinition> definitions = towerCatalog.Definitions;
            for (int index = 0; index < definitions.Count; index++)
            {
                TowerCombatDefinition definition = definitions[index];
                if (definition == null)
                {
                    continue;
                }

                if (towerNetworkSystem.IsFamilyBuildLimitReached(definition))
                {
                    unbuildable.Add(definition);
                    continue;
                }

                TowerEconomyProfile economy = definition.Core?.Economy;
                if (priceApplies && economy != null && !goldSystem.CanAfford(economy.BuildCost))
                {
                    unbuildable.Add(definition);
                }
            }

            return unbuildable;
        }

        /// <summary>
        /// The towers the bar draws as unavailable.
        /// </summary>
        /// <remarks>
        /// A tower is buildable because the player owns it, full stop - not because the level
        /// they happen to be standing in would have granted it. The two used to be the same
        /// question, and the answer moved with the player: someone who had only ever met fire
        /// and water found wind waiting on a Level 1 replay, because Level 1 is not the level
        /// that withholds wind. Ownership is now recorded when it is earned (see
        /// <see cref="GrantEarnedTowers"/>) and read back here.
        ///
        /// On top of ownership sit two restrictions that belong to a run rather than to the
        /// player, and so are not recorded anywhere: Level 2 is authored around fire and water
        /// alone, and the wave 3 nudge holds back the gold the player is being told to spend on
        /// fire.
        /// </remarks>
        private IReadOnlyList<TowerCombatDefinition> CollectLockedDefinitions()
        {
            var locked = new List<TowerCombatDefinition>();
            IReadOnlyList<TowerCombatDefinition> definitions = towerCatalog.Definitions;
            for (int index = 0; index < definitions.Count; index++)
            {
                TowerCombatDefinition definition = definitions[index];
                if (definition == null)
                {
                    continue;
                }

                if (!saveSystem.Progress.IsTowerUnlocked(TowerUnlockId(definition)))
                {
                    locked.Add(definition);
                    continue;
                }

                if (ShouldReserveFireGold()
                    && (definition.Family == TowerFamily.Generator
                        || definition.Family == TowerFamily.SoulNexus))
                {
                    locked.Add(definition);
                }

                if (definition.Family == TowerFamily.Wind && levelNumber == 2)
                {
                    locked.Add(definition);
                }
            }

            return locked;
        }

        /// <summary>
        /// Records every tower this run has earned the player, so they are theirs from now on.
        /// </summary>
        /// <remarks>
        /// Run on every refresh rather than once on entry, because the Level 1 tutorial earns
        /// its four towers wave by wave and the player owns each one the moment it is handed
        /// over. Towers already on record cost nothing: the set does not change, so nothing is
        /// written. The buffer is reused for the same reason - this runs every frame.
        /// </remarks>
        private void GrantEarnedTowers()
        {
            earnedTowerBuffer.Clear();
            IReadOnlyList<TowerCombatDefinition> definitions = towerCatalog.Definitions;
            for (int index = 0; index < definitions.Count; index++)
            {
                TowerCombatDefinition definition = definitions[index];
                if (definition != null && IsEarnedHere(definition))
                {
                    earnedTowerBuffer.Add(TowerUnlockId(definition));
                }
            }

            if (saveSystem.TryUnlockTowersAndSave(earnedTowerBuffer, out SaveWriteResult write)
                && !write.IsSuccess)
            {
                // Worth saying out loud but not worth refusing the towers over: the player has
                // earned them, and taking them back because the disk is unhappy would make a
                // storage fault look like a gameplay rule.
                Debug.LogWarning("Could not save a tower unlock: " + write.Error);
            }
        }

        private readonly List<string> earnedTowerBuffer = new List<string>();

        /// <summary>
        /// Whether this run is what earns <paramref name="definition"/> for the player.
        /// </summary>
        /// <remarks>
        /// This is the progression table, and the only place a tower is ever granted:
        /// <list type="bullet">
        /// <item>The Crab hero is earned by clearing the level it is gated on.</item>
        /// <item>Wind is earned by reaching Level 3, the level authored to teach it. Level 2 is
        /// fire and water only, so playing it must not hand wind over.</item>
        /// <item>The starter four are earned across the Level 1 tutorial, wave by wave. Any
        /// other level means that tutorial is behind the player, so all four are theirs - which
        /// is also what re-earns them for a save written before ownership was recorded.</item>
        /// </list>
        /// </remarks>
        private bool IsEarnedHere(TowerCombatDefinition definition)
        {
            int requiredLevel = definition.UnlockAfterClearingLevelNumber;
            if (requiredLevel > 0)
            {
                return saveSystem.Progress.IsCleared(requiredLevel);
            }

            if (definition.Family == TowerFamily.Wind)
            {
                return levelNumber >= WindUnlockLevelNumber;
            }

            return !IsLevelOneTutorialRun || IsTutorialTowerUnlocked(definition.Family);
        }

        /// <summary>
        /// How a tower is named in the save. The family, because the catalog carries exactly one
        /// definition per family and a family outlives any asset rename.
        /// </summary>
        private static string TowerUnlockId(TowerCombatDefinition definition)
        {
            return definition.Family.ToString();
        }

        /// <summary>
        /// Whether this run of Level 1 is still the tutorial, which hands the towers out one at
        /// a time at the waves that teach them.
        /// </summary>
        /// <remarks>
        /// A replay of Level 1 after the tutorial is an ordinary run: the drip-feed has nothing
        /// left to teach, so everything the player owns is on the bar from the first wave.
        /// </remarks>
        private bool IsLevelOneTutorialRun =>
            levelNumber == 1 && tutorialProgress?.HasCompletedLevelOneTutorial != true;

        /// <summary>
        /// The level that teaches wind, and so the level that earns it.
        /// </summary>
        private const int WindUnlockLevelNumber = 3;

        /// <summary>
        /// Whether the player owns the tower tools - unlink, sell and upgrade - and so whether
        /// tapping a tower may open the actions panel over it.
        /// </summary>
        /// <remarks>
        /// Level 1 holds them back until its tutorial has run, because the tutorial drives those
        /// same controls itself while it is teaching them. The moment it ends - on wave 4, having
        /// just walked the player through tapping a tower and unlinking it - they are the
        /// player's to use.
        /// </remarks>
        private bool AreTowerToolsUnlocked()
        {
            return levelNumber != 1 || tutorialProgress?.HasCompletedLevelOneTutorial == true;
        }

        /// <summary>
        /// Whether the upgrade button belongs on the actions panel. Level 1 teaches linking and
        /// unlinking and nothing else; upgrading has its own beat at Level 2, and until the player
        /// has met it the button is one they have no reason to press.
        /// </summary>
        private bool IsUpgradeUnlocked()
        {
            return levelNumber != 1;
        }

        // Water unlocks one wave ahead of the Stealth enemies it exists to reveal, which first
        // walk in on wave 6 of Level_001_Waves. The player therefore has it in hand, and has been
        // told what it is for, before the wave that needs it. WaterTowerHintTutorial is gated on
        // this same wave; the two have to move together.
        private const int WaterUnlockWaveNumber = 5;

        private bool IsTutorialTowerUnlocked(TowerFamily family)
        {
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
        /// Price printed below the upgrade glyph while another level can be bought.
        /// </summary>
        /// <remarks>
        /// Printing the price of a level that cannot be bought would read as a purchase the
        /// player merely cannot afford, which is a different problem with a different fix.
        /// </remarks>
        private static string CreateUpgradeCostText(
            bool hasUpgrade,
            bool atMaxLevel,
            int cost)
        {
            return hasUpgrade && !atMaxLevel ? cost.ToString() : string.Empty;
        }

        private static string CreateUpgradeTierText(
            bool hasUpgrade,
            bool atMaxLevel,
            int currentLevel)
        {
            return !hasUpgrade ? string.Empty : atMaxLevel ? "MAX" : (currentLevel + 1).ToString();
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
