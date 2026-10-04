using UnityEngine;

namespace SmartSpace.Character
{
    public class EmoteEffects : MonoBehaviour
    {
        private AudioSource _audioSource;
        private ParticleSystem _particleSystem;

        private void Awake()
        {
            SetupAudio();
            SetupParticles();
        }

        private void SetupAudio()
        {
            _audioSource = gameObject.GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.spatialBlend = 0.85f; // 3D Spatial Audio
            _audioSource.minDistance = 2f;
            _audioSource.maxDistance = 25f;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.playOnAwake = false;
        }

        private void SetupParticles()
        {
            // Create a child object for emote particle bursts
            GameObject vfxObj = new GameObject("VFX_Emitter");
            vfxObj.transform.SetParent(transform, false);
            vfxObj.transform.localPosition = new Vector3(0, 1.8f, 0);

            _particleSystem = vfxObj.AddComponent<ParticleSystem>();

            var main = _particleSystem.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = 1.0f;
            main.startSpeed = 2.5f;
            main.startSize = 0.25f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _particleSystem.emission;
            emission.rateOverTime = 0;

            var shape = _particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f;

            var colorOverLifetime = _particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            var sizeOverLifetime = _particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.4f);
            curve.AddKey(0.3f, 1.2f);
            curve.AddKey(1f, 0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var renderer = vfxObj.GetComponent<ParticleSystemRenderer>();
            Material particleMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"));
            renderer.sharedMaterial = particleMat;
        }

        public void PlayEmoteFeedback(EmoteType type)
        {
            // 1. Play Dynamic Audio
            PlayProceduralSound(type);

            // 2. Play Particle Burst
            PlayParticleBurst(type);
        }

        private void PlayParticleBurst(EmoteType type)
        {
            if (_particleSystem == null) return;

            Color burstColor = type switch
            {
                EmoteType.Heart => new Color(1f, 0.25f, 0.6f),     // Bright Pink
                EmoteType.Wave => new Color(0.2f, 0.85f, 1f),      // Cyan Sparkle
                EmoteType.Clap => new Color(1f, 0.84f, 0f),       // Gold Sparkle
                EmoteType.Bow => new Color(0.4f, 0.7f, 1f),        // Calm Blue
                EmoteType.Dance => new Color(0.9f, 0.4f, 1f),      // Purple Disco
                EmoteType.HighFive => new Color(1f, 0.5f, 0f),     // Vibrant Orange
                _ => Color.white
            };

            var main = _particleSystem.main;
            main.startColor = burstColor;

            _particleSystem.Stop();
            _particleSystem.Clear();
            _particleSystem.Emit(type == EmoteType.Heart ? 16 : 24);
        }

        private void PlayProceduralSound(EmoteType type)
        {
            if (_audioSource == null) return;

            AudioClip clip = GenerateEmoteClip(type);
            if (clip != null)
            {
                _audioSource.pitch = Random.Range(0.97f, 1.03f);
                _audioSource.PlayOneShot(clip, 0.65f);
            }
        }

        // Dynamically synthesize a pleasant melodic chime without external audio files
        private static AudioClip GenerateEmoteClip(EmoteType type)
        {
            int sampleRate = 44100;
            float duration = 0.35f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            float f1 = 523.25f; // C5
            float f2 = 659.25f; // E5
            float f3 = 783.99f; // G5
            float f4 = 1046.50f; // C6

            if (type == EmoteType.Heart)
            {
                // Sweet rising arpeggio C5 -> E5 -> G5
                for (int i = 0; i < totalSamples; i++)
                {
                    float t = (float)i / sampleRate;
                    float freq = t < 0.1f ? f1 : (t < 0.2f ? f2 : f3);
                    float env = Mathf.Exp(-t * 8f);
                    samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.4f;
                }
            }
            else if (type == EmoteType.Wave || type == EmoteType.HighFive)
            {
                // Playful two-tone chime G5 -> C6
                for (int i = 0; i < totalSamples; i++)
                {
                    float t = (float)i / sampleRate;
                    float freq = t < 0.15f ? f3 : f4;
                    float env = Mathf.Exp(-t * 9f);
                    samples[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * freq * t)) * env * 0.35f;
                }
            }
            else if (type == EmoteType.Clap)
            {
                // Crisp pop/clap double transient
                for (int i = 0; i < totalSamples; i++)
                {
                    float t = (float)i / sampleRate;
                    float env1 = Mathf.Exp(-t * 40f);
                    float t2 = Mathf.Max(0, t - 0.08f);
                    float env2 = Mathf.Exp(-t2 * 35f);
                    float noise = (Random.value * 2f - 1f);
                    samples[i] = (noise * (env1 + env2 * 0.8f)) * 0.3f;
                }
            }
            else if (type == EmoteType.Dance)
            {
                // Funk bounce tone with rapid vibrato
                for (int i = 0; i < totalSamples; i++)
                {
                    float t = (float)i / sampleRate;
                    float vibrato = Mathf.Sin(2f * Mathf.PI * 18f * t) * 40f;
                    float freq = 440f + vibrato;
                    float env = Mathf.Exp(-t * 7f);
                    samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.4f;
                }
            }
            else // Bow
            {
                // Gentle harmonic bell
                for (int i = 0; i < totalSamples; i++)
                {
                    float t = (float)i / sampleRate;
                    float env = Mathf.Exp(-t * 6f);
                    samples[i] = (Mathf.Sin(2f * Mathf.PI * 392f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 784f * t)) * env * 0.35f;
                }
            }

            AudioClip clip = AudioClip.Create("Emote_" + type, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
