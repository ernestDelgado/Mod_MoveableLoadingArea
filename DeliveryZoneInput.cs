using UnityEngine;

namespace DeliveryZoneMover
{
    internal static class DeliveryZoneInput
    {
        internal static void HandleEnterMoveModeInput()
        {
            // Press H to begin moving the delivery zone
            if (!Input.GetKeyDown(KeyCode.H))
                return;

            // Do not re-enter if already moving
            if (DeliveryZoneController.MoveModeActive)
                return;

            // Get the real delivery zone manager object
            GameObject sortableBoxManager = DeliveryZoneLocator.GetSortableBoxManager();
            if (sortableBoxManager == null)
                return;

            // Make sure root-to-center offset is ready before placement starts
            DeliveryZoneController.InitializeCenterOffset();

            // Start editing from the current real position/rotation
            DeliveryZoneController.CurrentTargetPosition = sortableBoxManager.transform.position;
            DeliveryZoneController.CurrentYaw =
                DeliveryZoneController.NormalizeYaw(sortableBoxManager.transform.eulerAngles.y);

            DeliveryZoneController.MoveModeActive = true;
        }

        internal static void HandleRotationInput()
        {
            float scroll = Input.mouseScrollDelta.y;

            if (scroll > 0f)
                DeliveryZoneController.CurrentYaw =
                    DeliveryZoneController.NormalizeYaw(
                        DeliveryZoneController.CurrentYaw + DeliveryZoneController.RotationStep);
            else if (scroll < 0f)
                DeliveryZoneController.CurrentYaw =
                    DeliveryZoneController.NormalizeYaw(
                        DeliveryZoneController.CurrentYaw - DeliveryZoneController.RotationStep);
        }

        internal static void HandleConfirmInput()
        {
            // Left click confirms the placement
            if (!Input.GetMouseButtonDown(0))
                return;

            DeliveryZoneController.ApplyCurrentPlacement();
            DeliveryZoneController.MoveModeActive = false;
        }
    }
}