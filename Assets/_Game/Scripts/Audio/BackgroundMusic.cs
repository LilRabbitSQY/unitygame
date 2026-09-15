using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FinalDefense.Audio
{
    [DisallowMultipleComponent]
    public sealed class BackgroundMusic : MonoBehaviour
    {
        public const string MainMenuShopTrack = "MainMenuShop";
        public const string StoryTrack = "Story";
        public const string BattleTrack = "Battle";
        public const float DefaultVolume = .35f;
        public const float TransitionSeconds = .6f;
        private const string ResourceRoot = "Audio/Bgm/";

        public static BackgroundMusic Instance { get; private set; }
        public string CurrentTrack { get; private set; }
        public float Volume { get; private set; } = DefaultVolume;
        public bool IsTransitioning { get; private set; }
        public IReadOnlyList<AudioSource> MusicSources => sourceView;

        private static bool quitting;
        private readonly AudioSource[] sources = new AudioSource[2];
        private readonly float[] gains = new float[2];
        private readonly float[] fadeStarts = new float[2];
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private IReadOnlyList<AudioSource> sourceView;
        private int targetSource;
        private float fadeElapsed;
        private bool initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Domain Reload may be disabled. Remove retained delegates before subscribing again.
            UnsubscribeScenes();
            if (Instance != null)
            {
                Instance.StopMusic();
                Instance.Volume = DefaultVolume;
            }
            Instance = null;
            quitting = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnLoad()
        {
            var music = EnsureInstance();
            if (music != null) music.PlayForScene(SceneManager.GetActiveScene().name);
        }

        public static BackgroundMusic EnsureInstance()
        {
            if (!Application.isPlaying || quitting) return null;
            if (Instance == null)
            {
                // Reuse the retained object when Scene Reload is disabled too.
                foreach (var existing in FindObjectsByType<BackgroundMusic>(FindObjectsInactive.Include))
                {
                    Instance = existing;
                    break;
                }
                if (Instance == null)
                    return new GameObject("Background Music").AddComponent<BackgroundMusic>();
            }
            Instance.EnsureSources();
            if (!Instance.gameObject.activeSelf) Instance.gameObject.SetActive(true);
            if (!Instance.enabled) Instance.enabled = true;
            DontDestroyOnLoad(Instance.gameObject);
            SubscribeScenes();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                Destroy(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureSources();
        }

        private void OnEnable()
        {
            if (Instance != this) return;
            EnsureSources();
            SubscribeScenes();
            PlayForScene(SceneManager.GetActiveScene().name);
        }

        private void EnsureSources()
        {
            if (initialized && sources[0] != null && sources[1] != null) return;
            for (int i = 0; i < sources.Length; i++)
            {
                string sourceName = i == 0 ? "Music A" : "Music B";
                var child = transform.Find(sourceName);
                if (child == null)
                {
                    child = new GameObject(sourceName).transform;
                    child.SetParent(transform, false);
                }
                var source = child.GetComponent<AudioSource>();
                if (source == null) source = child.gameObject.AddComponent<AudioSource>();
                sources[i] = source;
                source.Stop();
                source.clip = null;
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0;
                source.dopplerLevel = 0;
                source.ignoreListenerPause = true;
                source.bypassReverbZones = true;
                source.volume = 0;
                gains[i] = 0;
            }
            sourceView = Array.AsReadOnly(sources);
            CurrentTrack = null;
            IsTransitioning = false;
            initialized = true;
        }

        private static void SubscribeScenes()
        {
            // -= / += makes repeated runtime initialization and EnsureInstance calls idempotent.
            UnsubscribeScenes();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private static void UnsubscribeScenes()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Instance != null && (mode == LoadSceneMode.Single || scene == SceneManager.GetActiveScene()))
                Instance.PlayForScene(scene.name);
        }

        private static void OnActiveSceneChanged(Scene previous, Scene next)
        {
            if (Instance != null) Instance.PlayForScene(next.name);
        }

        public static string TrackForScene(string sceneName)
        {
            switch (sceneName)
            {
                case "MainMenu": case "Shop": case "Result": return MainMenuShopTrack;
                case "PersonalityTest": case "Dialogue": return StoryTrack;
                case "Schedule": case "Battle": return BattleTrack;
                default: return null;
            }
        }

        private void PlayForScene(string sceneName)
        {
            var track = TrackForScene(sceneName);
            if (track != null) PlayTrack(track);
        }

        public static void PlayBattleResult()
        {
            var music = EnsureInstance();
            if (music != null) music.PlayTrack(MainMenuShopTrack);
        }

        public bool PlayTrack(string track)
        {
            if (track != MainMenuShopTrack && track != StoryTrack && track != BattleTrack) return false;
            if (Instance != this || !isActiveAndEnabled || quitting) return false;
            EnsureSources();
            // Do not seek or call Play on an already selected song, including during its fade.
            if (CurrentTrack == track) return true;
            if (!clips.TryGetValue(track, out var clip) || clip == null)
            {
                clip = Resources.Load<AudioClip>(ResourceRoot + track);
                if (clip == null)
                {
                    Debug.LogWarning("Background music resource not found: " + ResourceRoot + track, this);
                    return false;
                }
                clips[track] = clip;
            }

            int next = -1;
            for (int i = 0; i < sources.Length; i++)
                if (sources[i].clip == clip && sources[i].isPlaying) { next = i; break; }
            if (next < 0)
            {
                // A third request during a fade replaces the quieter channel, preserving the louder song.
                next = gains[0] <= gains[1] ? 0 : 1;
                sources[next].Stop();
                sources[next].clip = clip;
                gains[next] = 0;
                sources[next].volume = 0;
                sources[next].Play();
            }
            // Rapid A -> B -> A reverses from the current gains and reuses A's playback position.
            for (int i = 0; i < sources.Length; i++) fadeStarts[i] = gains[i];
            targetSource = next;
            CurrentTrack = track;
            fadeElapsed = 0;
            IsTransitioning = true;
            return true;
        }

        public void SetVolume(float volume)
        {
            Volume = float.IsNaN(volume) ? 0 : Mathf.Clamp01(volume);
            ApplyVolumes();
        }

        private void Update()
        {
            if (!IsTransitioning) return;
            fadeElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(fadeElapsed / TransitionSeconds);
            float smooth = progress * progress * (3 - 2 * progress);
            for (int i = 0; i < sources.Length; i++)
                gains[i] = Mathf.Lerp(fadeStarts[i], i == targetSource ? 1 : 0, smooth);
            ApplyVolumes();
            if (progress < 1) return;
            IsTransitioning = false;
            int previous = 1 - targetSource;
            sources[previous].Stop();
            sources[previous].clip = null;
        }

        private void ApplyVolumes()
        {
            for (int i = 0; i < sources.Length; i++)
                if (sources[i] != null) sources[i].volume = Volume * gains[i];
        }

        private void StopMusic()
        {
            IsTransitioning = false;
            CurrentTrack = null;
            for (int i = 0; i < sources.Length; i++)
            {
                gains[i] = 0;
                if (sources[i] == null) continue;
                sources[i].Stop();
                sources[i].clip = null;
                sources[i].volume = 0;
            }
        }

        private void OnDisable()
        {
            if (Instance != this) return;
            UnsubscribeScenes();
            StopMusic();
        }

        private void OnApplicationQuit() => quitting = true;

        private void OnDestroy()
        {
            if (Instance != this) return;
            UnsubscribeScenes();
            StopMusic();
            Instance = null;
            if (!quitting)
                foreach (var source in sources)
                    if (source != null && source.transform.parent == transform) Destroy(source.gameObject);
        }
    }
}
