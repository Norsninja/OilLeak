using UnityEngine;

/// <summary>
/// Service interface for audio management
/// Handles all sound effects and music
/// </summary>
public interface IAudioService : IResettable
{
    /// <summary>
    /// Play a sound effect at position
    /// </summary>
    void PlaySound(AudioClip clip, Vector3 position, float volume = 1f);

    /// <summary>
    /// Play a 2D UI sound
    /// </summary>
    void PlayUISound(AudioClip clip, float volume = 1f);

    /// <summary>
    /// Play or change background music
    /// </summary>
    void PlayMusic(MusicType musicType);

    /// <summary>
    /// Stop all audio immediately
    /// </summary>
    void StopAll();

    /// <summary>
    /// Pause all audio
    /// </summary>
    void PauseAll();

    /// <summary>
    /// Resume paused audio
    /// </summary>
    void ResumeAll();

    /// <summary>
    /// Set master volume
    /// </summary>
    void SetMasterVolume(float volume);

    /// <summary>
    /// Set sound effects volume
    /// </summary>
    void SetSFXVolume(float volume);

    /// <summary>
    /// Set music volume
    /// </summary>
    void SetMusicVolume(float volume);
}

/// <summary>
/// Music types for different game states
/// </summary>
public enum MusicType
{
    Menu,
    Gameplay,
    Danger,
    Failure
}