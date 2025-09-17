using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Core.Services
{
    /// <summary>
    /// Adapter wrapping ResupplyManager to provide IResupplyService interface
    /// Maps ResupplyManager's methods to the service interface contract
    /// </summary>
    public class ResupplyManagerAdapter : IResupplyService
    {
        private readonly ResupplyManager wrapped;
        private readonly HashSet<Coroutine> trackedCoroutines;

        public ResupplyManagerAdapter(ResupplyManager manager)
        {
            wrapped = manager ?? throw new System.ArgumentNullException(nameof(manager));
            trackedCoroutines = new HashSet<Coroutine>();
        }

        // === IResupplyService Implementation ===

        /// <summary>
        /// Start resupply events
        /// </summary>
        public void StartResupply()
        {
            wrapped.StartResupply();
        }

        /// <summary>
        /// Pause all resupply events
        /// </summary>
        public void PauseResupply()
        {
            wrapped.PauseResupply();
        }

        /// <summary>
        /// Resume paused resupply
        /// </summary>
        public void ResumeResupply()
        {
            wrapped.ResumeResupply();
        }

        /// <summary>
        /// End all resupply and cleanup
        /// </summary>
        public void EndResupply()
        {
            wrapped.EndResupply();
        }

        /// <summary>
        /// Cancel all active coroutines and events
        /// </summary>
        public void CancelAll()
        {
            // Stop all coroutines on the manager
            wrapped.StopAllCoroutines();

            // Clear tracking
            trackedCoroutines.Clear();

            // End resupply to clean up active objects
            wrapped.EndResupply();
        }

        /// <summary>
        /// Schedule an air drop event
        /// Note: ResupplyManager handles this internally, so we log for now
        /// </summary>
        public void ScheduleAirDrop(float delay)
        {
            // ResupplyManager schedules drops internally based on config
            // This would require exposing a new method on ResupplyManager
            Debug.Log($"[ResupplyManagerAdapter] ScheduleAirDrop({delay}s) - handled internally by manager");
        }

        /// <summary>
        /// Schedule a barge event
        /// Note: ResupplyManager handles this internally, so we log for now
        /// </summary>
        public void ScheduleBarge(float delay)
        {
            // ResupplyManager schedules barges internally based on milestones
            // This would require exposing a new method on ResupplyManager
            Debug.Log($"[ResupplyManagerAdapter] ScheduleBarge({delay}s) - handled internally by manager");
        }

        /// <summary>
        /// Handle crate pickup by boat
        /// Note: ResupplyManager handles this via collision events
        /// </summary>
        public void OnCratePickup(GameObject crate, Collider collider)
        {
            // ResupplyManager handles pickups internally via collision detection
            // This is more of an event notification than a command
            Debug.Log($"[ResupplyManagerAdapter] OnCratePickup - handled via collision system");
        }

        /// <summary>
        /// Get count of active packages/crates
        /// Note: Would need to expose this from ResupplyManager
        /// </summary>
        public int ActivePackageCount
        {
            get
            {
                // ResupplyManager doesn't expose this yet
                // Would need to add public property for activePackages.Count + activeCrates.Count
                Debug.LogWarning("[ResupplyManagerAdapter] ActivePackageCount not exposed by ResupplyManager");
                return 0;
            }
        }

        /// <summary>
        /// Check if major event is active (plane/barge)
        /// </summary>
        public bool IsMajorEventActive => wrapped.IsMajorEventActive;

        /// <summary>
        /// Get all active coroutines (for debug/cleanup verification)
        /// </summary>
        public HashSet<Coroutine> ActiveCoroutines => trackedCoroutines;

        // === IResettable Implementation (forward to wrapped) ===

        /// <summary>
        /// Reset to initial state
        /// </summary>
        public void Reset()
        {
            wrapped.Reset();
            trackedCoroutines.Clear();
        }

        /// <summary>
        /// Verify the service is properly cleaned
        /// </summary>
        public bool IsClean => wrapped.IsClean;

        // === Helper Methods ===

        /// <summary>
        /// Track a coroutine started by the manager
        /// </summary>
        public void TrackCoroutine(Coroutine coroutine)
        {
            if (coroutine != null)
            {
                trackedCoroutines.Add(coroutine);
            }
        }

        /// <summary>
        /// Stop tracking a completed coroutine
        /// </summary>
        public void UntrackCoroutine(Coroutine coroutine)
        {
            if (coroutine != null)
            {
                trackedCoroutines.Remove(coroutine);
            }
        }
    }
}