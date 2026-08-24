using NUnit.Framework;
using TowerDefense3D.Enemies;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.Waves.Tests.EditMode
{
    public sealed class WaveDataDefinitionTests
    {
        private const string BasicEnemyPath = "Assets/Config/Enemies/Basic.asset";

        private WaveScheduleDefinition schedule;

        [SetUp]
        public void SetUp()
        {
            schedule = ScriptableObject.CreateInstance<WaveScheduleDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(schedule);
        }

        [Test]
        public void AuthoredSchedule_ExposesSeedWaveAndSpawnBatchData()
        {
            EnemyDefinition basicEnemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(BasicEnemyPath);
            Assert.That(basicEnemy, Is.Not.Null);

            ConfigureSingleBatch(
                randomSeed: 2718,
                enemy: basicEnemy,
                count: 3,
                startTimeSeconds: 1f,
                spawnWindowSeconds: 0.5f);

            Assert.That(schedule.CollectValidationErrors(), Is.Empty);
            Assert.That(schedule.RandomSeed, Is.EqualTo(2718));
            Assert.That(schedule.Waves, Has.Count.EqualTo(1));

            EnemySpawnBatchDefinition batch = schedule.Waves[0].SpawnBatches[0];
            Assert.That(batch.Enemy, Is.SameAs(basicEnemy));
            Assert.That(batch.Count, Is.EqualTo(3));
            Assert.That(batch.StartTimeSeconds, Is.EqualTo(1f));
            Assert.That(batch.SpawnWindowSeconds, Is.EqualTo(0.5f));
        }

        [Test]
        public void InvalidSchedule_ReportsOnlyEssentialAuthoringErrors()
        {
            Assert.That(
                schedule.CollectValidationErrors(),
                Is.EqualTo(new[] { "Wave Schedule must contain at least one wave." }));

            ConfigureSingleWave();

            Assert.That(
                schedule.CollectValidationErrors(),
                Is.EqualTo(new[] { "Wave 1: At least one Spawn Batch is required." }));

            ConfigureSingleBatch(
                randomSeed: -1,
                enemy: null,
                count: 0,
                startTimeSeconds: -1f,
                spawnWindowSeconds: -1f);

            Assert.That(
                schedule.CollectValidationErrors(),
                Is.EqualTo(new[]
                {
                    "Wave 1, batch 1: Enemy is required.",
                    "Wave 1, batch 1: Count must be greater than zero.",
                    "Wave 1, batch 1: Start Time Seconds cannot be negative.",
                    "Wave 1, batch 1: Spawn Window Seconds cannot be negative."
                }));
        }

        private void ConfigureSingleWave()
        {
            var serializedSchedule = new SerializedObject(schedule);
            serializedSchedule.FindProperty("waves").arraySize = 1;
            serializedSchedule.ApplyModifiedPropertiesWithoutUndo();
        }

        private void ConfigureSingleBatch(
            int randomSeed,
            EnemyDefinition enemy,
            int count,
            float startTimeSeconds,
            float spawnWindowSeconds)
        {
            var serializedSchedule = new SerializedObject(schedule);
            serializedSchedule.FindProperty("randomSeed").intValue = randomSeed;

            SerializedProperty waves = serializedSchedule.FindProperty("waves");
            waves.arraySize = 1;
            SerializedProperty batches = waves.GetArrayElementAtIndex(0)
                .FindPropertyRelative("spawnBatches");
            batches.arraySize = 1;

            SerializedProperty batch = batches.GetArrayElementAtIndex(0);
            batch.FindPropertyRelative("enemy").objectReferenceValue = enemy;
            batch.FindPropertyRelative("count").intValue = count;
            batch.FindPropertyRelative("startTimeSeconds").floatValue = startTimeSeconds;
            batch.FindPropertyRelative("spawnWindowSeconds").floatValue = spawnWindowSeconds;
            serializedSchedule.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
