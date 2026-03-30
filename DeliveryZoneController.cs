using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using UnityEngine;

namespace DeliveryZoneMover
{
    public class DeliveryZoneController : MonoBehaviour
    {
        internal static Vector3 CurrentTargetPosition = new Vector3(5.000f, 0.075f, 0.980f);
        internal static float CurrentYaw = 0f;
        internal static bool MoveModeActive = false;
        internal static float VisualHeightOffset = 0.0f;

        private static bool centerOffsetInitialized = false;
        private static Vector3 rootToCenterLocalOffset = Vector3.zero;

        private static int lastSortableBoxManagerInstanceId = 0;
        private static bool placementStateInitialized = false;

        private const float PlacementYOffset = 0.02f;
        private const float LookRayDistance = 100f;
        private const float GroundProbeStartHeight = 12f;
        private const float GroundProbeDistance = 60f;
        internal const float RotationStep = 45f;
        private const float MaxPlacementDistance = 8f;

        public DeliveryZoneController(IntPtr ptr) : base(ptr) { }

        public void Update()
        {
            SyncFromLiveManagerIfNeeded();
            DeliveryZoneInput.HandleEnterMoveModeInput();

            if (!MoveModeActive)
                return;

            UpdatePlacementFromLook();
            DeliveryZoneInput.HandleRotationInput();
            ApplyCurrentPlacement();
            DeliveryZoneInput.HandleConfirmInput();
        }

        private void UpdatePlacementFromLook()
        {
            Camera cam = DeliveryZoneLocator.GetPlacementCamera();
            if (cam == null)
                return;

            InitializeCenterOffset();

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit[] hits = Physics.RaycastAll(ray, LookRayDistance);
            if (hits == null || hits.Length == 0)
                return;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Vector3 cameraPos = cam.transform.position;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];

                if (!IsValidFloorHit(hit, cameraPos))
                    continue;

                Vector3 desiredCenter = hit.point;

                Vector3 flatDelta = desiredCenter - cameraPos;
                flatDelta.y = 0f;

                float flatDistance = flatDelta.magnitude;
                if (flatDistance > MaxPlacementDistance && flatDistance > 0.001f)
                {
                    Vector3 flatDir = flatDelta.normalized;
                    desiredCenter = new Vector3(
                        cameraPos.x + flatDir.x * MaxPlacementDistance,
                        desiredCenter.y,
                        cameraPos.z + flatDir.z * MaxPlacementDistance
                    );
                }

                if (!TrySnapPointToFloor(desiredCenter, cameraPos, out Vector3 groundedCenter))
                    continue;

                Vector3 rotatedCenterOffset = Quaternion.Euler(0f, CurrentYaw, 0f) * rootToCenterLocalOffset;
                Vector3 proposedRoot = groundedCenter - rotatedCenterOffset;

                if (!TrySnapRootToFloor(proposedRoot, cameraPos, out Vector3 groundedRoot))
                    continue;

