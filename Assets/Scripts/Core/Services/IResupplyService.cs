using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Service interface for resupply management
/// Handles air drops, barges, and loot distribution
/// </summary>
public interface IResupplyService : IResettable
{
    /// <summary>
    /// Start resupply events
    /// </summary>
    void StartResupply();

    /// <summary>
    /// Pause all resupply events
    /// </summary>
    void PauseResupply();

    /// <summary>
    /// Resume paused resupply
    /// </summary>
    void ResumeResupply();

    /// <summary>
    /// End all resupply and cleanup
    /// </summary>
    void EndResupply();

    /// <summary>
    /// Cancel all active coroutines and events
    /// CRITICAL for memory leak prevention
    /// </summary>
    void CancelAll();

    /// <summary>
    /// Schedule an air drop event
    /// </summary>
    void ScheduleAirDrop(float delay);

    /// <summary>
    /// Schedule a barge event
    /// </summary>
    void ScheduleBarge(float delay);

    /// <summary>
    /// Handle crate pickup by boat
    /// </summary>
    void OnCratePickup(GameObject crate, Collider collider);

    /// <summary>
    /// Get count of active packages/crates
    /// </summary>
    int ActivePackageCount { get; }

    /// <summary>
    /// Check if major event is active (plane/barge)
    /// </summary>
    bool IsMajorEventActive { get; }

    /// <summary>
    /// Check if resupply system is active
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Get time until next air drop in seconds (-1 if none scheduled)
    /// </summary>
    float GetTimeToNextAirDrop();

    /// <summary>
    /// Get time until next barge in seconds (-1 if none scheduled)
    /// </summary>
    float GetTimeToNextBarge();

    /// <summary>
    /// Get all active coroutines (for debug/cleanup verification)
    /// </summary>
    HashSet<Coroutine> ActiveCoroutines { get; }
}