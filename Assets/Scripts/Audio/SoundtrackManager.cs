using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Core;

namespace OilLeak.Audio
{
    /// <summary>
    /// Manages soundtrack playback with crossfading between states
    /// Implements IAudioService for integration with GameCore
    /// </summary>
    public class SoundtrackManager : MonoBehaviour, IAudioService, IResettable
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource audioSourceA;
        [SerializeField] private AudioSource audioSourceB;

        [Header("Soundtrack States")]
        [SerializeField] private SoundtrackState calmState;
        [SerializeField] private SoundtrackState tenseState;
        [SerializeField] private SoundtrackState criticalState;
        [SerializeField] private SoundtrackState gameOverState;
        [SerializeField] private SoundtrackState menuState; // Optional

        [Header("Volume Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float masterVolume = 1f;
        [Range(0f, 1f)]
        [SerializeField] private float musicVolume = 0.8f;
        [Range(0f, 1f)]
        [SerializeField] private float sfxVolume = 1f;

        [Header("Hysteresis Settings")]
        [Tooltip("Exit Tense state only when integrity >= this value")]
        [SerializeField] private float tensExitThreshold = 0.55f;
        [Tooltip("Exit Critical state only when integrity >= this value")]
        [SerializeField] private float criticalExitThreshold = 0.25f;

        [Header("Debug")]
        [SerializeField] private bool debugLogging = false;

        // Current state
        private SoundtrackState currentState;
        private AudioSource activeSource;
        private AudioSource nextSource;
        private bool isSourceA = true;

        // Playback state
        private Coroutine crossfadeCoroutine;
        private Coroutine playlistCoroutine;
        private AudioClip[] currentPlaylist;
        private int currentTrackIndex;
        private bool isPaused = false;

        // State tracking for hysteresis
        private MusicType lastMusicType = MusicType.Menu;
        private bool warningsLogged = false;

        void Awake()
        {
            // Setup audio sources if not assigned
            if (audioSourceA == null || audioSourceB == null)
            {
                SetupAudioSources();
            }

            // Configure audio sources
            ConfigureAudioSource(audioSourceA);
            ConfigureAudioSource(audioSourceB);

            activeSource = audioSourceA;
            nextSource = audioSourceB;

            // Register with ResetRegistry
            ResetRegistry.Register(this);

            // Log initial warnings for missing states
            ValidateStates();

            if (debugLogging)
                Debug.Log("[SoundtrackManager] Initialized");
        }

        void OnDestroy()
        {
            // Unregister from ResetRegistry
            ResetRegistry.Unregister(this);
        }

        private void SetupAudioSources()
        {
            if (audioSourceA == null)
            {
                GameObject sourceAObject = new GameObject("AudioSource_A");
                sourceAObject.transform.SetParent(transform);
                audioSourceA = sourceAObject.AddComponent<AudioSource>();
            }

            if (audioSourceB == null)
            {
                GameObject sourceBObject = new GameObject("AudioSource_B");
                sourceBObject.transform.SetParent(transform);
                audioSourceB = sourceBObject.AddComponent<AudioSource>();
            }
        }

        private void ConfigureAudioSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false; // We handle looping ourselves
            source.spatialBlend = 0f; // 2D sound for music
            source.volume = 0f; // Start silent
        }

        private void ValidateStates()
        {
            if (!warningsLogged)
            {
                if (calmState == null || !calmState.IsValid())
                    Debug.LogWarning("[SoundtrackManager] Calm state missing or has no clips");
                if (tenseState == null || !tenseState.IsValid())
                    Debug.LogWarning("[SoundtrackManager] Tense state missing or has no clips");
                if (criticalState == null || !criticalState.IsValid())
                    Debug.LogWarning("[SoundtrackManager] Critical state missing or has no clips");
                if (gameOverState == null || !gameOverState.IsValid())
                    Debug.LogWarning("[SoundtrackManager] GameOver state missing or has no clips");

                warningsLogged = true;
            }
        }

        #region IAudioService Implementation

        public void PlayMusic(MusicType musicType)
        {
            lastMusicType = musicType;

            // Map MusicType to SoundtrackState
            SoundtrackState targetState = null;
            switch (musicType)
            {
                case MusicType.Menu:
                    targetState = menuState ?? calmState; // Fallback to calm if no menu state
                    break;
                case MusicType.Gameplay:
                    targetState = calmState;
                    break;
                case MusicType.Danger:
                    targetState = criticalState;
                    break;
                case MusicType.Failure:
                    targetState = gameOverState;
                    break;
            }

            if (targetState != null && targetState.IsValid())
            {
                TransitionToState(targetState);
            }
            else if (debugLogging)
            {
                Debug.LogWarning($"[SoundtrackManager] Cannot play music type {musicType} - state missing or invalid");
            }
        }

        public void PlaySound(AudioClip clip, Vector3 position, float volume = 1f)
        {
            // Not implemented - handled by other systems
            // This manager focuses on music only
        }

        public void PlayUISound(AudioClip clip, float volume = 1f)
        {
            // Not implemented - handled by other systems
            // This manager focuses on music only
        }

        public void StopAll()
        {
            if (crossfadeCoroutine != null)
            {
                StopCoroutine(crossfadeCoroutine);
                crossfadeCoroutine = null;
            }

            if (playlistCoroutine != null)
            {
                StopCoroutine(playlistCoroutine);
                playlistCoroutine = null;
            }

            audioSourceA.Stop();
            audioSourceB.Stop();
            audioSourceA.volume = 0f;
            audioSourceB.volume = 0f;

            currentState = null;
            isPaused = false;

            if (debugLogging)
                Debug.Log("[SoundtrackManager] Stopped all music");
        }

        public void PauseAll()
        {
            if (!isPaused)
            {
                audioSourceA.Pause();
                audioSourceB.Pause();
                isPaused = true;

                if (debugLogging)
                    Debug.Log("[SoundtrackManager] Paused music");
            }
        }

        public void ResumeAll()
        {
            if (isPaused)
            {
                audioSourceA.UnPause();
                audioSourceB.UnPause();
                isPaused = false;

                if (debugLogging)
                    Debug.Log("[SoundtrackManager] Resumed music");
            }
        }

        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            UpdateVolumes();
        }

        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            // SFX handled elsewhere, stored here for consistency
        }

        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            UpdateVolumes();
        }

        #endregion

        #region IResettable Implementation

        public void Reset()
        {
            StopAll();
            currentState = null;
            currentPlaylist = null;
            currentTrackIndex = 0;
            lastMusicType = MusicType.Menu;

            if (debugLogging)
                Debug.Log("[SoundtrackManager] Reset complete");
        }

        public bool IsClean => !audioSourceA.isPlaying && !audioSourceB.isPlaying &&
                               crossfadeCoroutine == null && playlistCoroutine == null;

        #endregion

        #region State Transitions

        /// <summary>
        /// Transition to a new soundtrack state with crossfade
        /// </summary>
        public void TransitionToState(SoundtrackState newState)
        {
            if (newState == null || !newState.IsValid())
            {
                if (debugLogging)
                    Debug.LogWarning($"[SoundtrackManager] Cannot transition to invalid state");
                return;
            }

            if (newState == currentState)
            {
                if (debugLogging)
                    Debug.Log($"[SoundtrackManager] Already in state: {newState.stateName}");
                return;
            }

            // Stop any existing transitions
            if (crossfadeCoroutine != null)
            {
                StopCoroutine(crossfadeCoroutine);
            }

            if (playlistCoroutine != null)
            {
                StopCoroutine(playlistCoroutine);
            }

            currentState = newState;
            currentPlaylist = newState.GetPlaylist();
            currentTrackIndex = 0;

            // Start crossfade
            crossfadeCoroutine = StartCoroutine(CrossfadeToState(newState));

            if (debugLogging)
                Debug.Log($"[SoundtrackManager] Transitioning to: {newState.stateName}");
        }

        /// <summary>
        /// Transition by state name
        /// </summary>
        public void TransitionToState(string stateName)
        {
            SoundtrackState state = GetStateByName(stateName);
            if (state != null)
            {
                TransitionToState(state);
            }
        }

        /// <summary>
        /// Update music based on integrity with hysteresis
        /// </summary>
        public void UpdateMusicForIntegrity(float integrity)
        {
            // Determine target state based on integrity and hysteresis
            SoundtrackState targetState = null;

            if (currentState == criticalState)
            {
                // In critical, need to exceed exit threshold to leave
                if (integrity >= criticalExitThreshold)
                {
                    targetState = integrity >= tensExitThreshold ? calmState : tenseState;
                }
                else
                {
                    targetState = criticalState; // Stay in critical
                }
            }
            else if (currentState == tenseState)
            {
                // In tense, check both thresholds
                if (integrity >= tensExitThreshold)
                {
                    targetState = calmState;
                }
                else if (integrity <= 0.2f) // Enter critical at 20%
                {
                    targetState = criticalState;
                }
                else
                {
                    targetState = tenseState; // Stay in tense
                }
            }
            else // In calm or other state
            {
                if (integrity <= 0.2f)
                {
                    targetState = criticalState;
                }
                else if (integrity <= 0.5f)
                {
                    targetState = tenseState;
                }
                else
                {
                    targetState = calmState;
                }
            }

            if (targetState != null && targetState != currentState && targetState.IsValid())
            {
                TransitionToState(targetState);
            }
        }

        #endregion

        #region Crossfade Implementation

        private IEnumerator CrossfadeToState(SoundtrackState newState)
        {
            if (currentPlaylist == null || currentPlaylist.Length == 0)
            {
                yield break;
            }

            // Get next track
            AudioClip nextClip = currentPlaylist[currentTrackIndex];
            float crossfadeDuration = newState.crossfadeSeconds;
            float targetVolume = newState.volume * musicVolume * masterVolume;

            // Setup next source
            nextSource.clip = nextClip;
            nextSource.volume = 0f;
            nextSource.Play();

            // Crossfade
            float elapsed = 0f;
            while (elapsed < crossfadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / crossfadeDuration;

                // Fade out active, fade in next
                activeSource.volume = Mathf.Lerp(activeSource.volume, 0f, t);
                nextSource.volume = Mathf.Lerp(0f, targetVolume, t);

                yield return null;
            }

            // Complete transition
            activeSource.Stop();
            activeSource.volume = 0f;

            // Swap sources
            AudioSource temp = activeSource;
            activeSource = nextSource;
            nextSource = temp;
            isSourceA = !isSourceA;

            // Start playlist management
            playlistCoroutine = StartCoroutine(ManagePlaylist());
        }

        private IEnumerator ManagePlaylist()
        {
            while (currentState != null && currentState.IsValid())
            {
                // Wait for current track to finish
                while (activeSource.isPlaying)
                {
                    yield return new WaitForSeconds(0.5f);
                }

                // Apply gap if configured
                if (currentState.minGap > 0f)
                {
                    yield return new WaitForSeconds(currentState.minGap);
                }

                // Move to next track
                currentTrackIndex++;

                // Loop playlist if needed
                if (currentTrackIndex >= currentPlaylist.Length)
                {
                    if (currentState.loopPlaylist)
                    {
                        currentTrackIndex = 0;
                        if (currentState.shuffle)
                        {
                            currentPlaylist = currentState.GetShuffledPlaylist();
                        }
                    }
                    else
                    {
                        // Playlist ended, stop
                        yield break;
                    }
                }

                // Play next track
                if (currentTrackIndex < currentPlaylist.Length)
                {
                    AudioClip nextClip = currentPlaylist[currentTrackIndex];
                    float targetVolume = currentState.volume * musicVolume * masterVolume;

                    activeSource.clip = nextClip;
                    activeSource.volume = targetVolume;
                    activeSource.Play();
                }
            }
        }

        #endregion

        #region Helper Methods

        private SoundtrackState GetStateByName(string name)
        {
            if (name == calmState?.stateName) return calmState;
            if (name == tenseState?.stateName) return tenseState;
            if (name == criticalState?.stateName) return criticalState;
            if (name == gameOverState?.stateName) return gameOverState;
            if (name == menuState?.stateName) return menuState;
            return null;
        }

        private void UpdateVolumes()
        {
            if (currentState != null)
            {
                float targetVolume = currentState.volume * musicVolume * masterVolume;
                activeSource.volume = targetVolume;
            }
        }

        /// <summary>
        /// Check if soundtrack system is ready
        /// </summary>
        public bool IsReady()
        {
            return (calmState != null && calmState.IsValid()) ||
                   (tenseState != null && tenseState.IsValid()) ||
                   (criticalState != null && criticalState.IsValid()) ||
                   (gameOverState != null && gameOverState.IsValid());
        }

        #endregion
    }
}