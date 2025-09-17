using UnityEngine;
using Core;

namespace Core.Services
{
    /// <summary>
    /// Adapter that bridges PlayerController to IPlayerMovementService
    /// Implements IResettable for clean restart support
    /// </summary>
    public class PlayerControllerAdapter : IPlayerMovementService, IResettable
    {
        private readonly PlayerController controller;

        public PlayerControllerAdapter(PlayerController controller)
        {
            this.controller = controller;
            if (controller == null)
            {
                Debug.LogError("[PlayerAdapter] Controller is null!");
            }
        }

        #region IPlayerMovementService Implementation

        public void EnableMovement(bool canMove)
        {
            if (controller != null)
            {
                controller.EnableMovement(canMove);
                Debug.Log($"[PlayerAdapter] Movement {(canMove ? "enabled" : "disabled")}");
            }
        }

        public bool IsMovementEnabled => controller != null && controller.IsMovementEnabled;

        public void ResetPosition()
        {
            if (controller != null)
            {
                controller.ResetPosition();
                Debug.Log("[PlayerAdapter] Position reset");
            }
        }

        #endregion

        #region IResettable Implementation

        public void Reset()
        {
            EnableMovement(false);
            ResetPosition();
            Debug.Log("[PlayerAdapter] State reset");
        }

        public bool IsClean => !IsMovementEnabled;

        #endregion
    }
}