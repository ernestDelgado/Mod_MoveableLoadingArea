using System.Collections.Generic;
using UnityEngine;

namespace DeliveryZoneMover
{
    internal static class DeliveryZoneLocator
    {
        internal static GameObject CachedSortableBoxManager;
        internal static Camera CachedCamera;
        internal static Transform CachedSortAreaCanvas;
        internal static Transform CachedIndicator;
        internal static Transform CachedLoadingText;

        internal static void CacheVisualReferences()
        {
            GameObject sortableBoxManager = GetSortableBoxManager();
            if (sortableBoxManager == null)
            {
                CachedSortAreaCanvas = null;
                CachedIndicator = null;
                CachedLoadingText = null;
                return;
            }

            CachedSortAreaCanvas = FindChildRecursive(sortableBoxManager.transform, "Sort Area Canvas");
            CachedIndicator = CachedSortAreaCanvas != null
                ? FindChildRecursive(CachedSortAreaCanvas, "Indicator")
                : null;

            CachedLoadingText = CachedSortAreaCanvas != null
                ? FindChildRecursive(CachedSortAreaCanvas, "Text (TMP)")
                : null;
        }

        internal static GameObject GetSortableBoxManager()
        {
            if (CachedSortableBoxManager != null)
                return CachedSortableBoxManager;

            GameObject[] allObjects = Object.FindObjectsOfType<GameObject>(true);

            for (int i = 0; i < allObjects.Length; i++)
            {
                GameObject go = allObjects[i];
                if (go != null && go.name == "SortableBoxManager")
                {
                    CachedSortableBoxManager = go;
                    return CachedSortableBoxManager;
                }
            }

            return null;
        }

        internal static Camera GetPlacementCamera()
        {
            if (CachedCamera != null)
                return CachedCamera;

            if (Camera.main != null)
            {
                CachedCamera = Camera.main;
                return CachedCamera;
            }

            Camera[] cameras = Object.FindObjectsOfType<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].enabled)
                {
                    CachedCamera = cameras[i];
                    return CachedCamera;
                }
            }

            return null;
        }

        internal static Transform FindChildRecursive(Transform root, string exactName)
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

        internal static string GetTransformPath(Transform t)
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
    }
}