using NUnit.Framework;
using TowerDefense3D.Enemies;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.Waves.Tests.EditMode
{
    public sealed class WaveSpawnPlannerTests
    {
        private EnemyDefinition enemy;
        private WaveScheduleDefinition schedule;

        [SetUp]
        public void SetUp()
        {
            enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
            schedule = ScriptableObject.CreateInstance<WaveScheduleDefinition>();
            ConfigureSchedule();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(schedule);
        }

        [Test]
        public void CreatePlan_UsesStableSeedAndKeepsEverySpawnInsideWindow()
        {
            var planner = new WaveSpawnPlanner();

            var first = planner.CreatePlan(schedule, 0);
            var second = planner.CreatePlan(schedule, 0);

            Assert.That(first, Has.Count.EqualTo(3));
            for (int index = 0; index < first.Count; index++)
            {
                Assert.That(first[index].TimeSeconds, Is.EqualTo(second[index].TimeSeconds));
                Assert.That(first[index].TimeSeconds, Is.InRange(1f, 1.5f));
                Assert.That(first[index].Enemy, Is.SameAs(enemy));
            }
        }

        [Test]
        public void CreatePlan_PreservesSelectedRoadSpawn()
        {
            var serialized = new SerializedObject(schedule);
            SerializedProperty batch = serialized.FindProperty("waves")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("spawnBatches")
                .GetArrayElementAtIndex(0);
            batch.FindPropertyRelative("spawnPointIndex").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var plan = new WaveSpawnPlanner().CreatePlan(schedule, 0);

            Assert.That(plan, Is.Not.Empty);
            Assert.That(plan[0].SpawnPointIndex, Is.EqualTo(1));
        }

        private void ConfigureSchedule()
        {
            var serialized = new SerializedObject(schedule);
            serialized.FindProperty("randomSeed").intValue = 2718;
            SerializedProperty waves = serialized.FindProperty("waves");
            waves.arraySize = 1;
            SerializedProperty batches = waves.GetArrayElementAtIndex(0)
                .FindPropertyRelative("spawnBatches");
            batches.arraySize = 1;
            SerializedProperty batch = batches.GetArrayElementAtIndex(0);
            batch.FindPropertyRelative("enemy").objectReferenceValue = enemy;
            batch.FindPropertyRelative("count").intValue = 3;
            batch.FindPropertyRelative("startTimeSeconds").floatValue = 1f;
            batch.FindPropertyRelative("spawnWindowSeconds").floatValue = 0.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
