using System.Collections.Generic;
using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// Lightweight runtime audio: procedural SFX so the game feels finished without asset packs.
    /// Replace clips later; keep calling the same API from gameplay.
    /// </summary>
    public class AudioService : MonoBehaviour
    {
        public static AudioService Instance { get; private set; }

        AudioSource sfxSource;
        AudioSource musicSource;
        readonly Dictionary<string, AudioClip> clipCache = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("--- Audio Service ---");
            DontDestroyOnLoad(go);
            go.AddComponent<AudioService>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            musicSource.volume = 0f;
            musicSource.clip = CreateLoop();

            ApplyVolumes();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void ApplyVolumes()
        {
            var master = SaveService.MasterVolume;
            var sfxOn = SaveService.SfxEnabled ? 1f : 0f;
            var musicOn = SaveService.MusicEnabled ? 1f : 0f;
            if (sfxSource != null)
                sfxSource.volume = master * SaveService.SfxVolume * sfxOn;
            if (musicSource != null)
            {
                musicSource.volume = master * SaveService.MusicVolume * 0.35f * musicOn;
                if (musicOn > 0.01f)
                {
                    if (musicSource.clip == null)
                        musicSource.clip = CreateLoop();
                    if (!musicSource.isPlaying)
                        musicSource.Play();
                }
                else if (musicSource.isPlaying)
                {
                    musicSource.Stop();
                }
            }
        }

        public void PlayJump() => PlayTone("jump", 520f, 0.06f, 0.22f, sine: true);
        public void PlayLand() => PlayTone("land", 180f, 0.05f, 0.28f, sine: false);
        public void PlayBreak() => PlayNoise("break", 0.12f, 0.35f);
        public void PlayFail() => PlayTone("fail", 140f, 0.28f, 0.4f, sine: true, slideTo: 70f);
        public void PlayRecover() => PlayTone("recover", 400f, 0.1f, 0.3f, sine: true, slideTo: 720f);
        public void PlayUi() => PlayTone("ui", 660f, 0.04f, 0.15f, sine: true);
        public void PlayScoreTick() => PlayTone("tick", 880f, 0.03f, 0.12f, sine: true);

        public void PlayGameOver()
        {
            PlayTone("over1", 220f, 0.12f, 0.35f, sine: true, slideTo: 160f);
        }

        /// <summary>Soft distant rumble — avoid harsh high-volume cracks with the visual pulse.</summary>
        public void PlayStormThunder()
        {
            PlayNoise("thunderSoft", 0.14f, 0.28f);
            PlayTone("thunderLow", 110f, 0.12f, 0.18f, sine: true, slideTo: 65f);
        }

        void PlayTone(string key, float hz, float duration, float volume, bool sine, float slideTo = -1f)
        {
            if (!SaveService.SfxEnabled) return;
            var clip = GetOrCreateTone(key, hz, duration, sine, slideTo);
            if (clip == null || sfxSource == null) return;
            // Source volume already = master * sfx (ApplyVolumes). OneShot scale is relative only.
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        void PlayNoise(string key, float duration, float volume)
        {
            if (!SaveService.SfxEnabled) return;
            var clip = GetOrCreateNoise(key, duration);
            if (clip == null || sfxSource == null) return;
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        AudioClip GetOrCreateTone(string key, float hz, float duration, bool sine, float slideTo)
        {
            if (clipCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            const int sampleRate = 22050;
            var sampleCount = Mathf.Max(64, Mathf.RoundToInt(sampleRate * duration));
            var data = new float[sampleCount];
            var endHz = slideTo > 0f ? slideTo : hz;

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleRate;
                var n = i / (float)sampleCount;
                var freq = Mathf.Lerp(hz, endHz, n);
                var env = Mathf.Sin(n * Mathf.PI); // attack+release
                env *= 1f - n * 0.35f;

                float sample;
                if (sine)
                    sample = Mathf.Sin(2f * Mathf.PI * freq * t);
                else
                    sample = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq * t)) * 0.7f;

                data[i] = sample * env * 0.55f;
            }

            var clip = AudioClip.Create($"sfx_{key}", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            clipCache[key] = clip;
            return clip;
        }

        AudioClip GetOrCreateNoise(string key, float duration)
        {
            if (clipCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            const int sampleRate = 22050;
            var sampleCount = Mathf.Max(64, Mathf.RoundToInt(sampleRate * duration));
            var data = new float[sampleCount];
            var rng = new System.Random(key.GetHashCode());

            for (var i = 0; i < sampleCount; i++)
            {
                var n = i / (float)sampleCount;
                var env = (1f - n) * (1f - n);
                var noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                data[i] = noise * env * 0.45f;
            }

            var clip = AudioClip.Create($"sfx_{key}", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            clipCache[key] = clip;
            return clip;
        }

        static AudioClip CreateLoop()
        {
            const int sampleRate = 22050;
            const float duration = 8f;
            var sampleCount = Mathf.RoundToInt(sampleRate * duration);
            var data = new float[sampleCount];
            float[] bass = { 130.81f, 164.81f, 196f, 174.61f };

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleRate;
                var bar = Mathf.FloorToInt(t / 2f) % bass.Length;
                var hz = bass[bar];
                var env = 0.55f + 0.45f * Mathf.Sin(t * Mathf.PI * 0.25f);
                var b = Mathf.Sin(2f * Mathf.PI * hz * t) * 0.16f;
                var pad = Mathf.Sin(2f * Mathf.PI * hz * 2f * t) * 0.06f
                          + Mathf.Sin(2f * Mathf.PI * hz * 3f * t) * 0.03f;
                var melEnv = Mathf.Max(0f, Mathf.Sin((t % 2f) / 2f * Mathf.PI));
                var mel = Mathf.Sin(2f * Mathf.PI * hz * 2f * t) * 0.07f * melEnv;
                data[i] = (b + pad + mel) * env;
            }

            var clip = AudioClip.Create("music_loop", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
