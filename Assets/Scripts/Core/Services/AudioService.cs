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
            if (sfxSource != null)
                sfxSource.volume = master * SaveService.SfxVolume;
            if (musicSource != null)
                musicSource.volume = master * SaveService.MusicVolume * 0.35f;
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

        void PlayTone(string key, float hz, float duration, float volume, bool sine, float slideTo = -1f)
        {
            var clip = GetOrCreateTone(key, hz, duration, sine, slideTo);
            if (clip == null || sfxSource == null) return;
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume) * SaveService.MasterVolume * SaveService.SfxVolume);
        }

        void PlayNoise(string key, float duration, float volume)
        {
            var clip = GetOrCreateNoise(key, duration);
            if (clip == null || sfxSource == null) return;
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume) * SaveService.MasterVolume * SaveService.SfxVolume);
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
    }
}
