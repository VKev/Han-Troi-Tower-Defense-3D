using System.Collections;
using System.Collections.Generic;
using TowerDefense3D.Components.Core;
using UnityEngine;

namespace TowerDefense3D.Vfx
{
    /// <summary>
    /// Draws everything the game will later spawn - every effect, every enemy - once, off screen,
    /// at boot. The first time the player causes one, nothing is left to build.
    /// </summary>
    /// <remarks>
    /// The stall this exists to remove is the graphics driver building a pipeline state object the
    /// first time a shader is drawn with a particular render state and vertex layout. On Vulkan,
    /// which is this project's first choice on Android, that is the dominant cost, and it is only
    /// paid when a draw call is actually submitted. So this warms by drawing, not by asking the
    /// shader system to compile: a compiled shader with no pipeline state behind it still stalls.
    ///
    /// Three things follow from that, and all three are easy to get wrong in a way that leaves the
    /// warmup doing nothing at all while appearing to work:
    /// <list type="bullet">
    /// <item>The thing has to be inside the camera's frustum. Parked out of view it is culled, no
    /// draw call is submitted, and nothing is warmed.</item>
    /// <item>It has to survive a real frame. Spawned and destroyed within one frame, it never
    /// reaches a render.</item>
    /// <item>A particle system has to have live particles. An emitter with none submits no draw
    /// call.</item>
    /// </list>
    /// Effects and enemies are warmed separately because they are not the same work. An effect is
    /// particles, and playing it through the same <see cref="GlobalEffectEmitterView"/> the game
    /// plays it through is what guarantees the three rules above. An enemy is a skinned mesh under
    /// an animator, drawn through a different vertex path with its own shaders - warming the
    /// effects does not touch it, and nothing but drawing an enemy will.
    ///
    /// Nothing is visible. The camera renders into an off-screen texture rather than the screen,
    /// which is a stronger guarantee than hiding it behind whatever the menu happens to be
    /// drawing. The texture carries the level camera's format and anti-aliasing, because those are
    /// part of what a pipeline state is built against - warmed against a different format, the
    /// pipeline states would not be the ones gameplay asks for.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RuntimeWarmupView : MonoBehaviour
    {
        /// <summary>Every effect the game can play lives under here.</summary>
        private const string EffectResourceFolder = "Prefabs/VFX";

        /// <summary>Every enemy the game can spawn lives under here.</summary>
        private const string EnemyResourceFolder = "Prefabs/Enemies";

        /// <summary>
        /// The camera to warm against, authored to match the one the levels render with. Kept as
        /// an asset rather than built in code so the two can be compared - see the edit-mode test
        /// that fails when a level camera drifts away from it.
        /// </summary>
        private const string WarmupCameraResourcePath = "Prefabs/WarmupCamera";

        [Tooltip("How many prefabs to draw per frame. Spread out because the point is to move a stall off the player's screen, not to concentrate it into one long boot frame.")]
        [SerializeField, Min(1)] private int prefabsPerFrame = 3;

        [Tooltip("Frames each batch is held for. Two, so a batch is drawn at least once even if the first frame lands before its particles are simulated or its skinning has run.")]
        [SerializeField, Min(1)] private int framesPerBatch = 2;

        [Tooltip("Log what was warmed and what it cost. Worth leaving on: a warmup that silently warms nothing looks exactly like one that works.")]
        [SerializeField] private bool logSummary = true;

        [Tooltip("Start warming as soon as the game starts. Off only for a scene that wants to drive the warmup itself.")]
        [SerializeField] private bool warmUpOnStart = true;

        private readonly List<GameObject> effectPrefabs = new List<GameObject>();
        private readonly List<GameObject> enemyPrefabs = new List<GameObject>();
        private bool hasRun;

        /// <summary>The prefabs found on disk, kept referenced so they are not unloaded again.</summary>
        /// <remarks>
        /// Textures and meshes are uploaded to the GPU on first draw as well, and an unreferenced
        /// Resources asset is freed the next time a scene unloads - after which the upload has to
        /// happen again. Holding the lists keeps them resident for the run.
        /// </remarks>
        public IReadOnlyList<GameObject> EffectPrefabs => effectPrefabs;

        public IReadOnlyList<GameObject> EnemyPrefabs => enemyPrefabs;

        public bool HasRun => hasRun;

        /// <summary>
        /// Runs the warmup at boot, off the back of the title screen.
        /// </summary>
        /// <remarks>
        /// Boot rather than level entry because pipeline states, once built, last the whole run -
        /// warming here covers every level, where warming per level would repeat the same work ten
        /// times. And the title screen is a stretch of time the player spends looking at something
        /// static, which is the cheapest place in the game to spend it.
        /// </remarks>
        private void Start()
        {
            if (warmUpOnStart)
            {
                StartCoroutine(WarmUp());
            }
        }

        /// <summary>
        /// Draws every effect and every enemy once. Safe to call again; the second call returns
        /// immediately.
        /// </summary>
        public IEnumerator WarmUp()
        {
            if (hasRun)
            {
                yield break;
            }

            hasRun = true;
            LoadPrefabs(EffectResourceFolder, effectPrefabs, requireParticles: true);
            LoadPrefabs(EnemyResourceFolder, enemyPrefabs, requireParticles: false);
            if (effectPrefabs.Count == 0 && enemyPrefabs.Count == 0)
            {
                Debug.LogWarning(
                    "Runtime warmup found nothing under Resources/" + EffectResourceFolder
                    + " or Resources/" + EnemyResourceFolder
                    + ". Everything will build itself the first time it is spawned.");
                yield break;
            }

            var cameraPrefab = Resources.Load<GameObject>(WarmupCameraResourcePath);
            if (cameraPrefab == null)
            {
                Debug.LogWarning(
                    "Warmup camera missing at Resources/" + WarmupCameraResourcePath
                    + ". Skipping warmup rather than warming against a camera that does not match "
                    + "the levels, which would build pipeline states gameplay never asks for.");
                yield break;
            }

            float startedAt = Time.realtimeSinceStartup;
            GameObject cameraInstance = Instantiate(cameraPrefab);
            cameraInstance.name = "Runtime Warmup Camera";
            Camera camera = cameraInstance.GetComponentInChildren<Camera>(true);
            if (camera == null)
            {
                Debug.LogWarning("Warmup camera prefab has no Camera on it.");
                Destroy(cameraInstance);
                yield break;
            }

            RenderTexture target = CreateOffscreenTarget(camera);
            camera.targetTexture = target;
            AimAtOrigin(camera);

            int effectParticles = 0;
            yield return WarmEffects(count => effectParticles = count);

            int enemiesDrawn = 0;
            yield return WarmEnemies(count => enemiesDrawn = count);

            camera.targetTexture = null;
            RuntimeObjectDestroyer.Destroy(cameraInstance);
            target.Release();
            RuntimeObjectDestroyer.Destroy(target);

            if (logSummary)
            {
                // Particle count is the check that matters for effects: ones played but never
                // populated would report a warmup that drew nothing, which is the failure worth
                // catching.
                Debug.Log(
                    "Runtime warmup: " + effectPrefabs.Count + " effects ("
                    + effectParticles + " particles alive at the end), "
                    + enemiesDrawn + " enemies, in "
                    + ((Time.realtimeSinceStartup - startedAt) * 1000f).ToString("0") + "ms.");
                if (effectPrefabs.Count > 0 && effectParticles == 0)
                {
                    Debug.LogWarning(
                        "Runtime warmup finished with no live particles, so it is likely no effect "
                        + "was actually drawn and no pipeline states were built for them.");
                }
            }
        }

        /// <summary>
        /// Plays every effect through a rig of its own, at the origin the camera is pointed at.
        /// </summary>
        private IEnumerator WarmEffects(System.Action<int> reportParticles)
        {
            reportParticles(0);
            if (effectPrefabs.Count == 0)
            {
                yield break;
            }

            // The rig root has to sit at the world origin with an identity transform, the same as
            // the ones the game builds: effects authored in Local simulation space are placed
            // relative to it, and moving it moves their particles.
            var emitterRoot = new GameObject("Runtime Warmup Emitter");
            emitterRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var emitter = emitterRoot.AddComponent<GlobalEffectEmitterView>();

            int played = 0;
            for (int index = 0; index < effectPrefabs.Count; index++)
            {
                emitter.Play(effectPrefabs[index], Vector3.zero);
                played++;
                if (played % Mathf.Max(1, prefabsPerFrame) == 0)
                {
                    yield return HoldForFrames();
                }
            }

            // The tail batch still has to be drawn, and one extra frame beyond it so the last
            // effect played is not cleared on the frame it was emitted into.
            yield return HoldForFrames();
            yield return null;

            reportParticles(CountLiveParticles(emitterRoot));

            emitter.Clear();
            RuntimeObjectDestroyer.Destroy(emitterRoot);
        }

        /// <summary>
        /// Spawns every enemy in front of the camera, holds it for a frame or two, and clears up.
        /// </summary>
        /// <remarks>
        /// Their scripts are switched off the moment they exist. An enemy that has not been handed
        /// a definition and a path will throw from its own update loop, and a boot full of null
        /// references is a worse outcome than a cold shader. Awake has already run by then - the
        /// same as it would in the pool - so anything the renderers need from it is in place; only
        /// the per-frame work is stopped.
        ///
        /// They are spread out rather than stacked at one point, so an enemy is not depth-rejected
        /// behind another and skipped. Drawing is the entire purpose here.
        /// </remarks>
        private IEnumerator WarmEnemies(System.Action<int> reportDrawn)
        {
            reportDrawn(0);
            if (enemyPrefabs.Count == 0)
            {
                yield break;
            }

            var spawned = new List<GameObject>(enemyPrefabs.Count);
            int drawn = 0;
            for (int index = 0; index < enemyPrefabs.Count; index++)
            {
                GameObject instance = Instantiate(
                    enemyPrefabs[index],
                    SpreadPosition(index, enemyPrefabs.Count),
                    Quaternion.identity);
                instance.name = "Warmup " + enemyPrefabs[index].name;
                SilenceScripts(instance);
                spawned.Add(instance);
                drawn++;

                if (drawn % Mathf.Max(1, prefabsPerFrame) == 0)
                {
                    yield return HoldForFrames();
                }
            }

            yield return HoldForFrames();
            reportDrawn(drawn);

            for (int index = 0; index < spawned.Count; index++)
            {
                RuntimeObjectDestroyer.Destroy(spawned[index]);
            }
        }

        /// <summary>
        /// Stops every script on a warmed instance without touching its renderers or its animator,
        /// neither of which is a <see cref="MonoBehaviour"/>.
        /// </summary>
        private static void SilenceScripts(GameObject instance)
        {
            MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] != null)
                {
                    behaviours[index].enabled = false;
                }
            }
        }

        /// <summary>Points along a line across the camera's view, all of it inside the frustum.</summary>
        private static Vector3 SpreadPosition(int index, int count)
        {
            float span = Mathf.Max(1, count - 1);
            float offset = ((index / span) - 0.5f) * 6f;
            return new Vector3(offset, 0f, 0f);
        }

        private IEnumerator HoldForFrames()
        {
            for (int frame = 0; frame < Mathf.Max(1, framesPerBatch); frame++)
            {
                yield return new WaitForEndOfFrame();
            }
        }

        private static void LoadPrefabs(string folder, List<GameObject> into, bool requireParticles)
        {
            if (into.Count > 0)
            {
                return;
            }

            GameObject[] loaded = Resources.LoadAll<GameObject>(folder);
            for (int index = 0; index < loaded.Length; index++)
            {
                GameObject prefab = loaded[index];
                if (prefab == null)
                {
                    continue;
                }

                // A prefab under the effects folder with no particle systems is not an effect -
                // there is nothing for the emitter to play, so playing it would warm nothing.
                if (requireParticles
                    && prefab.GetComponentInChildren<ParticleSystem>(true) == null)
                {
                    continue;
                }

                into.Add(prefab);
            }
        }

        /// <summary>
        /// An off-screen target carrying the camera's own format and sample count.
        /// </summary>
        /// <remarks>
        /// Full screen size rather than a few pixels. Post-processing halves its buffers several
        /// times over, and a tiny target collapses those to nothing - the bloom passes would be
        /// skipped and their pipeline states never built, which is the opposite of the point.
        /// </remarks>
        private static RenderTexture CreateOffscreenTarget(Camera camera)
        {
            var descriptor = new RenderTextureDescriptor(
                Mathf.Max(64, Screen.width),
                Mathf.Max(64, Screen.height),
                camera.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default,
                24)
            {
                msaaSamples = camera.allowMSAA ? Mathf.Max(1, QualitySettings.antiAliasing) : 1,
                useMipMap = false,
                autoGenerateMips = false
            };

            var target = new RenderTexture(descriptor) { name = "Runtime Warmup Target" };
            target.Create();
            return target;
        }

        /// <summary>
        /// Points the camera at the origin from far enough back that a whole effect, and a row of
        /// enemies, fit in the frustum. Anything hanging outside it is culled, and a culled thing
        /// warms nothing.
        /// </summary>
        private static void AimAtOrigin(Camera camera)
        {
            camera.transform.position = new Vector3(0f, 6f, -12f);
            camera.transform.LookAt(Vector3.zero);
            camera.orthographic = false;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 60f);
        }

        private static int CountLiveParticles(GameObject emitterRoot)
        {
            int total = 0;
            ParticleSystem[] systems = emitterRoot.GetComponentsInChildren<ParticleSystem>(true);
            for (int index = 0; index < systems.Length; index++)
            {
                total += systems[index].particleCount;
            }

            return total;
        }
    }
}
