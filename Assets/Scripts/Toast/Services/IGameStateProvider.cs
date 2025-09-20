using System;

namespace OilLeak.Toast.Services
{
    /// <summary>
    /// Interface for providing game state to the Toast system
    /// Allows ToastManager to work without direct dependency on GameSession
    /// </summary>
    public interface IGameStateProvider
    {
        // Current values
        float Timer { get; }
        float Integrity { get; }
        float GallonsBlocked { get; }
        float GallonsEscaped { get; }
        int ItemsDegraded { get; }
        int ResupplyCount { get; }
        int RunSeed { get; }

        // Events
        event Action<float> OnTimeUpdated;
        event Action<float> OnIntegrityChanged;
        event Action<float> OnGallonsBlockedChanged;
        event Action<float> OnGallonsEscapedChanged;
        event Action OnResupplyEvent;
    }

    /// <summary>
    /// Simple implementation for testing
    /// </summary>
    public class MockGameStateProvider : IGameStateProvider
    {
        public float Timer { get; set; }
        public float Integrity { get; set; } = 100f;
        public float GallonsBlocked { get; set; }
        public float GallonsEscaped { get; set; }
        public int ItemsDegraded { get; set; }
        public int ResupplyCount { get; set; }
        public int RunSeed { get; set; } = 12345;

        public event Action<float> OnTimeUpdated;
        public event Action<float> OnIntegrityChanged;
        public event Action<float> OnGallonsBlockedChanged;
        public event Action<float> OnGallonsEscapedChanged;
        public event Action OnResupplyEvent;

        public void UpdateTime(float time)
        {
            Timer = time;
            OnTimeUpdated?.Invoke(time);
        }

        public void UpdateIntegrity(float integrity)
        {
            Integrity = integrity;
            OnIntegrityChanged?.Invoke(integrity);
        }

        public void UpdateGallonsBlocked(float gallons)
        {
            GallonsBlocked = gallons;
            OnGallonsBlockedChanged?.Invoke(gallons);
        }

        public void TriggerResupply()
        {
            ResupplyCount++;
            OnResupplyEvent?.Invoke();
        }
    }
}