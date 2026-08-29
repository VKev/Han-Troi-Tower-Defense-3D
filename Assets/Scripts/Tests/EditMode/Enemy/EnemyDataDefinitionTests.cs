using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense3D.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    public sealed class EnemyDataDefinitionTests
    {
        private const string CatalogPath = "Assets/Config/Enemies/EnemyCatalog.asset";
        private const string LocomotionControllerPath =
            "Assets/Resources/Animations/Enemies/EnemyLocomotion.controller";
        private const string BasicOverrideControllerPath =
            "Assets/Resources/Animations/Enemies/BasicEnemy.overrideController";

        [Test]
        public void ApprovedEnemyCatalog_ContainsSevenValidDefinitions()
        {
            EnemyCatalog catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.CollectValidationErrors(), Is.Empty);
            Assert.That(catalog.Definitions, Has.Count.EqualTo(7));

            EnemyDefinition basic = Get(catalog, "basic");
            EnemyDefinition armored = Get(catalog, "armored");
            EnemyDefinition magicResistant = Get(catalog, "magic-resistant");
            StealthEnemyDefinition stealth = Get<StealthEnemyDefinition>(catalog, "stealth");
            SpeedSupportEnemyDefinition speedSupport = Get<SpeedSupportEnemyDefinition>(catalog, "speed-support");
            EnemyDefinition miniBoss = Get(catalog, "mini-boss");
            SummonerBossEnemyDefinition summonerBoss =
                Get<SummonerBossEnemyDefinition>(catalog, "summoner-boss");

            Assert.That(basic.GetType(), Is.EqualTo(typeof(EnemyDefinition)));
            Assert.That(armored.GetType(), Is.EqualTo(typeof(EnemyDefinition)));
            Assert.That(magicResistant.GetType(), Is.EqualTo(typeof(EnemyDefinition)));
            Assert.That(miniBoss.GetType(), Is.EqualTo(typeof(EnemyDefinition)));
            Assert.That(miniBoss.Rank, Is.EqualTo(EnemyRank.MiniBoss));

            Assert.That(stealth.RevealDurationSeconds, Is.EqualTo(5f));
            Assert.That(speedSupport.RegularSpeedBonusFraction, Is.EqualTo(0.5f));
            Assert.That(speedSupport.MiniBossSpeedBonusFraction, Is.EqualTo(0.10f));
            Assert.That(miniBoss.LeakDamage, Is.EqualTo(1));
            Assert.That(summonerBoss.Rank, Is.EqualTo(EnemyRank.Boss));
            Assert.That(summonerBoss.LeakDamage, Is.EqualTo(5));
            Assert.That(summonerBoss.SummonPhases, Has.Count.EqualTo(3));
            Assert.That(summonerBoss.SummonPhases[0].StartHealthFraction, Is.EqualTo(1f));
            Assert.That(summonerBoss.SummonPhases[1].StartHealthFraction, Is.EqualTo(0.6f));
            Assert.That(summonerBoss.SummonPhases[2].StartHealthFraction, Is.EqualTo(0.3f));
        }

        [Test]
        public void ApprovedEnemyCatalog_UsesOneValidViewPrefabPerDefinition()
        {
            EnemyCatalog catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(CatalogPath);
            var viewPrefabs = new HashSet<GameObject>();

            Assert.That(catalog, Is.Not.Null);
            foreach (EnemyDefinition definition in catalog.Definitions)
            {
                Assert.That(definition.ViewPrefab, Is.Not.Null, definition.StableId);
                Assert.That(
                    definition.ViewPrefab.GetComponent<EnemyView>(),
                    Is.Not.Null,
                    $"{definition.StableId} View Prefab must have EnemyView on its root.");
                Animator animator = definition.ViewPrefab.GetComponent<Animator>();
                Assert.That(animator, Is.Not.Null, $"{definition.StableId} View Prefab must have an Animator.");
                Assert.That(
                    animator.runtimeAnimatorController,
                    Is.Not.Null,
                    $"{definition.StableId} View Prefab must have an Animator Controller.");
                Assert.That(
                    viewPrefabs.Add(definition.ViewPrefab),
                    Is.True,
                    $"{definition.StableId} must use its own View Prefab.");
            }
        }

        [Test]
        public void SpeedSupportEnemy_UsesChickenSkillEffectAtHeadBone()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/Enemies/SpeedSupportEnemy.prefab");
            GameObject effect = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/VFX/FX_Chicken.prefab");

            EnemySkillEffectView skillView = prefab.GetComponent<EnemySkillEffectView>();
            Assert.That(skillView, Is.Not.Null);

            SerializedObject serialized = new SerializedObject(skillView);
            Assert.That(serialized.FindProperty("effectPrefab").objectReferenceValue, Is.SameAs(effect));
            Transform anchor = serialized.FindProperty("anchor").objectReferenceValue as Transform;
            Assert.That(anchor, Is.Not.Null);
            Assert.That(anchor.name, Is.EqualTo("Bone_008"));
            Assert.That(anchor.IsChildOf(prefab.transform), Is.True);
        }

        [Test]
        public void ApprovedEnemyViews_UseEarthTrailForSpeedBuffs()
        {
            EnemyCatalog catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(CatalogPath);
            GameObject trail = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/VFX/VFX_Trail_Earth.prefab");

            foreach (EnemyDefinition definition in catalog.Definitions)
            {
                EnemySpeedTrailView trailView = definition.ViewPrefab.GetComponent<EnemySpeedTrailView>();
                Assert.That(trailView, Is.Not.Null, definition.StableId);

                SerializedObject serialized = new SerializedObject(trailView);
                Assert.That(
                    serialized.FindProperty("trailPrefab").objectReferenceValue,
                    Is.SameAs(trail),
                    definition.StableId);
            }
        }

        [Test]
        public void ApprovedEnemyAnimationAssets_UseSharedLocomotionContract()
        {
            AnimatorController locomotionController =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(LocomotionControllerPath);
            AnimatorOverrideController basicOverride =
                AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(BasicOverrideControllerPath);

            Assert.That(locomotionController, Is.Not.Null);
            Assert.That(basicOverride, Is.Not.Null);
            Assert.That(basicOverride.runtimeAnimatorController, Is.SameAs(locomotionController));
            Assert.That(basicOverride["EnemyIdle"].name, Is.EqualTo("Idle"));
            Assert.That(basicOverride["EnemyMove"].name, Is.EqualTo("Walk"));
            Assert.That(locomotionController.parameters, Has.Exactly(1).Matches<AnimatorControllerParameter>(
                parameter => parameter.name == "IsMoving" && parameter.type == AnimatorControllerParameterType.Bool));

            AnimatorStateMachine stateMachine = locomotionController.layers[0].stateMachine;
            Assert.That(stateMachine.defaultState.name, Is.EqualTo("Idle"));
            Assert.That(stateMachine.states, Has.Exactly(1).Matches<ChildAnimatorState>(
                childState => childState.state.name == "Idle"));
            Assert.That(stateMachine.states, Has.Exactly(1).Matches<ChildAnimatorState>(
                childState => childState.state.name == "Move"));

            EnemyCatalog catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(CatalogPath);
            GameObject basicPrefab = Get(catalog, "basic").ViewPrefab;
            Animator basicAnimator = basicPrefab.GetComponent<Animator>();
            Assert.That(basicPrefab.name, Is.EqualTo("BasicEnemy"));
            Assert.That(basicAnimator.runtimeAnimatorController, Is.SameAs(basicOverride));
            Assert.That(basicAnimator.avatar, Is.Not.Null);
            Assert.That(basicAnimator.avatar.isValid, Is.True);
            Assert.That(basicAnimator.avatar.isHuman, Is.False);
        }

        private static EnemyDefinition Get(EnemyCatalog catalog, string stableId)
        {
            Assert.That(catalog.TryGet(stableId, out EnemyDefinition definition), Is.True, stableId);
            return definition;
        }

        private static T Get<T>(EnemyCatalog catalog, string stableId) where T : EnemyDefinition
        {
            EnemyDefinition definition = Get(catalog, stableId);
            Assert.That(definition, Is.TypeOf<T>());
            return (T)definition;
        }
    }
}
