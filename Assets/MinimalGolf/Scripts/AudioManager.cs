using UnityEngine;

namespace MinimalGolf
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(AudioListener))]
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Background Music")]
        [SerializeField] private AudioClip[] musicTracks;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.32f;

        [Header("Sound Effects")]
        [SerializeField] private AudioClip[] shotClips;
        [SerializeField] private AudioClip[] holeClips;
        [SerializeField] private AudioClip collisionClip;
        [SerializeField] private AudioClip rotationClip;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.70f;

        private AudioSource musicSource;
        private AudioSource sfxSource;
        private int previousTrackIndex = -1;
        private int previousShotIndex = -1;
        private int previousHoleIndex = -1;
        private double nextStartAttemptTime;

        public AudioClip CurrentTrack => musicSource != null ? musicSource.clip : null;
        public AudioClip LastPlayedSfx { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureAudioSources();
            ApplySourceSettings();
        }

        private void Start()
        {
            PlayRandomTrack();
        }

        private void Update()
        {
            if (musicSource == null || musicSource.isPlaying || AudioSettings.dspTime < nextStartAttemptTime)
                return;

            PlayRandomTrack();
        }

        public void Configure(AudioClip[] tracks, float volume)
        {
            musicTracks = tracks;
            musicVolume = Mathf.Clamp01(volume);

            EnsureAudioSources();
            ApplySourceSettings();
        }

        public void ConfigureSfx(AudioClip[] shots, AudioClip[] holes, AudioClip collision, float volume)
        {
            shotClips = shots;
            holeClips = holes;
            collisionClip = collision;
            sfxVolume = Mathf.Clamp01(volume);

            EnsureAudioSources();
            ApplySourceSettings();
        }

        public void ConfigureRotationSfx(AudioClip rotation)
        {
            rotationClip = rotation;
        }

        public void PlayShotSfx()
        {
            PlayRandomOneShot(shotClips, ref previousShotIndex);
        }

        public void PlayHoleSfx()
        {
            PlayRandomOneShot(holeClips, ref previousHoleIndex);
        }

        public void PlayCollisionSfx()
        {
            PlayOneShot(collisionClip);
        }

        public void PlayRotationSfx()
        {
            PlayOneShot(rotationClip);
        }

        private void PlayRandomTrack()
        {
            if (musicSource == null || musicTracks == null || musicTracks.Length == 0)
            {
                nextStartAttemptTime = AudioSettings.dspTime + 1d;
                return;
            }

            int nextTrackIndex = ChooseTrackIndex();
            AudioClip nextTrack = musicTracks[nextTrackIndex];
            if (nextTrack == null)
            {
                previousTrackIndex = nextTrackIndex;
                nextStartAttemptTime = AudioSettings.dspTime + 0.25d;
                return;
            }

            previousTrackIndex = nextTrackIndex;
            musicSource.clip = nextTrack;
            musicSource.Play();
            nextStartAttemptTime = AudioSettings.dspTime + 0.25d;
        }

        private int ChooseTrackIndex()
        {
            if (musicTracks.Length == 1)
                return 0;

            if (previousTrackIndex < 0)
                return Random.Range(0, musicTracks.Length);

            int index = Random.Range(0, musicTracks.Length - 1);
            if (index >= previousTrackIndex)
                index++;

            return index;
        }

        private void PlayRandomOneShot(AudioClip[] clips, ref int previousIndex)
        {
            if (clips == null || clips.Length == 0)
                return;

            int index;
            if (clips.Length == 1)
            {
                index = 0;
            }
            else if (previousIndex < 0)
            {
                index = Random.Range(0, clips.Length);
            }
            else
            {
                index = Random.Range(0, clips.Length - 1);
                if (index >= previousIndex)
                    index++;
            }

            previousIndex = index;
            PlayOneShot(clips[index]);
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip == null)
                return;

            EnsureAudioSources();
            LastPlayedSfx = clip;
            sfxSource.PlayOneShot(clip);
        }

        private void EnsureAudioSources()
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length == 0)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                sfxSource = gameObject.AddComponent<AudioSource>();
                return;
            }

            musicSource = sources[0];
            sfxSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();
        }

        private void ApplySourceSettings()
        {
            if (musicSource == null || sfxSource == null)
                return;

            musicSource.playOnAwake = false;
            musicSource.loop = false;
            musicSource.spatialBlend = 0f;
            musicSource.volume = musicVolume;
            musicSource.priority = 64;

            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
            sfxSource.volume = sfxVolume;
            sfxSource.priority = 32;
        }

        private void OnValidate()
        {
            musicVolume = Mathf.Clamp01(musicVolume);
            sfxVolume = Mathf.Clamp01(sfxVolume);
            EnsureAudioSources();
            ApplySourceSettings();
        }
    }
}
