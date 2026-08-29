using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Vfx
{
    /// <summary>
    /// Plays authored one-shot effects from one shared rig per effect, instead of one pooled
    /// instance per event. Draw calls then stop scaling with how many events overlap: ten
    /// simultaneous hits share the same particle systems.
    ///
    /// The authored look is preserved because the rig keeps every system the effect was authored
    /// with - same materials, render modes, over-lifetime curves, sub-emitters - and only the
    /// emission is driven from code. Two rules make that exact:
    /// <list type="bullet">
    /// <item>The rig sits at the origin with an identity transform, so systems authored in Local
    /// simulation space behave the same as they did on an instance placed at the event position.
    /// Moving this object breaks that and will drag Local-space particles with it.</item>
    /// <item>Emission carries <c>applyShapeToPosition</c>, so a system with a Shape module still
    /// spreads its particles instead of collapsing them onto a single point.</item>
    /// </list>
    /// Effects that must follow a moving owner cannot use this: their particles live in the rig's
    /// space, not the owner's. That rules out projectile bodies, element marks and shields.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlobalEffectEmitterView : MonoBehaviour
    {
        private readonly Dictionary<GameObject, EffectRig> rigsByPrefab =
            new Dictionary<GameObject, EffectRig>();
        private readonly List<PendingBurst> pendingBursts = new List<PendingBurst>();
        private readonly List<PendingRateEmission> pendingRateEmissions =
            new List<PendingRateEmission>();

        /// <summary>
        /// Plays <paramref name="effectPrefab"/> at <paramref name="position"/>. The rig for that
        /// prefab is built on first use and reused for every later call.
        /// </summary>
        public void Play(GameObject effectPrefab, Vector3 position)
        {
            if (effectPrefab == null)
            {
                return;
            }

            EffectRig rig = GetRig(effectPrefab);
            for (int index = 0; index < rig.Emitters.Length; index++)
            {
                EffectEmitter emitter = rig.Emitters[index];
                for (int burst = 0; burst < emitter.Bursts.Length; burst++)
                {
                    BurstSchedule schedule = emitter.Bursts[burst];
                    if (schedule.DelaySeconds <= 0f)
                    {
                        Emit(emitter, position, schedule.Count);
                        continue;
                    }

                    // Authored bursts can start late; firing them immediately would compress the
                    // effect's timing into a single frame.
                    pendingBursts.Add(new PendingBurst(
                        emitter,
                        position,
                        schedule.Count,
                        schedule.DelaySeconds));
                }

                if (emitter.RatePerSecond > 0f && emitter.DurationSeconds > 0f)
                {
                    float initialSeconds = Mathf.Min(0.1f, emitter.DurationSeconds);
                    Emit(
                        emitter,
                        position,
                        Mathf.CeilToInt(emitter.RatePerSecond * initialSeconds));
                    pendingRateEmissions.Add(new PendingRateEmission(
                        emitter,
                        position,
                        emitter.DurationSeconds - initialSeconds));
                }
            }
        }

        public void Clear()
        {
            pendingBursts.Clear();
            pendingRateEmissions.Clear();
            foreach (EffectRig rig in rigsByPrefab.Values)
            {
                for (int index = 0; index < rig.Emitters.Length; index++)
                {
                    rig.Emitters[index].System.Clear(true);
                }
            }
        }

        private void Update()
        {
            if (pendingBursts.Count == 0 && pendingRateEmissions.Count == 0)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            for (int index = pendingBursts.Count - 1; index >= 0; index--)
            {
                PendingBurst pending = pendingBursts[index];
                pending.RemainingSeconds -= deltaTime;
                if (pending.RemainingSeconds > 0f)
                {
                    pendingBursts[index] = pending;
                    continue;
                }

                Emit(pending.Emitter, pending.Position, pending.Count);
                pendingBursts.RemoveAt(index);
            }

            for (int index = pendingRateEmissions.Count - 1; index >= 0; index--)
            {
                PendingRateEmission pending = pendingRateEmissions[index];
                float activeSeconds = Mathf.Min(deltaTime, pending.RemainingSeconds);
                pending.FractionalCount += pending.Emitter.RatePerSecond * activeSeconds;
                int count = Mathf.FloorToInt(pending.FractionalCount);
                if (count > 0)
                {
                    pending.FractionalCount -= count;
                    Emit(pending.Emitter, pending.Position, count);
                }

                pending.RemainingSeconds -= deltaTime;
                if (pending.RemainingSeconds > 0f)
                {
                    pendingRateEmissions[index] = pending;
                }
                else
                {
                    pendingRateEmissions.RemoveAt(index);
                }
            }
        }

        private static void Emit(EffectEmitter emitter, Vector3 worldPosition, int count)
        {
            ParticleSystem system = emitter.System;
            if (count <= 0 || system == null)
            {
                return;
            }

            // EmitParams.position is read in the system's own simulation space. A Local-space
            // system therefore needs the world position converted, and the rig keeps the authored
            // root scale, so passing world coordinates straight through would place particles at
            // scale times the intended distance from the origin.
            Vector3 position = system.main.simulationSpace == ParticleSystemSimulationSpace.Local
                ? system.transform.InverseTransformPoint(worldPosition)
                : worldPosition;

            var emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                applyShapeToPosition = true
            };
            system.Emit(emitParams, count);
        }

        private EffectRig GetRig(GameObject effectPrefab)
        {
            if (rigsByPrefab.TryGetValue(effectPrefab, out EffectRig existing))
            {
                return existing;
            }

            var rig = EffectRig.Build(effectPrefab, transform);
            rigsByPrefab.Add(effectPrefab, rig);
            return rig;
        }

        private sealed class EffectRig
        {
            private EffectRig(EffectEmitter[] emitters)
            {
                Emitters = emitters;
            }

            public EffectEmitter[] Emitters { get; }

            public static EffectRig Build(GameObject effectPrefab, Transform parent)
            {
                GameObject instance = Instantiate(effectPrefab, parent);
                instance.name = effectPrefab.name + " (global)";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.SetActive(true);

                NeutraliseAutoClear(instance);

                ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
                HashSet<ParticleSystem> driven = CollectSubEmitters(systems);
                var emitters = new List<EffectEmitter>(systems.Length);

                for (int index = 0; index < systems.Length; index++)
                {
                    ParticleSystem system = systems[index];
                    system.gameObject.SetActive(true);
                    KeepAliveForReuse(system);

                    // Every system, including a sub-emitter, must have its authored automatic
                    // emission stopped. Otherwise a sub-emitter also plays from its parked
                    // transform at the origin instead of only when its parent particle triggers it.
                    ParticleSystem.EmissionModule emission = system.emission;
                    emission.enabled = false;

                    // Sub-emitters are spawned by their parent's particles. Emitting into them
                    // directly would place their particles at the event position instead of at
                    // the parent particle that should have produced them.
                    if (driven.Contains(system))
                    {
                        continue;
                    }

                    BurstSchedule[] bursts = ReadBursts(system);
                    float ratePerSecond = emission.rateOverTime.constantMax;
                    ParticleSystem.MainModule main = system.main;
                    float durationSeconds = main.loop ? 1f : main.duration;

                    // The authored emission module is replaced by explicit Emit calls, and the
                    // system is kept alive so its particles keep simulating between events.
                    main.loop = true;
                    main.playOnAwake = false;

                    system.Play();

                    emitters.Add(new EffectEmitter(
                        system,
                        bursts,
                        ratePerSecond,
                        durationSeconds));
                }

                return new EffectRig(emitters.ToArray());
            }

            /// <summary>
            /// Clears the two authored settings that assume a throwaway instance. Applied to every
            /// system including sub-emitters, which are otherwise left alone.
            /// </summary>
            private static void KeepAliveForReuse(ParticleSystem system)
            {
                ParticleSystem.MainModule main = system.main;

                // The authored stop action disables or destroys the object when playback ends. The
                // pooled instances this replaced cleared it for the same reason: on a rig that has
                // to survive every later event it would switch the effect off for good.
                main.stopAction = ParticleSystemStopAction.None;

                // Automatic culling pauses a system whose bounds are offscreen. An idle rig has no
                // particles, so its bounds are a point at the origin - usually outside the camera -
                // and the system would be paused at the moment an event tries to emit into it.
                // Nothing about the look changes; only the culling decision does.
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            }

            /// <summary>
            /// Authored effects can carry a component that disables or destroys the object once
            /// the effect finishes - Cartoon FX does this by default. On a shared rig that would
            /// switch the whole thing off after the first event, so the behaviour is turned off.
            /// </summary>
            private static void NeutraliseAutoClear(GameObject instance)
            {
                MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = 0; index < behaviours.Length; index++)
                {
                    MonoBehaviour behaviour = behaviours[index];
                    if (behaviour == null)
                    {
                        continue;
                    }

                    System.Reflection.FieldInfo field =
                        behaviour.GetType().GetField("clearBehavior");
                    if (field == null || !field.FieldType.IsEnum)
                    {
                        continue;
                    }

                    // 0 is None in CFXR_Effect.ClearBehavior { None, Disable, Destroy }.
                    field.SetValue(behaviour, System.Enum.ToObject(field.FieldType, 0));
                }
            }

            private static BurstSchedule[] ReadBursts(ParticleSystem system)
            {
                ParticleSystem.EmissionModule emission = system.emission;
                int count = emission.burstCount;
                if (count == 0)
                {
                    return new BurstSchedule[0];
                }

                var bursts = new ParticleSystem.Burst[count];
                emission.GetBursts(bursts);
                var schedules = new List<BurstSchedule>(count);
                for (int index = 0; index < count; index++)
                {
                    ParticleSystem.Burst burst = bursts[index];
                    int cycles = burst.cycleCount <= 0 ? 1 : burst.cycleCount;
                    for (int cycle = 0; cycle < cycles; cycle++)
                    {
                        schedules.Add(new BurstSchedule(
                            burst.time + cycle * burst.repeatInterval,
                            burst.maxCount));
                    }
                }

                return schedules.ToArray();
            }

            private static HashSet<ParticleSystem> CollectSubEmitters(ParticleSystem[] systems)
            {
                var driven = new HashSet<ParticleSystem>();
                for (int index = 0; index < systems.Length; index++)
                {
                    ParticleSystem.SubEmittersModule sub = systems[index].subEmitters;
                    if (!sub.enabled)
                    {
                        continue;
                    }

                    for (int slot = 0; slot < sub.subEmittersCount; slot++)
                    {
                        ParticleSystem child = sub.GetSubEmitterSystem(slot);
                        if (child != null)
                        {
                            driven.Add(child);
                        }
                    }
                }

                return driven;
            }
        }

        private readonly struct EffectEmitter
        {
            public EffectEmitter(
                ParticleSystem system,
                BurstSchedule[] bursts,
                float ratePerSecond,
                float durationSeconds)
            {
                System = system;
                Bursts = bursts;
                RatePerSecond = ratePerSecond;
                DurationSeconds = durationSeconds;
            }

            public ParticleSystem System { get; }
            public BurstSchedule[] Bursts { get; }
            public float RatePerSecond { get; }
            public float DurationSeconds { get; }
        }

        private readonly struct BurstSchedule
        {
            public BurstSchedule(float delaySeconds, int count)
            {
                DelaySeconds = delaySeconds;
                Count = count;
            }

            public float DelaySeconds { get; }
            public int Count { get; }
        }

        private struct PendingBurst
        {
            public PendingBurst(
                EffectEmitter emitter,
                Vector3 position,
                int count,
                float remainingSeconds)
            {
                Emitter = emitter;
                Position = position;
                Count = count;
                RemainingSeconds = remainingSeconds;
            }

            public EffectEmitter Emitter { get; }
            public Vector3 Position { get; }
            public int Count { get; }
            public float RemainingSeconds { get; set; }
        }

        private struct PendingRateEmission
        {
            public PendingRateEmission(
                EffectEmitter emitter,
                Vector3 position,
                float remainingSeconds)
            {
                Emitter = emitter;
                Position = position;
                RemainingSeconds = remainingSeconds;
                FractionalCount = 0f;
            }

            public EffectEmitter Emitter { get; }
            public Vector3 Position { get; }
            public float RemainingSeconds { get; set; }
            public float FractionalCount { get; set; }
        }
    }
}
