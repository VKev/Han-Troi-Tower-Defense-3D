using NUnit.Framework;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    [TestFixture]
    public sealed class LevelStarRatingTests
    {
        [Test]
        public void Rating_AwardsThreeStarsOnlyWhenNoHealthWasLost()
        {
            Assert.That(LevelStarRating.FromRemainingHealth(20, 20), Is.EqualTo(3));
            Assert.That(
                LevelStarRating.FromRemainingHealth(19, 20),
                Is.EqualTo(2),
                "Losing a single point has to cost the third star.");
        }

        [Test]
        public void Rating_AwardsTwoStarsDownToExactlyHalfHealth()
        {
            Assert.That(LevelStarRating.FromRemainingHealth(10, 20), Is.EqualTo(2), "Half counts as two.");
            Assert.That(LevelStarRating.FromRemainingHealth(9, 20), Is.EqualTo(1));
        }

        [Test]
        public void Rating_SplitsAnOddStartingHealthWithoutRounding()
        {
            // 25 has no exact half: 13 is over it and 12 is under, and a float ratio comparison
            // is where that distinction goes missing.
            Assert.That(LevelStarRating.FromRemainingHealth(13, 25), Is.EqualTo(2));
            Assert.That(LevelStarRating.FromRemainingHealth(12, 25), Is.EqualTo(1));
        }

        [Test]
        public void Rating_ScoresNothingForARunWithNoHealthLeft()
        {
            Assert.That(
                LevelStarRating.FromRemainingHealth(0, 20),
                Is.EqualTo(LevelStarRating.NoStars),
                "A fallen Cóc has not cleared the level, so it cannot earn the one star a bare victory does.");
            Assert.That(LevelStarRating.FromRemainingHealth(-5, 20), Is.EqualTo(LevelStarRating.NoStars));
            Assert.That(LevelStarRating.FromRemainingHealth(5, 0), Is.EqualTo(LevelStarRating.NoStars));
        }

        [Test]
        public void Progress_KeepsTheBestScoreAcrossReplays()
        {
            var progress = new UnlockProgress();

            Assert.That(progress.TryMarkCleared(3, 1), Is.EqualTo(UnlockAttemptResult.Unlocked));
            Assert.That(progress.GetStars(3), Is.EqualTo(1));
            Assert.That(progress.IsCleared(3), Is.True);
            Assert.That(progress.IsUnlocked(3), Is.True, "Clearing a level implies having reached it.");

            Assert.That(
                progress.TryMarkCleared(3, 3),
                Is.EqualTo(UnlockAttemptResult.Unlocked),
                "A better run is worth a save write.");
            Assert.That(progress.GetStars(3), Is.EqualTo(3));

            Assert.That(
                progress.TryMarkCleared(3, 1),
                Is.EqualTo(UnlockAttemptResult.AlreadyUnlocked),
                "A worse replay changes nothing, so it must not cost a write.");
            Assert.That(progress.GetStars(3), Is.EqualTo(3), "A worse replay must not take back a score.");
        }

        [Test]
        public void Progress_ReportsNoScoreForALevelNeverBeaten()
        {
            var progress = new UnlockProgress();
            progress.TryUnlock(2);

            Assert.That(progress.GetStars(2), Is.EqualTo(LevelStarRating.NoStars));
            Assert.That(progress.TotalStars, Is.Zero);
        }

        [Test]
        public void Progress_SumsEveryRecordedScore()
        {
            var progress = new UnlockProgress();
            progress.TryMarkCleared(1, 3);
            progress.TryMarkCleared(2, 2);

            Assert.That(progress.TotalStars, Is.EqualTo(5));
        }

        [Test]
        public void Progress_RestoresScoresAndDropsOnesTheSaveShouldNotCarry()
        {
            var progress = new UnlockProgress(
                new[] { 1, 2, 3, 4 },
                new[] { 1, 2 },
                new[]
                {
                    new LevelStarRecord(1, 3),
                    new LevelStarRecord(2, 2),

                    // Reached but never beaten, so a score for it is a corrupted save.
                    new LevelStarRecord(3, 3),
                    new LevelStarRecord(4, 9),
                    new LevelStarRecord(-1, 2)
                });

            Assert.That(progress.GetStars(1), Is.EqualTo(3));
            Assert.That(progress.GetStars(2), Is.EqualTo(2));
            Assert.That(progress.GetStars(3), Is.EqualTo(LevelStarRating.NoStars));
            Assert.That(progress.GetStars(4), Is.EqualTo(LevelStarRating.NoStars));
            Assert.That(progress.TotalStars, Is.EqualTo(5));
        }

        [Test]
        public void Snapshot_RoundTripsScoresInLevelOrder()
        {
            var progress = new UnlockProgress();
            progress.TryMarkCleared(3, 1);
            progress.TryMarkCleared(1, 3);

            LevelStarRecord[] records = progress.CreateSortedStarSnapshot();

            Assert.That(records.Length, Is.EqualTo(2));
            Assert.That(records[0].LevelNumber, Is.EqualTo(1));
            Assert.That(records[0].Stars, Is.EqualTo(3));
            Assert.That(records[1].LevelNumber, Is.EqualTo(3));
            Assert.That(records[1].Stars, Is.EqualTo(1));

            SaveSnapshot snapshot = SaveSnapshot.Create(
                progress.CreateSortedSnapshot(),
                progress.CreateSortedClearedSnapshot(),
                records,
                "2026-01-01T00:00:00.0000000Z",
                "1.0.0");

            Assert.That(snapshot.TryValidate(out string error), Is.True, error);
            Assert.That(snapshot.LevelStars.Length, Is.EqualTo(2));
        }

        [Test]
        public void Snapshot_WrittenBeforeScoringExistedLoadsAsNoScores()
        {
            SaveSnapshot snapshot = SaveSnapshot.Create(
                new[] { 1, 2 },
                new[] { 1 },
                "2026-01-01T00:00:00.0000000Z",
                "1.0.0");

            Assert.That(snapshot.TryValidate(out string error), Is.True, error);
            Assert.That(snapshot.LevelStars, Is.Empty);

            var progress = new UnlockProgress(
                snapshot.UnlockedLevelNumbers,
                snapshot.ClearedLevelNumbers,
                snapshot.LevelStars);

            Assert.That(progress.IsCleared(1), Is.True);
            Assert.That(progress.GetStars(1), Is.EqualTo(LevelStarRating.NoStars));
        }
    }
}
