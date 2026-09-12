using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    /// <summary>
    /// What clearing a level pays into the wallet, and what a replay of one is owed.
    /// </summary>
    [TestFixture]
    public sealed class LevelClearRewardTests
    {
        private const int FullReward = 300;

        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(
                Path.GetTempPath(),
                "TowerDefense3D.GameFlow.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);
        }

        [TearDown]
        public void TearDown()
        {
            string allowedRoot = Path.GetFullPath(
                Path.Combine(Path.GetTempPath(), "TowerDefense3D.GameFlow.Tests"));
            string candidate = Path.GetFullPath(testRoot);
            if (candidate.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(candidate))
            {
                Directory.Delete(candidate, true);
            }
        }

        [Test]
        public void GoldForStars_PaysAThirdOfThePursePerStar()
        {
            Assert.That(LevelStarRating.GoldForStars(FullReward, 0), Is.Zero);
            Assert.That(LevelStarRating.GoldForStars(FullReward, 1), Is.EqualTo(100));
            Assert.That(LevelStarRating.GoldForStars(FullReward, 2), Is.EqualTo(200));
            Assert.That(LevelStarRating.GoldForStars(FullReward, 3), Is.EqualTo(300));
        }

        /// <summary>
        /// A purse that does not divide by three still pays out to exactly the purse at three
        /// stars, which is what stops a player losing change for taking three runs to get there.
        /// </summary>
        [Test]
        public void GoldForStars_LosesNoChangeOnAPurseThatDoesNotDivideByThree()
        {
            Assert.That(LevelStarRating.GoldForStars(250, 1), Is.EqualTo(83));
            Assert.That(LevelStarRating.GoldForStars(250, 2), Is.EqualTo(166));
            Assert.That(LevelStarRating.GoldForStars(250, 3), Is.EqualTo(250));
        }

        [Test]
        public void FirstClear_PaysTheShareOfThePurseItsScoreIsWorth()
        {
            using (var harness = new Harness(testRoot))
            {
                harness.Flow.MarkLevelCleared(1, 2);
                harness.ShowMenu();

                Assert.That(harness.Save.Progress.Gold, Is.EqualTo(200));
                Assert.That(harness.LastReward().StarsGained, Is.EqualTo(2));
                Assert.That(harness.LastReward().GoldGained, Is.EqualTo(200));
            }
        }

        [Test]
        public void BetterReplay_PaysOnlyTheDifferenceAndReportsOnlyTheNewStar()
        {
            using (var harness = new Harness(testRoot))
            {
                harness.Flow.MarkLevelCleared(1, 2);
                harness.ShowMenu();

                harness.Flow.MarkLevelCleared(1, 3);
                harness.ShowMenu();

                Assert.That(
                    harness.Save.Progress.Gold,
                    Is.EqualTo(FullReward),
                    "Two runs to three stars must pay exactly what one run to three stars pays.");
                Assert.That(harness.LastReward().StarsGained, Is.EqualTo(1));
                Assert.That(harness.LastReward().GoldGained, Is.EqualTo(100));
            }
        }

        [Test]
        public void ReplayOfAThreeStarLevel_PaysNothingAndAsksForNoAnimation()
        {
            using (var harness = new Harness(testRoot))
            {
                harness.Flow.MarkLevelCleared(1, 3);
                harness.ShowMenu();

                harness.Flow.MarkLevelCleared(1, 3);
                harness.ShowMenu();

                Assert.That(harness.Save.Progress.Gold, Is.EqualTo(FullReward));
                Assert.That(
                    harness.LastReward().HasAnything,
                    Is.False,
                    "Nothing was earned, so the journey screen has nothing to fly.");
            }
        }

        /// <summary>
        /// A sloppy replay takes nothing back - the score on record does not drop either.
        /// </summary>
        [Test]
        public void WorseReplay_PaysNothingAndTakesNothingBack()
        {
            using (var harness = new Harness(testRoot))
            {
                harness.Flow.MarkLevelCleared(1, 3);
                harness.ShowMenu();

                harness.Flow.MarkLevelCleared(1, 1);
                harness.ShowMenu();

                Assert.That(harness.Save.Progress.Gold, Is.EqualTo(FullReward));
                Assert.That(harness.Save.Progress.GetStars(1), Is.EqualTo(3));
                Assert.That(harness.LastReward().HasAnything, Is.False);
            }
        }

        [Test]
        public void PendingReward_IsSpentByTheFirstLookAtTheMenu()
        {
            using (var harness = new Harness(testRoot))
            {
                harness.Flow.MarkLevelCleared(1, 3);

                harness.ShowMenu();
                Assert.That(harness.LastReward().GoldGained, Is.EqualTo(FullReward));

                harness.ShowMenu();
                Assert.That(
                    harness.LastReward().HasAnything,
                    Is.False,
                    "Re-opening the menu must not replay a payout the player has already been shown.");
            }
        }

        /// <summary>
        /// Beating two levels back to back from the victory panel banks both payouts, and the
        /// menu shows the whole haul arriving out of the level the player came from.
        /// </summary>
        [Test]
        public void RewardsEarnedBetweenVisits_ArriveTogether()
        {
            using (var harness = new Harness(testRoot))
            {
                harness.Flow.MarkLevelCleared(1, 3);
                harness.Flow.MarkLevelCleared(2, 3);

                harness.ShowMenu();

                LevelMenuRewardState reward = harness.LastReward();
                Assert.That(reward.GoldGained, Is.EqualTo(FullReward * 2));
                Assert.That(reward.StarsGained, Is.EqualTo(6));
                Assert.That(reward.LevelNumber, Is.EqualTo(2), "The flight leaves the node just played.");
            }
        }

        [Test]
        public void Wallet_SurvivesASaveRoundTrip()
        {
            var repository = new LocalSaveRepository(testRoot);
            var save = new SaveSystem(repository, "test");
            save.Initialize();
            save.TryMarkClearedAndSave(1, 3, 275, out SaveWriteResult write);
            Assert.That(write.IsSuccess, Is.True, write.Error);

            var reloaded = new SaveSystem(repository, "test");
            reloaded.Initialize();

            Assert.That(reloaded.Progress.Gold, Is.EqualTo(275));
        }

        [Test]
        public void SaveWrittenBeforeTheWalletExisted_LoadsAsAnEmptyWallet()
        {
            SaveSnapshot snapshot = SaveSnapshot.Create(
                new[] { 1, 2 },
                new[] { 1 },
                "2026-01-01T00:00:00.0000000Z",
                "1.0.0");

            Assert.That(snapshot.TryValidate(out string error), Is.True, error);
            Assert.That(snapshot.Gold, Is.Zero);

            var progress = new UnlockProgress(
                snapshot.UnlockedLevelNumbers,
                snapshot.ClearedLevelNumbers,
                snapshot.LevelStars,
                snapshot.Gold);

            Assert.That(progress.Gold, Is.Zero);
        }

        private sealed class Harness : IDisposable
        {
            private readonly LevelCatalog catalog;
            private readonly RecordingLevelMenuUIView view;
            private readonly GameFlowSystem gameFlowSystem;

            public Harness(string testRoot)
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                SetCatalogEntries(
                    catalog,
                    new LevelCatalogEntry(1, "Level 1", "Assets/Scenes/Levels/Level_001.unity", 440, 20, FullReward),
                    new LevelCatalogEntry(2, "Level 2", "Assets/Scenes/Levels/Level_002.unity", 440, 20, FullReward));

                Save = new SaveSystem(new LocalSaveRepository(testRoot), "test");
                Save.Initialize();

                view = new RecordingLevelMenuUIView();
                var ui = new ApplicationUISystem(view);
                Flow = new LevelMenuFlow(catalog, Save, ui);
                gameFlowSystem = new GameFlowSystem(
                    new ApplicationBootFlow(catalog, Save, ui),
                    Flow,
                    new LevelTransitionFlow(new LevelSceneSystem(new NullLevelSceneGateway()), ui),
                    new SaveRecoveryFlow(Save, ui));
                gameFlowSystem.Start();
            }

            public SaveSystem Save { get; }
            public LevelMenuFlow Flow { get; }

            public void ShowMenu()
            {
                Flow.Show();
            }

            /// <summary>
            /// What the journey screen was last handed. Only <see cref="ShowMenu"/> puts anything
            /// here - the payout is banked when the level is cleared but not reported until the
            /// player actually looks at the screen it belongs to - so every assertion on it has
            /// to open the menu first.
            /// </summary>
            public LevelMenuRewardState LastReward()
            {
                return view.LastReward;
            }

            public void Dispose()
            {
                gameFlowSystem.Dispose();
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        private static void SetCatalogEntries(LevelCatalog catalog, params LevelCatalogEntry[] entries)
        {
            FieldInfo field = typeof(LevelCatalog).GetField(
                "levels",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(catalog, new List<LevelCatalogEntry>(entries));
        }

        private sealed class RecordingLevelMenuUIView : IApplicationUIView
        {
            public LevelMenuRewardState LastReward { get; private set; }

            public void ShowLevelMenu(
                IReadOnlyList<LevelMenuItemState> levels,
                int goldTotal,
                LevelMenuRewardState reward,
                Action<int> onLevelSelected)
            {
                LastReward = reward;
            }

            public void Reset()
            {
            }

            public void HideLevelMenu()
            {
            }

            public void ShowLoading(string message)
            {
            }

            public void HideLoading()
            {
            }

            public void CoverScreen(Action onCovered)
            {
                onCovered?.Invoke();
            }

            public void UncoverScreen(Action onUncovered)
            {
                onUncovered?.Invoke();
            }

            public void ShowBlockingError(string message, Action retry, Action startNew)
            {
            }

            public void HideBlockingError()
            {
            }

            public void ShowSaveWarning(string message, Action retrySave)
            {
            }

            public void HideSaveWarning()
            {
            }

            public void SetInputBlocked(bool isBlocked)
            {
            }
        }

        /// <summary>
        /// Never reports back: these tests never load a scene, and a gateway that completed
        /// instantly would drive the flow into states they are not about.
        /// </summary>
        private sealed class NullLevelSceneGateway : ILevelSceneGateway
        {
            public void LoadLevel(
                LevelLoadRequest request,
                Action<LevelSceneHandle, LevelTransitionResult> completion)
            {
            }

            public void UnloadLevel(
                LevelSceneHandle handle,
                Action<LevelTransitionResult> completion)
            {
            }
        }
    }
}
