using UnityEngine;

namespace TowerDefense3D.Towers
{
    /// <summary>
    /// One straight run of line: a link between two towers, or the line under a link being
    /// dragged. Circles are not drawn here - see <see cref="TowerSelectionRingView"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class TowerLinkLineView : MonoBehaviour
    {
        private const float EffectReferenceLength = 4f;
        private const float EffectEndpointPadding = 1.06f;
        private const float EffectEmissionMultiplier = 1.5f;
        private const float EffectDarkBrightness = 0.8f;
        private const float EffectLightBrightness = 1f;

        private LineRenderer lineRenderer;
        private GameObject linkEffect;
        private ParticleSystem[] linkParticles;
        private float[] baseStartSpeeds;
        private float[] baseEmissionRates;
        private Color effectColor;
        private bool hasEffectColor;
        private bool effectNeedsWarmup = true;

        public void Initialize(Material material, GameObject effectPrefab, float width, int positionCount)
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.sharedMaterial = material;
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = false;
            lineRenderer.positionCount = positionCount;
            lineRenderer.widthMultiplier = width;
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;

            if (effectPrefab != null)
            {
                linkEffect = Instantiate(effectPrefab, transform);
                linkEffect.name = "Directional Flow";
                linkParticles = linkEffect.GetComponentsInChildren<ParticleSystem>(true);
                baseStartSpeeds = new float[linkParticles.Length];
                baseEmissionRates = new float[linkParticles.Length];
                for (int index = 0; index < linkParticles.Length; index++)
                {
                    ParticleSystem.MainModule main = linkParticles[index].main;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    ParticleSystem.TrailModule trails = linkParticles[index].trails;
                    trails.worldSpace = false;
                    baseStartSpeeds[index] = linkParticles[index].main.startSpeedMultiplier;
                    baseEmissionRates[index] = linkParticles[index].emission.rateOverTimeMultiplier;
                }

                lineRenderer.enabled = false;
            }

            gameObject.SetActive(false);
        }

        public void ShowLine(Vector3 start, Vector3 end, Color color)
        {
            gameObject.SetActive(true);
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
            SetColor(color);
            SetEffect(start, end, color);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            effectNeedsWarmup = true;
        }

        private void SetColor(Color color)
        {
            lineRenderer.startColor = color;
            lineRenderer.endColor = Brighten(color);
        }

        private void SetEffect(Vector3 start, Vector3 end, Color color)
        {
            if (linkEffect == null)
            {
                return;
            }

            Vector3 direction = end - start;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                linkEffect.SetActive(false);
                return;
            }

            linkEffect.SetActive(true);
            linkEffect.transform.SetPositionAndRotation(start, Quaternion.LookRotation(direction));
            linkEffect.transform.localScale = new Vector3(1f, 1f, direction.magnitude / EffectReferenceLength);

            bool colorChanged = !hasEffectColor || effectColor != color;
            Color brightColor = Brighten(color);
            Color effectTint = new Color(
                color.r * EffectDarkBrightness,
                color.g * EffectDarkBrightness,
                color.b * EffectDarkBrightness,
                color.a);
            Color brightTint = new Color(
                brightColor.r * EffectLightBrightness,
                brightColor.g * EffectLightBrightness,
                brightColor.b * EffectLightBrightness,
                brightColor.a);
            for (int index = 0; index < linkParticles.Length; index++)
            {
                ParticleSystem.MainModule main = linkParticles[index].main;
                main.startSpeedMultiplier = baseStartSpeeds[index] * EffectEndpointPadding;
                main.startColor = new ParticleSystem.MinMaxGradient(effectTint, brightTint);

                ParticleSystem.EmissionModule emission = linkParticles[index].emission;
                emission.rateOverTimeMultiplier = baseEmissionRates[index] * EffectEmissionMultiplier;
            }

            if (colorChanged)
            {
                for (int systemIndex = 0; systemIndex < linkParticles.Length; systemIndex++)
                {
                    ParticleSystem system = linkParticles[systemIndex];
                    var particles = new ParticleSystem.Particle[system.particleCount];
                    int particleCount = system.GetParticles(particles);
                    for (int particleIndex = 0; particleIndex < particleCount; particleIndex++)
                    {
                        float blend = (particles[particleIndex].randomSeed & 255) / 255f;
                        particles[particleIndex].startColor = Color.Lerp(effectTint, brightTint, blend);
                    }

                    system.SetParticles(particles, particleCount);
                }

                effectColor = color;
                hasEffectColor = true;
            }

            if (effectNeedsWarmup)
            {
                ParticleSystem rootParticles = linkEffect.GetComponent<ParticleSystem>();
                rootParticles.Simulate(0.5f, true, true);
                rootParticles.Play(true);
                effectNeedsWarmup = false;
            }
        }

        private static Color Brighten(Color color)
        {
            Color pale = Color.Lerp(color, Color.white, 0.25f);
            pale.a = color.a;
            return pale;
        }
    }
}
