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

        private static GameObject cachedSortableBoxManager;
        private static Camera cachedCamera;
        private static Transform cachedSortAreaCanvas;
        private static Transform cachedIndicator;
        private static Transform cachedLoadingText;

        private static bool centerOffsetInitialized = false;
        private static Vector3 rootToCenterLocalOffset = Vector3.zero;

        private static int lastSortableBoxManagerInstanceId = 0;
        private static bool placementStateInitialized = false;

        private const float PlacementYOffset = 0.02f;
        private const float LookRayDistance = 100f;
        private const float GroundProbeStartHeight = 12f;
        private const float GroundProbeDistance = 60f;
        private const float RotationStep = 45f;
        private const float MaxPlacementDistance = 8f;

        public DeliveryZoneController(IntPtr ptr) : base(ptr) { }

        public void Update()
        {
            SyncFromLiveManagerIfNeeded();
            HandleEnterMoveModeInput();

            if (!MoveModeActive)
                return;

            UpdatePlacementFromLook();
            HandleRotationInput();
            ApplyCurrentPlacement();
            HandleConfirmInput();
        }

        private void HandleEnterMoveModeInput()
        {
            if (!Input.GetKeyDown(KeyCode.H))
                return;

            if (MoveModeActive)
                return;

            GameObject sortableBoxManager = GetSortableBoxManager();
            if (sortableBoxManager == null)
                return;

            InitializeCenterOffset();

            CurrentTargetPosition = sortableBoxManager.transform.position;
            CurrentYaw = NormalizeYaw(sortableBoxManager.transform.eulerAngles.y);
            MoveModeActive = true;
        }

        private void HandleRotationInput()
        {
            float scroll = Input.mouseScrollDelta.y;

            if (scroll > 0f)
                CurrentYaw = NormalizeYaw(CurrentYaw + RotationStep);
            else if (scroll < 0f)
                CurrentYaw = NormalizeYaw(CurrentYaw - RotationStep);
        }

        private void HandleConfirmInput()
        {
            if (!Input.GetMouseButtonDown(0))
                return;

            ApplyCurrentPlacement();
            MoveModeActive = false;
        }

        private void UpdatePlacementFromLook()
        {
            Camera cam = GetPlacementCamera();
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
                GameObject sortableBoxManager = GetSortableBoxManager();
                if (sortableBoxManager == null)
                    return;

                Camera cam = GetPlacementCamera();
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

        internal static void SavePlacementToFile()
        {
            try
            {
                string path = GetSaveFilePath();
                if (string.IsNullOrWhiteSpace(path))
                    return;

                string line =
                    CurrentTargetPosition.x.ToString("F6", CultureInfo.InvariantCulture) + "|" +
                    CurrentTargetPosition.y.ToString("F6", CultureInfo.InvariantCulture) + "|" +
                    CurrentTargetPosition.z.ToString("F6", CultureInfo.InvariantCulture) + "|" +
                    CurrentYaw.ToString("F6", CultureInfo.InvariantCulture);

                File.WriteAllText(path, line);
            }
            catch (Exception ex)
            {
                Plugin.LogError("SavePlacementToFile failed: " + ex);
            }
        }

        private static bool LoadPlacementFromFile()
        {
            try
            {
                string path = GetSaveFilePath();
                if (string.IsNullOrWhiteSpace(path))
                    return false;

                if (!File.Exists(path))
                    return false;

                string line = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(line))
                    return false;

                string[] parts = line.Split('|');
                if (parts.Length != 4)
                    return false;

                float x = float.Parse(parts[0], CultureInfo.InvariantCulture);
                float y = float.Parse(parts[1], CultureInfo.InvariantCulture);
                float z = float.Parse(parts[2], CultureInfo.InvariantCulture);
                float yaw = float.Parse(parts[3], CultureInfo.InvariantCulture);

                CurrentTargetPosition = new Vector3(x, y, z);
                CurrentYaw = NormalizeYaw(yaw);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.LogError("LoadPlacementFromFile failed: " + ex);
                return false;
            }
        }

        private static void AdjustVisualLoadingZoneHeight()
        {
            GameObject sortableBoxManager = GetSortableBoxManager();
            if (sortableBoxManager == null)
                return;

            if (cachedSortAreaCanvas == null)
                CacheVisualReferences();

            if (cachedSortAreaCanvas == null)
                return;

            Vector3 rootPos = sortableBoxManager.transform.position;
            Vector3 floorProbeStart = new Vector3(rootPos.x, rootPos.y + GroundProbeStartHeight, rootPos.z);

            Camera cam = GetPlacementCamera();
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

                Vector3 canvasPos = cachedSortAreaCanvas.position;
                canvasPos.y = visualY;
                cachedSortAreaCanvas.position = canvasPos;

                if (cachedIndicator != null)
                {
                    Vector3 p = cachedIndicator.position;
                    p.y = visualY;
                    cachedIndicator.position = p;
                }

                if (cachedLoadingText != null)
                {
                    Vector3 p = cachedLoadingText.position;
                    p.y = visualY;
                    cachedLoadingText.position = p;
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
            string path = GetTransformPath(t);

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

        private static void InitializeCenterOffset()
        {
            if (centerOffsetInitialized)
                return;

            GameObject sortableBoxManager = GetSortableBoxManager();
            if (sortableBoxManager == null)
                return;

            Transform indicator = FindChildRecursive(sortableBoxManager.transform, "Indicator");
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

        private static void CacheVisualReferences()
        {
            GameObject sortableBoxManager = GetSortableBoxManager();
            if (sortableBoxManager == null)
            {
                cachedSortAreaCanvas = null;
                cachedIndicator = null;
                cachedLoadingText = null;
                return;
            }

            cachedSortAreaCanvas = FindChildRecursive(sortableBoxManager.transform, "Sort Area Canvas");
            cachedIndicator = cachedSortAreaCanvas != null
                ? FindChildRecursive(cachedSortAreaCanvas, "Indicator")
                : null;

            cachedLoadingText = cachedSortAreaCanvas != null
                ? FindChildRecursive(cachedSortAreaCanvas, "Text (TMP)")
                : null;
        }

        private static GameObject GetSortableBoxManager()
        {
            if (cachedSortableBoxManager != null)
                return cachedSortableBoxManager;

            GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>(true);

            for (int i = 0; i < allObjects.Length; i++)
            {
                GameObject go = allObjects[i];
                if (go != null && go.name == "SortableBoxManager")
                {
                    cachedSortableBoxManager = go;
                    return cachedSortableBoxManager;
                }
            }

            return null;
        }

        private static Camera GetPlacementCamera()
        {
            if (cachedCamera != null)
                return cachedCamera;

            if (Camera.main != null)
            {
                cachedCamera = Camera.main;
                return cachedCamera;
            }

            Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].enabled)
                {
                    cachedCamera = cameras[i];
                    return cachedCamera;
                }
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform root, string exactName)
        {
            if (root == null)
                return null;

            if (root.name == exactName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), exactName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static string GetTransformPath(Transform t)
        {
            if (t == null)
                return string.Empty;

            List<string> parts = new List<string>();
            Transform current = t;

            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        private static float NormalizeYaw(float yaw)
        {
            while (yaw < 0f)
                yaw += 360f;

            while (yaw >= 360f)
                yaw -= 360f;

            return yaw;
        }

        private static void SyncFromLiveManagerIfNeeded()
        {
            GameObject sortableBoxManager = GetSortableBoxManager();
            if (sortableBoxManager == null)
                return;

            int instanceId = sortableBoxManager.GetInstanceID();

            if (placementStateInitialized && instanceId == lastSortableBoxManagerInstanceId)
                return;

            cachedSortableBoxManager = sortableBoxManager;
            cachedCamera = null;
            cachedSortAreaCanvas = null;
            cachedIndicator = null;
            cachedLoadingText = null;

            centerOffsetInitialized = false;
            InitializeCenterOffset();
            CacheVisualReferences();

            MoveModeActive = false;
            lastSortableBoxManagerInstanceId = instanceId;
            placementStateInitialized = true;

            CurrentTargetPosition = sortableBoxManager.transform.position;
            CurrentYaw = NormalizeYaw(sortableBoxManager.transform.eulerAngles.y);

            if (LoadPlacementFromFile())
                ApplyCurrentPlacement();
        }

        private static string GetSaveFilePath()
        {
            try
            {
                if (SaveManager.Instance == null)
                    return null;

                string currentSavePath = SaveManager.Instance.m_CurrentSaveFilePath;
                if (string.IsNullOrWhiteSpace(currentSavePath))
                    return null;

                string saveKey = MakeSafeFileKey(currentSavePath);
                if (string.IsNullOrWhiteSpace(saveKey))
                    return null;

                return Path.Combine(Paths.ConfigPath, "DeliveryZoneMover_Position_" + saveKey + ".txt");
            }
            catch
            {
                return null;
            }
        }

        private static string MakeSafeFileKey(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            char[] invalid = Path.GetInvalidFileNameChars();
            string safe = raw;

            for (int i = 0; i < invalid.Length; i++)
                safe = safe.Replace(invalid[i], '_');

            safe = safe.Replace('\\', '_').Replace('/', '_').Replace(':', '_');

            if (safe.Length > 120)
                safe = safe.Substring(safe.Length - 120);

            return safe;
        }
    }
}