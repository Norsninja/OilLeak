namespace Core.Services
{
    /// <summary>
    /// Service interface for player movement control
    /// Allows game systems to enable/disable player input
    /// </summary>
    public interface IPlayerMovementService
    {
        /// <summary>
        /// Enable or disable player movement
        /// </summary>
        void EnableMovement(bool canMove);

        /// <summary>
        /// Check if movement is currently enabled
        /// </summary>
        bool IsMovementEnabled { get; }

        /// <summary>
        /// Reset player to starting position
        /// </summary>
        void ResetPosition();
    }
}