                CurrentTargetPosition = groundedRoot;
                return;
            }
        }

        internal static void ApplyCurrentPlacement()
        {
            try
            {
                GameObject sortableBoxManager = DeliveryZoneLocator.GetSortableBoxManager();
                if (sortableBoxManager == null)
                    return;

                Camera cam = DeliveryZoneLocator.GetPlacementCamera();
                Vector3 cameraPos = cam != null ? cam.transform.position : Vector3.zero;

                if (TrySnapRootToFloor(CurrentTargetPosition, cameraPos, out Vector3 groundedRoot))
                    CurrentTargetPosition = groundedRoot;

                sortableBoxManager.transform.position = CurrentTargetPosition;
                sortableBoxManager.transform.rotation = Quaternion.Euler(0f, CurrentYaw, 0f);

                AdjustVisualLoadingZoneHeight();
            }
            catch (Exception ex)
            {
                Plugin.LogError("ApplyCurrentPlacement failed: " + ex);
            }
        }

        private static void AdjustVisualLoadingZoneHeight()
        {
            GameObject sortableBoxManager = DeliveryZoneLocator.GetSortableBoxManager();
            if (sortableBoxManager == null)
                return;

            if (DeliveryZoneLocator.CachedSortAreaCanvas == null)
                DeliveryZoneLocator.CacheVisualReferences();

            if (DeliveryZoneLocator.CachedSortAreaCanvas == null)
                return;

            Vector3 rootPos = sortableBoxManager.transform.position;
            Vector3 floorProbeStart = new Vector3(rootPos.x, rootPos.y + GroundProbeStartHeight, rootPos.z);

            Camera cam = DeliveryZoneLocator.GetPlacementCamera();
            Vector3 cameraPos = cam != null ? cam.transform.position : Vector3.zero;

            RaycastHit[] hits = Physics.RaycastAll(floorProbeStart, Vector3.down, GroundProbeDistance);
            if (hits == null || hits.Length == 0)
                return;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];

                if (!IsValidFloorHit(hit, cameraPos))
                    continue;

                float visualY = hit.point.y + 0.06f + VisualHeightOffset;

                Vector3 canvasPos = DeliveryZoneLocator.CachedSortAreaCanvas.position;
                canvasPos.y = visualY;
                DeliveryZoneLocator.CachedSortAreaCanvas.position = canvasPos;

                if (DeliveryZoneLocator.CachedIndicator != null)
                {
                    Vector3 p = DeliveryZoneLocator.CachedIndicator.position;
                    p.y = visualY;
                    DeliveryZoneLocator.CachedIndicator.position = p;
                }

                if (DeliveryZoneLocator.CachedLoadingText != null)
                {
                    Vector3 p = DeliveryZoneLocator.CachedLoadingText.position;
                    p.y = visualY;
                    DeliveryZoneLocator.CachedLoadingText.position = p;
                }

                return;
            }
        }

        private static bool TrySnapPointToFloor(Vector3 pointGuess, Vector3 cameraPos, out Vector3 groundedPoint)
        {
            groundedPoint = pointGuess;

            Vector3 rayOrigin = new Vector3(
                pointGuess.x,
                pointGuess.y + GroundProbeStartHeight,
                pointGuess.z
            );

            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, GroundProbeDistance);
            if (hits == null || hits.Length == 0)
                return false;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];

                if (!IsValidFloorHit(hit, cameraPos))
                    continue;

                groundedPoint = new Vector3(
                    pointGuess.x,
                    hit.point.y + PlacementYOffset,
                    pointGuess.z
                );
                return true;
            }

            return false;
        }

        private static bool TrySnapRootToFloor(Vector3 rootGuess, Vector3 cameraPos, out Vector3 groundedRoot)
        {
            groundedRoot = rootGuess;

            Vector3 rayOrigin = new Vector3(
                rootGuess.x,
                rootGuess.y + GroundProbeStartHeight,
                rootGuess.z
            );

            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, GroundProbeDistance);
            if (hits == null || hits.Length == 0)
                return false;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];

                if (!IsValidFloorHit(hit, cameraPos))
                    continue;

                groundedRoot = new Vector3(
                    rootGuess.x,
                    hit.point.y + PlacementYOffset,
                    rootGuess.z
                );
                return true;
            }

            return false;
        }

        private static bool IsValidFloorHit(RaycastHit hit, Vector3 cameraPos)
        {
            if (hit.collider == null)
                return false;

            Transform t = hit.collider.transform;
            if (t == null)
                return false;

            GameObject go = t.gameObject;
            int layer = go.layer;
            string tag = go.tag ?? string.Empty;
            string name = t.name ?? string.Empty;
            string path = DeliveryZoneLocator.GetTransformPath(t);

            if (hit.normal.y < 0.85f)
                return false;

            if (hit.point.y > cameraPos.y + 0.25f)
                return false;

            if (layer == 3)
                return false;

            if (name.IndexOf("OutOfBoundsTrigger", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            if (name.IndexOf("Plane", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            if (name.IndexOf("Dead Zone", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            if (path.IndexOf("PlayerManager/Player", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            if (layer == 21)
                return true;

            if (string.Equals(tag, "Floor", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(tag, "Storage Floor", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(tag, "Sidewalk", StringComparison.OrdinalIgnoreCase))
                return true;

            if (layer == 12)
                return true;

            return false;
        }

        internal static void InitializeCenterOffset()
        {
            if (centerOffsetInitialized)
                return;

            GameObject sortableBoxManager = DeliveryZoneLocator.GetSortableBoxManager();
            if (sortableBoxManager == null)
                return;

            Transform indicator = DeliveryZoneLocator.FindChildRecursive(sortableBoxManager.transform, "Indicator");
            if (indicator != null)
            {
                rootToCenterLocalOffset =
                    Quaternion.Inverse(sortableBoxManager.transform.rotation) *
                    (indicator.position - sortableBoxManager.transform.position);

                centerOffsetInitialized = true;
                return;
            }

            rootToCenterLocalOffset = Vector3.zero;
            centerOffsetInitialized = true;
        }

        internal static float NormalizeYaw(float yaw)
        {
            while (yaw < 0f)
                yaw += 360f;

            while (yaw >= 360f)
                yaw -= 360f;

            return yaw;
        }

        private static void SyncFromLiveManagerIfNeeded()
        {
            GameObject sortableBoxManager = DeliveryZoneLocator.GetSortableBoxManager();
            if (sortableBoxManager == null)
                return;

            int instanceId = sortableBoxManager.GetInstanceID();

            if (placementStateInitialized && instanceId == lastSortableBoxManagerInstanceId)
                return;

            DeliveryZoneLocator.CachedSortableBoxManager = sortableBoxManager;
            DeliveryZoneLocator.CachedCamera = null;
            DeliveryZoneLocator.CachedSortAreaCanvas = null;
            DeliveryZoneLocator.CachedIndicator = null;
            DeliveryZoneLocator.CachedLoadingText = null;

            centerOffsetInitialized = false;
            InitializeCenterOffset();
            DeliveryZoneLocator.CacheVisualReferences();

            MoveModeActive = false;
            lastSortableBoxManagerInstanceId = instanceId;
            placementStateInitialized = true;

            CurrentTargetPosition = sortableBoxManager.transform.position;
            CurrentYaw = NormalizeYaw(sortableBoxManager.transform.eulerAngles.y);

            if (DeliveryZonePersistance.LoadPlacementFromFile())
                ApplyCurrentPlacement();
        }

    }
}