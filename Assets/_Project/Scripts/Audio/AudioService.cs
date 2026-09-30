using UnityEngine;
using UnityEngine.Audio;

namespace CatMerge.Audio
{
    /// <summary>
    /// Background music with a fade-in plus a small pool of one-shot SFX sources. Meows are rate-limited
    /// (max simultaneous, cooldown) and get a random pitch so a room full of cats sounds alive, not spammy.
    /// Missing clips are fine: playback just stays silent.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        /// <summary>Exposed parameter names on the mixer (see Tools/CatMerge/Create Audio Mixer).</summary>
        public const string MusicVolumeParam = "MusicVolume";
        public const string SfxVolumeParam = "SfxVolume";

        [Tooltip("Optional. Without it (and on WebGL, where Unity ignores mixers) volume is applied to the sources directly.")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;

        [Header("Music")]
        [SerializeField] private AudioClip musicClip;
        [SerializeField, Min(0f)] private float musicFadeIn = 4f;

        [Header("SFX")]
        [SerializeField, Range(1, 16)] private int poolSize = 6;
        [SerializeField, Min(1)] private int maxSimultaneousMeows = 3;
        [Tooltip("Seconds after one meow during which ordinary meows are skipped.")]
        [SerializeField, Min(0f)] private float meowCooldown = 0.15f;
        [Tooltip("Random pitch offset of each meow, 0.06 = ±6%.")]
        [SerializeField, Range(0f, 0.3f)] private float meowPitchVariance = 0.06f;

#if UNITY_WEBGL && !UNITY_EDITOR
        private static readonly bool MixerSupported = false;
#else
        private static readonly bool MixerSupported = true;
#endif

        // Browsers block audio until the first user gesture. Static so a scene reload (progress reset) doesn't ask again.
        private static bool s_unlocked;

        private AudioSource _music;
        private AudioSource[] _pool;
        private bool[] _isMeow;
        private float[] _baseVolume;
        private float[] _startedAt;
        private float _musicFade;
        private float _lastMeowTime = float.NegativeInfinity;
        private bool _useMixer;

        /// <summary>True after the "tap to start" screen was dismissed; nothing plays before that.</summary>
        public static bool IsUnlocked => s_unlocked;

        public float MusicVolume { get; private set; }
        public float SfxVolume { get; private set; }

        private void Awake()
        {
            _useMixer = MixerSupported && mixer != null;

            _music = CreateSource("Music", musicGroup);
            _music.loop = true;

            _pool = new AudioSource[poolSize];
            _isMeow = new bool[poolSize];
            _baseVolume = new float[poolSize];
            _startedAt = new float[poolSize];
            for (int i = 0; i < poolSize; i++) _pool[i] = CreateSource($"Sfx {i}", sfxGroup);

            MusicVolume = VolumeSettings.Music;
            SfxVolume = VolumeSettings.Sfx;
        }

        private void Start()
        {
            // Mixer parameters can't be set reliably in Awake (the mixer may not be active yet).
            ApplyMusicVolume();
            ApplySfxVolume();
            if (s_unlocked) StartMusic();
        }

        private void Update()
        {
            if (_musicFade >= 1f || !_music.isPlaying) return;
            _musicFade = musicFadeIn > 0f ? Mathf.Min(1f, _musicFade + Time.unscaledDeltaTime / musicFadeIn) : 1f;
            ApplyMusicVolume();
        }

        /// <summary>Called on the first user gesture: allows sound and starts the music.</summary>
        public void Unlock()
        {
            s_unlocked = true;
            StartMusic();
        }

        public void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            VolumeSettings.Music = MusicVolume;
            ApplyMusicVolume();
        }

        public void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            VolumeSettings.Sfx = SfxVolume;
            ApplySfxVolume();
        }

        /// <summary>
        /// Plays a meow with a random pitch unless too many are playing or one just played. <paramref name="important"/>
        /// meows (merges) ignore the cooldown but still respect the simultaneous limit. A null clip counts as a
        /// silent meow, so callers can animate the cat before real sounds exist.
        /// </summary>
        /// <returns>True if the meow "happened" (played, or silent because the clip is missing).</returns>
        public bool PlayMeow(AudioClip clip, bool important = false)
        {
            if (!s_unlocked) return false;

            float now = Time.unscaledTime;
            if (!important && now - _lastMeowTime < meowCooldown) return false;
            if (CountPlayingMeows() >= maxSimultaneousMeows) return false;

            _lastMeowTime = now;
            if (clip == null) return true;

            float pitch = 1f + Random.Range(-meowPitchVariance, meowPitchVariance);
            Play(clip, 1f, pitch, isMeow: true);
            return true;
        }

        /// <summary>One-shot UI/game sound without meow limits.</summary>
        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (!s_unlocked || clip == null) return;
            Play(clip, volume, 1f, isMeow: false);
        }

        private void StartMusic()
        {
            if (musicClip == null || _music.isPlaying) return;
            _music.clip = musicClip;
            _musicFade = 0f;
            ApplyMusicVolume();
            _music.Play();
        }

        private void Play(AudioClip clip, float volume, float pitch, bool isMeow)
        {
            int index = FindSource();
            var source = _pool[index];
            source.Stop();
            source.clip = clip;
            source.pitch = pitch;
            _isMeow[index] = isMeow;
            _baseVolume[index] = volume;
            _startedAt[index] = Time.unscaledTime;
            source.volume = volume * SfxMultiplier;
            source.Play();
        }

        /// <summary>A free source, or the one that has been playing longest.</summary>
        private int FindSource()
        {
            int oldest = 0;
            for (int i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].isPlaying) return i;
                if (_startedAt[i] < _startedAt[oldest]) oldest = i;
            }
            return oldest;
        }

        private int CountPlayingMeows()
        {
            int count = 0;
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_isMeow[i] && _pool[i].isPlaying) count++;
            }
            return count;
        }

        private float SfxMultiplier => _useMixer ? 1f : SfxVolume;

        private void ApplyMusicVolume()
        {
            if (_useMixer) mixer.SetFloat(MusicVolumeParam, VolumeSettings.ToDecibels(MusicVolume));
            // Smoothstep so the start of the fade is barely audible and it eases in.
            float fade = Mathf.SmoothStep(0f, 1f, _musicFade);
            _music.volume = fade * (_useMixer ? 1f : MusicVolume);
        }

        private void ApplySfxVolume()
        {
            if (_useMixer) mixer.SetFloat(SfxVolumeParam, VolumeSettings.ToDecibels(SfxVolume));
            for (int i = 0; i < _pool.Length; i++) _pool[i].volume = _baseVolume[i] * SfxMultiplier;
        }

        private AudioSource CreateSource(string sourceName, AudioMixerGroup group)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = group;
            return source;
        }
    }
}
