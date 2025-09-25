using UnityEngine;

namespace OilLeak.Audio
{
    /// <summary>
    /// Defines a soundtrack state with playlist and playback settings
    /// Used by SoundtrackManager to manage music for different game states
    /// </summary>
    [CreateAssetMenu(fileName = "SoundtrackState", menuName = "OilLeak/Audio/Soundtrack State", order = 1)]
    public class SoundtrackState : ScriptableObject
    {
        [Header("State Identity")]
        [Tooltip("Name of this soundtrack state (e.g., Calm, Tense, Critical)")]
        public string stateName = "Unnamed State";

        [Header("Playlist")]
        [Tooltip("Audio clips to play in this state")]
        public AudioClip[] playlist;

        [Header("Playback Settings")]
        [Tooltip("Randomize playlist order")]
        public bool shuffle = true;

        [Tooltip("Loop back to start when playlist ends")]
        public bool loopPlaylist = true;

        [Tooltip("Volume for this state (0-1)")]
        [Range(0f, 1f)]
        public float volume = 1f;

        [Header("Transition Settings")]
        [Tooltip("Crossfade duration when transitioning to this state (seconds)")]
        [Range(0.1f, 5f)]
        public float crossfadeSeconds = 1.5f;

        [Tooltip("Gap between tracks in playlist (seconds)")]
        [Range(0f, 5f)]
        public float minGap = 0f;

        [Header("Debug")]
        [Tooltip("Description for designers")]
        [TextArea(2, 4)]
        public string description;

        /// <summary>
        /// Validate the state has usable content
        /// </summary>
        public bool IsValid()
        {
            return playlist != null && playlist.Length > 0;
        }

        /// <summary>
        /// Get a shuffled copy of the playlist
        /// </summary>
        public AudioClip[] GetShuffledPlaylist()
        {
            if (playlist == null || playlist.Length == 0)
                return new AudioClip[0];

            AudioClip[] shuffled = new AudioClip[playlist.Length];
            System.Array.Copy(playlist, shuffled, playlist.Length);

            // Fisher-Yates shuffle
            for (int i = shuffled.Length - 1; i > 0; i--)
            {
                int randomIndex = UnityEngine.Random.Range(0, i + 1);
                AudioClip temp = shuffled[i];
                shuffled[i] = shuffled[randomIndex];
                shuffled[randomIndex] = temp;
            }

            return shuffled;
        }

        /// <summary>
        /// Get playlist in order or shuffled based on settings
        /// </summary>
        public AudioClip[] GetPlaylist()
        {
            if (shuffle)
                return GetShuffledPlaylist();
            else
                return playlist;
        }
    }
}