using System.Collections.Generic;
using NUnit.Framework;
using TowerDefense3D.Vfx;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.Vfx.Tests.EditMode
{
    /// <summary>
    /// The shared rig replaced one pooled instance per event, so nothing in the scene shows whether
    /// it actually emits. These tests drive it directly against the authored prefabs and read the
    /// particles back, which is the only check that distinguishes "emitting somewhere wrong" from
    /// "not emitting at all".
    /// </summary>
    public sealed class GlobalEffectEmitterViewTests
    {
        private static readonly string[] EffectPrefabPaths =
        {
            "Assets/Resources/Prefabs/VFX/Projectiles/Fire/Hit_Fire.prefab",
            "Assets/Resources/Prefabs/VFX/Projectiles/Water/Hit_Water.prefab",
            "Assets/Resources/Prefabs/VFX/Projectiles/Wind/Hit_Wind.prefab",
            "Assets/Resources/Prefabs/VFX/VFX_SocNhiet.prefab",
            "Assets/Resources/Prefabs/VFX/vfx_WindSwirl.prefab",
            "Assets/Resources/Prefabs/VFX/VFX_WaterKnock.prefab",
            "Assets/Resources/Prefabs/VFX/FX_Chicken.prefab"
        };

        private GameObject host;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Emitter Under Test");
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Play_EmitsParticlesForEveryAuthoredEffect()
        {
            var emitter = host.AddComponent<GlobalEffectEmitterView>();
            var failures = new List<string>();

            for (int index = 0; index < EffectPrefabPaths.Length; index++)
            {
                string path = EffectPrefabPaths[index];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, "Missing effect prefab at " + path);

                emitter.Play(prefab, new Vector3(12f, 0f, -7f));

                int total = 0;
                ParticleSystem[] systems = host.GetComponentsInChildren<ParticleSystem>(true);
                for (int s = 0; s < systems.Length; s++)
                {
                    total += systems[s].particleCount;
                }

                if (total == 0)
                {
                    failures.Add(System.IO.Path.GetFileNameWithoutExtension(path)
                        + ": rig emitted no particles at all");
                }

                emitter.Clear();
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        /// <summary>
        /// Both settings assume a throwaway instance and would break a rig that has to outlive
        /// every event: stopAction switches the object off when playback ends, and automatic
        /// culling pauses an idle rig because its empty bounds sit at the offscreen origin.
        /// </summary>
        [Test]
        public void Rig_ClearsTheSettingsThatAssumeAThrowawayInstance()
        {
            var emitter = host.AddComponent<GlobalEffectEmitterView>();
            var failures = new List<string>();

            for (int index = 0; index < EffectPrefabPaths.Length; index++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EffectPrefabPaths[index]);
                emitter.Play(prefab, Vector3.zero);
            }

            ParticleSystem[] systems = host.GetComponentsInChildren<ParticleSystem>(true);
            Assert.That(systems, Is.Not.Empty, "No rig was built.");

            for (int index = 0; index < systems.Length; index++)
            {
                ParticleSystem.MainModule main = systems[index].main;
                if (main.stopAction != ParticleSystemStopAction.None)
                {
                    failures.Add(systems[index].name + ": stopAction is " + main.stopAction);
                }

                if (main.cullingMode != ParticleSystemCullingMode.AlwaysSimulate)
                {
                    failures.Add(systems[index].name + ": cullingMode is " + main.cullingMode);
                }
            }

            Assert.That(failures, Is.Empty, string.Join(System.Environment.NewLine, failures));
        }

        [Test]
        public void Play_PutsParticlesAtTheRequestedWorldPosition()
        {
            var emitter = host.AddComponent<GlobalEffectEmitterView>();
            var target = new Vector3(12f, 0f, -7f);
            var failures = new List<string>();

            for (int index = 0; index < EffectPrefabPaths.Length; index++)
            {
                string path = EffectPrefabPaths[index];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                emitter.Play(prefab, target);

                ParticleSystem[] systems = host.GetComponentsInChildren<ParticleSystem>(true);
                for (int s = 0; s < systems.Length; s++)
                {
                    ParticleSystem system = systems[s];
                    if (system.particleCount == 0)
                    {
                        continue;
                    }

                    var particles = new ParticleSystem.Particle[system.particleCount];
                    int read = system.GetParticles(particles);
                    for (int p = 0; p < read; p++)
                    {
                        // GetParticles reports positions in the system's simulation space.
                        Vector3 world = system.main.simulationSpace == ParticleSystemSimulationSpace.Local
                            ? system.transform.TransformPoint(particles[p].position)
                            : particles[p].position;

                        float distance = Vector3.Distance(world, target);
                        if (distance > 6f)
                        {
                            failures.Add(string.Format(
                                "{0}/{1}: particle {2:0.0}m from the requested position (at {3})",
                                System.IO.Path.GetFileNameWithoutExtension(path),
                                system.name,
                                distance,
                                world));
                            break;
                        }
                    }
                }

                emitter.Clear();
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void WaterKnock_StaysAtGroundLevel()
        {
            var emitter = host.AddComponent<GlobalEffectEmitterView>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/VFX/VFX_WaterKnock.prefab");
            emitter.Play(prefab, Vector3.zero);

            ParticleSystem[] systems = host.GetComponentsInChildren<ParticleSystem>(true);
            Assert.That(systems, Has.Length.EqualTo(1));
            Assert.That(systems[0].name, Is.EqualTo("Big Ripples"));
            systems[0].Simulate(0.35f, true, false, false);
            float highestY = 0f;
            for (int index = 0; index < systems.Length; index++)
            {
                ParticleSystem system = systems[index];
                var particles = new ParticleSystem.Particle[system.particleCount];
                int count = system.GetParticles(particles);
                for (int particle = 0; particle < count; particle++)
                {
                    Vector3 world = system.main.simulationSpace == ParticleSystemSimulationSpace.Local
                        ? system.transform.TransformPoint(particles[particle].position)
                        : particles[particle].position;
                    highestY = Mathf.Max(highestY, world.y);
                }
            }

            Assert.That(highestY, Is.LessThanOrEqualTo(0.1f));
        }
    }
}
