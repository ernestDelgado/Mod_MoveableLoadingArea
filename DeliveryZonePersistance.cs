using System;
using System.Globalization;
using System.IO;
using BepInEx;
using UnityEngine;

namespace DeliveryZoneMover
{
    internal static class DeliveryZonePersistance
    {
        internal static void SavePlacementToFile()
        {
            try
            {
                string path = GetSaveFilePath();
                if (string.IsNullOrWhiteSpace(path))
                    return;

                string line =
                    DeliveryZoneController.CurrentTargetPosition.x.ToString("F6", CultureInfo.InvariantCulture) + "|" +
                    DeliveryZoneController.CurrentTargetPosition.y.ToString("F6", CultureInfo.InvariantCulture) + "|" +
                    DeliveryZoneController.CurrentTargetPosition.z.ToString("F6", CultureInfo.InvariantCulture) + "|" +
                    DeliveryZoneController.CurrentYaw.ToString("F6", CultureInfo.InvariantCulture);

                File.WriteAllText(path, line);
            }
            catch (Exception ex)
            {
                Plugin.LogError("SavePlacementToFile failed: " + ex);
            }
        }

        internal static bool LoadPlacementFromFile()
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

                DeliveryZoneController.CurrentTargetPosition = new Vector3(x, y, z);
                DeliveryZoneController.CurrentYaw = DeliveryZoneController.NormalizeYaw(yaw);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.LogError("LoadPlacementFromFile failed: " + ex);
                return false;
            }
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